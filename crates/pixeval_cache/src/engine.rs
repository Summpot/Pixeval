// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use futures_util::StreamExt;
use parking_lot::RwLock;
use pixeval_maho::{DnsResolver, IMAGE_HOST, IMAGE_HOST2, MahoConfig, MahoHttpClient};
use std::collections::{HashMap, VecDeque};
use std::fs;
use std::net::IpAddr;
use std::path::PathBuf;
use std::sync::Arc;
use std::sync::atomic::{AtomicU64, Ordering};

use foyer::{
    BlockEngineConfig, DeviceBuilder, FileDeviceBuilder, HybridCache, HybridCacheBuilder,
    HybridCachePolicy, PsyncIoEngineConfig,
};

use crate::error::CacheError;
use crate::planar::{
    decode_planar_lz4_to_bgra, decode_planar_lz4_to_bgra_vec, encode_planar_lz4, is_planar_lz4,
};

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Record)]
pub struct CacheStats {
    pub file_count: u32,
    pub entry_count: u32,
    pub total_allocated_bytes: u64,
    pub total_used_bytes: u64,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct PlanarImageResult {
    pub width: u32,
    pub height: u32,
    pub bgra_data: Vec<u8>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Record)]
pub struct PlanarImageInfo {
    pub width: u32,
    pub height: u32,
}

#[uniffi::export(callback_interface)]
pub trait CachePreviewCallback: Send + Sync {
    fn on_preview_frame(&self, frame_data: Vec<u8>);
    fn on_progress(&self, downloaded_bytes: u64, total_bytes: u64);
}

struct EngineMeta {
    entries: HashMap<String, usize>,
    lru_order: VecDeque<String>,
}

fn build_http_client(
    domain_fronting_enabled: bool,
    split_delay_ms: u64,
    host_ips: &HashMap<String, Vec<String>>,
    proxy_url: Option<&str>,
) -> MahoHttpClient {
    let resolver = DnsResolver::new();
    for (host, ips) in host_ips {
        let parsed: Vec<IpAddr> = ips.iter().filter_map(|s| s.parse().ok()).collect();
        resolver.set_static_ips(host, parsed);
    }
    if resolver.get_static_ips(IMAGE_HOST).is_none() {
        resolver.set_static_ips(
            IMAGE_HOST,
            vec![
                "210.140.139.134".parse().unwrap(),
                "210.140.139.135".parse().unwrap(),
                "210.140.139.136".parse().unwrap(),
                "210.140.139.137".parse().unwrap(),
            ],
        );
    }
    if resolver.get_static_ips(IMAGE_HOST2).is_none() {
        resolver.set_static_ips(
            IMAGE_HOST2,
            vec![
                "210.140.139.135".parse().unwrap(),
                "210.140.139.136".parse().unwrap(),
                "210.140.139.137".parse().unwrap(),
            ],
        );
    }

    let maho_config = Arc::new(MahoConfig {
        enabled: domain_fronting_enabled,
        split_delay_ms,
        dns_resolver: resolver,
    });

    let clean_proxy = proxy_url
        .map(|s| s.trim())
        .filter(|s| !s.is_empty())
        .map(|s| s.to_string());

    MahoHttpClient::new(maho_config, clean_proxy)
}

#[derive(uniffi::Object)]
pub struct CacheEngine {
    hybrid_cache: HybridCache<String, Vec<u8>>,
    rt: Arc<tokio::runtime::Runtime>,
    http_client: Arc<parking_lot::RwLock<MahoHttpClient>>,
    _cache_dir: PathBuf,
    _cache_file: PathBuf,
    storage_capacity: u64,
    meta: Arc<RwLock<EngineMeta>>,
    total_used_bytes: Arc<AtomicU64>,
}

#[uniffi::export(async_runtime = "tokio")]
impl CacheEngine {
    #[uniffi::constructor]
    pub fn new(
        cache_dir: String,
        initial_file_size: u64,
        max_files: u32,
    ) -> Result<Arc<Self>, CacheError> {
        let dir = PathBuf::from(&cache_dir);
        fs::create_dir_all(&dir)?;

        // Clean up legacy custom MMF chunk files (*.bin / *.cache / cache_index.bin)
        if let Ok(entries) = fs::read_dir(&dir) {
            for entry in entries.flatten() {
                let name = entry.file_name().to_string_lossy().to_string();
                if (name.starts_with("cache_chunk_") && name.ends_with(".bin"))
                    || name == "cache_index.bin"
                    || name == "cache_index.bin.tmp"
                    || entry.path().extension().is_some_and(|e| e == "cache")
                {
                    let _ = fs::remove_file(entry.path());
                }
            }
        }

        let max_files = max_files.max(1);
        let storage_capacity = ((initial_file_size as usize) * (max_files as usize))
            .max(64 * 1024)
            .min(16 * 1024 * 1024 * 1024) as u64;

        let cache_file = dir.join("foyer_cache.bin");

        let rt = Arc::new(
            tokio::runtime::Builder::new_multi_thread()
                .enable_all()
                .build()
                .map_err(|e| CacheError::Io {
                    message: format!("Failed to create Tokio runtime: {e}"),
                })?,
        );

        let cache_file_clone = cache_file.clone();
        let hybrid_cache = rt.block_on(async {
            HybridCacheBuilder::new()
                .with_name("pixeval_foyer")
                .with_policy(HybridCachePolicy::WriteOnInsertion)
                .memory((storage_capacity / 8).max(4 * 1024 * 1024) as usize)
                .with_weighter(|_k, v: &Vec<u8>| (v.len() + 7) & !7)
                .storage()
                .with_io_engine_config(PsyncIoEngineConfig::new())
                .with_engine_config(
                    BlockEngineConfig::new(
                        FileDeviceBuilder::new(&cache_file_clone)
                            .with_capacity(storage_capacity as usize)
                            .build()?,
                    )
                    .with_block_size(64 * 1024),
                )
                .with_recover_mode(foyer::RecoverMode::Quiet)
                .build()
                .await
                .map_err(|e| CacheError::Io {
                    message: format!("Failed to build foyer HybridCache: {e}"),
                })
        })?;

        let meta = Arc::new(RwLock::new(EngineMeta {
            entries: HashMap::new(),
            lru_order: VecDeque::new(),
        }));

        let http_client = build_http_client(true, 100, &HashMap::new(), None);

        Ok(Arc::new(Self {
            hybrid_cache,
            rt,
            http_client: Arc::new(parking_lot::RwLock::new(http_client)),
            _cache_dir: dir,
            _cache_file: cache_file,
            storage_capacity,
            meta,
            total_used_bytes: Arc::new(AtomicU64::new(0)),
        }))
    }

    pub fn update_network_options(
        &self,
        domain_fronting_enabled: bool,
        split_delay_ms: u64,
        host_ips: HashMap<String, Vec<String>>,
        proxy_url: Option<String>,
    ) {
        let new_client = build_http_client(
            domain_fronting_enabled,
            split_delay_ms,
            &host_ips,
            proxy_url.as_deref(),
        );

        *self.http_client.write() = new_client;
    }

    pub fn set_proxy(&self, proxy_url: Option<String>) {
        self.update_network_options(true, 100, HashMap::new(), proxy_url);
    }

    pub fn get(&self, key: String) -> Option<Vec<u8>> {
        // Fast path: in-memory cache
        if let Some(entry) = self.hybrid_cache.memory().get(&key) {
            let mut meta = self.meta.write();
            if let Some(pos) = meta.lru_order.iter().position(|k| k == &key) {
                meta.lru_order.remove(pos);
            }
            meta.lru_order.push_back(key);
            return Some(entry.value().clone());
        }

        // Storage path
        let key_clone = key.clone();
        let cache = self.hybrid_cache.clone();
        let val = tokio::task::block_in_place(|| {
            self.rt.block_on(async {
                cache
                    .get(&key_clone)
                    .await
                    .ok()
                    .flatten()
                    .map(|e| e.value().clone())
            })
        });

        if let Some(ref data) = val {
            let mut meta = self.meta.write();
            if let Some(pos) = meta.lru_order.iter().position(|k| k == &key) {
                meta.lru_order.remove(pos);
            }
            meta.lru_order.push_back(key.clone());
            if !meta.entries.contains_key(&key) {
                meta.entries.insert(key, data.len());
                let aligned = ((data.len() + 7) & !7) as u64;
                self.total_used_bytes.fetch_add(aligned, Ordering::Relaxed);
            }
        }

        val
    }

    pub async fn get_async(&self, key: String) -> Option<Vec<u8>> {
        if let Some(entry) = self.hybrid_cache.memory().get(&key) {
            return Some(entry.value().clone());
        }

        self.hybrid_cache
            .get(&key)
            .await
            .ok()
            .flatten()
            .map(|e| e.value().clone())
    }

    pub fn put(&self, key: String, data: Vec<u8>) -> Result<(), CacheError> {
        let max_item_limit = self.storage_capacity.max(64 * 1024 * 1024);
        if data.len() as u64 > max_item_limit {
            return Err(CacheError::ItemTooLarge {
                size: data.len() as u64,
                limit: max_item_limit,
            });
        }

        let aligned_len = ((data.len() + 7) & !7) as u64;
        let mut meta = self.meta.write();

        if let Some(old_len) = meta.entries.insert(key.clone(), data.len()) {
            let old_aligned = ((old_len + 7) & !7) as u64;
            if aligned_len >= old_aligned {
                self.total_used_bytes
                    .fetch_add(aligned_len - old_aligned, Ordering::Relaxed);
            } else {
                self.total_used_bytes
                    .fetch_sub(old_aligned - aligned_len, Ordering::Relaxed);
            }
        } else {
            self.total_used_bytes
                .fetch_add(aligned_len, Ordering::Relaxed);
        }

        if let Some(pos) = meta.lru_order.iter().position(|k| k == &key) {
            meta.lru_order.remove(pos);
        }
        meta.lru_order.push_back(key.clone());

        self.hybrid_cache.insert(key, data);
        Ok(())
    }

    pub fn remove(&self, key: String) -> bool {
        let mut meta = self.meta.write();
        if let Some(old_len) = meta.entries.remove(&key) {
            let aligned = ((old_len + 7) & !7) as u64;
            self.total_used_bytes.fetch_sub(aligned, Ordering::Relaxed);
            if let Some(pos) = meta.lru_order.iter().position(|k| k == &key) {
                meta.lru_order.remove(pos);
            }
            self.hybrid_cache.remove(&key);
            true
        } else if self.hybrid_cache.contains(&key) {
            self.hybrid_cache.remove(&key);
            true
        } else {
            false
        }
    }

    pub fn clear(&self) -> Result<(), CacheError> {
        {
            let mut meta = self.meta.write();
            meta.entries.clear();
            meta.lru_order.clear();
            self.total_used_bytes.store(0, Ordering::Relaxed);
        }

        let cache = self.hybrid_cache.clone();
        tokio::task::block_in_place(|| {
            self.rt.block_on(async {
                let _ = cache.clear().await;
            });
        });

        Ok(())
    }

    pub fn purge_compact(&self) -> Result<u64, CacheError> {
        let mut meta = self.meta.write();
        let total = meta.lru_order.len();
        if total == 0 {
            return Ok(0);
        }

        let retain_count = (total / 2).max(1);
        let evict_count = total - retain_count;

        for _ in 0..evict_count {
            if let Some(old_key) = meta.lru_order.pop_front() {
                if let Some(len) = meta.entries.remove(&old_key) {
                    let aligned = ((len + 7) & !7) as u64;
                    self.total_used_bytes.fetch_sub(aligned, Ordering::Relaxed);
                }
                self.hybrid_cache.remove(&old_key);
            }
        }

        Ok(evict_count as u64)
    }

    pub fn purge_to_size(&self, max_bytes: u64) -> Result<u64, CacheError> {
        let mut meta = self.meta.write();
        let mut current_used = self.total_used_bytes.load(Ordering::Relaxed);
        if current_used <= max_bytes {
            return Ok(0);
        }

        let mut evict_count = 0u64;
        while current_used > max_bytes {
            if let Some(old_key) = meta.lru_order.pop_front() {
                if let Some(len) = meta.entries.remove(&old_key) {
                    let aligned = ((len + 7) & !7) as u64;
                    current_used = current_used.saturating_sub(aligned);
                    self.total_used_bytes.fetch_sub(aligned, Ordering::Relaxed);
                    evict_count += 1;
                }
                self.hybrid_cache.remove(&old_key);
            } else {
                break;
            }
        }

        Ok(evict_count)
    }

    pub fn stats(&self) -> CacheStats {
        let meta = self.meta.read();
        let entry_count = meta.entries.len() as u32;
        let total_used = self.total_used_bytes.load(Ordering::Relaxed);

        CacheStats {
            file_count: 1,
            entry_count,
            total_allocated_bytes: self.storage_capacity,
            total_used_bytes: total_used,
        }
    }

    // ==========================================
    // Planar RGBA & SIMD Zero-Copy Endpoints
    // ==========================================

    /// Puts an uncompressed RGBA pixel buffer by converting it to Planar RGBA + LZ4 and storing under `key + "#planar"`.
    pub fn put_planar_image(
        &self,
        key: String,
        width: u32,
        height: u32,
        rgba: Vec<u8>,
    ) -> Result<(), CacheError> {
        let encoded = encode_planar_lz4(width, height, &rgba)?;
        let planar_key = format!("{key}#planar");
        self.put(planar_key, encoded)
    }

    /// Retrieves and decompresses Planar RGBA into interleaved BGRA bytes (Avalonia Bgra8888).
    pub fn get_planar_image(&self, key: String) -> Option<PlanarImageResult> {
        let planar_key = if key.ends_with("#planar") {
            key
        } else {
            format!("{key}#planar")
        };

        let raw = self.get(planar_key)?;
        if !is_planar_lz4(&raw) {
            return None;
        }

        let (width, height, bgra_data) = decode_planar_lz4_to_bgra_vec(&raw).ok()?;
        Some(PlanarImageResult {
            width,
            height,
            bgra_data,
        })
    }

    /// Retrieves dimensions of a cached planar image without full decompression.
    pub fn get_planar_dimensions(&self, key: String) -> Option<PlanarImageInfo> {
        let planar_key = if key.ends_with("#planar") {
            key
        } else {
            format!("{key}#planar")
        };

        let raw = self.get(planar_key)?;
        if !is_planar_lz4(&raw) {
            return None;
        }

        let width = u32::from_le_bytes(raw[4..8].try_into().ok()?);
        let height = u32::from_le_bytes(raw[8..12].try_into().ok()?);
        Some(PlanarImageInfo { width, height })
    }

    /// Decodes a raw image (PNG/JPEG/WebP/GIF) in Rust, saves it as Planar LZ4, and returns BGRA pixels.
    pub fn decode_and_cache_planar(
        &self,
        key: String,
        raw_image_bytes: Vec<u8>,
    ) -> Result<PlanarImageResult, CacheError> {
        let img = image::load_from_memory(&raw_image_bytes).map_err(|e| CacheError::Codec {
            message: format!("Image decode failed: {e}"),
        })?;

        let rgba = img.to_rgba8();
        let width = rgba.width();
        let height = rgba.height();
        let rgba_bytes = rgba.into_raw();

        self.put_planar_image(key.clone(), width, height, rgba_bytes)?;

        self.get_planar_image(key).ok_or(CacheError::CorruptedData)
    }

    /// Decompresses a cached planar RGBA image directly into external memory (e.g. Avalonia WriteableBitmap framebuffer).
    /// `dst_ptr` is the pointer address passed as an integer (`u64`).
    pub fn decompress_planar_to_memory(
        &self,
        key: String,
        dst_ptr: u64,
        dst_len: u64,
    ) -> Result<PlanarImageInfo, CacheError> {
        if dst_ptr == 0 {
            return Err(CacheError::Io {
                message: "Destination pointer is null".to_string(),
            });
        }

        let planar_key = if key.ends_with("#planar") {
            key
        } else {
            format!("{key}#planar")
        };

        let data = self.get(planar_key).ok_or(CacheError::CorruptedData)?;
        let dst_slice =
            unsafe { std::slice::from_raw_parts_mut(dst_ptr as *mut u8, dst_len as usize) };
        let (width, height) = decode_planar_lz4_to_bgra(&data, dst_slice)?;

        Ok(PlanarImageInfo { width, height })
    }

    pub async fn get_or_fetch(
        &self,
        url: String,
        referer: Option<String>,
        callback: Option<Box<dyn CachePreviewCallback>>,
    ) -> Result<Vec<u8>, CacheError> {
        if url.trim().is_empty() {
            return Err(CacheError::Io {
                message: "Empty URL".to_string(),
            });
        }

        if let Some(data) = self.get(url.clone()) {
            if let Some(cb) = &callback {
                cb.on_progress(data.len() as u64, data.len() as u64);
            }
            return Ok(data);
        }

        if url.starts_with("file://")
            || (!url.starts_with("http://") && !url.starts_with("https://"))
        {
            let path_str = if let Some(stripped) = url.strip_prefix("file://") {
                stripped.trim_start_matches('/')
            } else {
                &url
            };
            let bytes = fs::read(path_str).map_err(|e| CacheError::Io {
                message: e.to_string(),
            })?;
            if let Some(cb) = &callback {
                cb.on_progress(bytes.len() as u64, bytes.len() as u64);
            }
            let _ = self.put(url, bytes.clone());
            return Ok(bytes);
        }

        let client = self.http_client.read().clone();
        let mut req = client.get(&url);

        if let Some(ref ref_str) = referer {
            req = req.header("Referer", ref_str);
        } else if url.contains("pximg.net") || url.contains("pixiv.net") {
            req = req.header("Referer", "https://app-api.pixiv.net/");
        }
        if url.contains("pximg.net") || url.contains("pixiv.net") {
            req = req.header("User-Agent", "PixivAndroidApp/6.140.2 (Android 15.0)");
        }

        let resp = req.send().await.map_err(|e| CacheError::Network {
            message: e.to_string(),
        })?;
        let status = resp.status();
        if !status.is_success() {
            return Err(CacheError::Http {
                code: status.as_u16(),
            });
        }

        let total_bytes = resp.content_length().unwrap_or(0);
        let mut stream = resp.bytes_stream();
        let mut buffer = Vec::new();
        let mut zip_sniffer = ZipSniffer::new();
        let mut image_previewed = false;
        let mut last_progress_report = std::time::Instant::now();

        while let Some(chunk_res) = stream.next().await {
            let chunk = chunk_res.map_err(|e| CacheError::Network {
                message: e.to_string(),
            })?;
            buffer.extend_from_slice(&chunk);

            if let Some(cb) = &callback {
                let now = std::time::Instant::now();
                if now.duration_since(last_progress_report) >= std::time::Duration::from_millis(100)
                {
                    last_progress_report = now;
                    cb.on_progress(buffer.len() as u64, total_bytes);
                }

                if let Some(frame) = zip_sniffer.sniff(&buffer) {
                    cb.on_preview_frame(frame);
                } else if !image_previewed && is_previewable_image_prefix(&buffer) {
                    if buffer.len() >= 64 * 1024
                        || (total_bytes > 0 && buffer.len() >= (total_bytes as usize) / 2)
                    {
                        image_previewed = true;
                        cb.on_preview_frame(buffer.clone());
                    }
                }
            }
        }

        if let Some(cb) = &callback {
            cb.on_progress(buffer.len() as u64, buffer.len() as u64);
        }

        let _ = self.put(url, buffer.clone());
        Ok(buffer)
    }
}

struct ZipSniffer {
    offset: usize,
    _emitted_count: usize,
}

impl ZipSniffer {
    fn new() -> Self {
        Self {
            offset: 0,
            _emitted_count: 0,
        }
    }

    fn sniff(&mut self, buffer: &[u8]) -> Option<Vec<u8>> {
        let mut latest_frame = None;
        while buffer.len().saturating_sub(self.offset) >= 30 {
            let header = &buffer[self.offset..self.offset + 30];
            if header[0..4] != [0x50, 0x4b, 0x03, 0x04] {
                break;
            }
            let flags = u16::from_le_bytes([header[6], header[7]]);
            let method = u16::from_le_bytes([header[8], header[9]]);
            let compressed_size =
                u32::from_le_bytes([header[18], header[19], header[20], header[21]]) as usize;
            let uncompressed_size =
                u32::from_le_bytes([header[22], header[23], header[24], header[25]]) as usize;
            let filename_len = u16::from_le_bytes([header[26], header[27]]) as usize;
            let extra_len = u16::from_le_bytes([header[28], header[29]]) as usize;

            if (flags & 9) != 0
                || (method != 0 && method != 8)
                || compressed_size > 8 * 1024 * 1024
                || uncompressed_size > 8 * 1024 * 1024
            {
                break;
            }

            let data_start = self.offset + 30 + filename_len + extra_len;
            let data_end = data_start + compressed_size;

            if buffer.len() < data_end {
                break;
            }

            self.offset = data_end;
            if uncompressed_size == 0 {
                continue;
            }

            let entry_compressed = &buffer[data_start..data_end];
            if method == 0 {
                latest_frame = Some(entry_compressed.to_vec());
                self._emitted_count += 1;
            } else if method == 8 {
                use std::io::Read;
                let mut decoder = flate2::read::DeflateDecoder::new(entry_compressed);
                let mut decompressed = Vec::with_capacity(uncompressed_size);
                if decoder.read_to_end(&mut decompressed).is_ok()
                    && decompressed.len() == uncompressed_size
                {
                    latest_frame = Some(decompressed);
                    self._emitted_count += 1;
                }
            }
        }
        latest_frame
    }
}

fn is_previewable_image_prefix(buf: &[u8]) -> bool {
    if buf.len() < 4 {
        return false;
    }
    // JPEG: 0xFF 0xD8
    if buf[0] == 0xFF && buf[1] == 0xD8 {
        return true;
    }
    // PNG: 0x89 'P' 'N' 'G'
    if buf[0] == 0x89 && buf[1] == 0x50 && buf[2] == 0x4E && buf[3] == 0x47 {
        return true;
    }
    // GIF: "GIF8"
    if buf[0..4] == *b"GIF8" {
        return true;
    }
    // WebP: "RIFF"
    if buf[0..4] == *b"RIFF" {
        return true;
    }
    false
}
