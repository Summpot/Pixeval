// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use futures_util::StreamExt;
use pixeval_maho::{DnsResolver, IMAGE_HOST, IMAGE_HOST2, MahoConfig, MahoHttpClient};
use std::collections::HashMap;
use std::fs;
use std::net::IpAddr;
use std::path::PathBuf;
use std::sync::Arc;

use foyer::{
    BlockEngineConfig, DeviceBuilder, FileDeviceBuilder, HybridCache, HybridCacheBuilder,
    HybridCachePolicy, PsyncIoEngineConfig,
};
use foyer_common::properties::Properties;

use crate::error::CacheError;
use crate::planar::{
    decode_planar_lz4_to_bgra, decode_planar_lz4_to_bgra_vec, encode_planar_lz4, is_planar_lz4,
};

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Record)]
pub struct CacheStats {
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
    fn on_preview_frame(&self, frame: crate::progressive::DecodedPreviewFrame);
    fn on_progress(&self, downloaded_bytes: u64, total_bytes: u64);
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
    pub(crate) hybrid_cache: HybridCache<String, Vec<u8>>,
    pub(crate) rt: Arc<tokio::runtime::Runtime>,
    http_client: Arc<parking_lot::RwLock<MahoHttpClient>>,
    storage_capacity: u64,
}

#[uniffi::export(async_runtime = "tokio")]
impl CacheEngine {
    #[uniffi::constructor]
    pub fn new(
        cache_dir: String,
        capacity: Option<u64>,
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

        let storage_capacity = match capacity {
            Some(cap) if cap > 0 => cap,
            _ => 2 * 1024 * 1024 * 1024,
        }
        .max(64 * 1024)
        .min(64 * 1024 * 1024 * 1024);

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
        let rt_clone = rt.clone();
        let block_size = ((storage_capacity / 16) as usize).clamp(16 * 1024, 16 * 1024 * 1024);
        let block_size = (block_size + 4095) & !4095;

        let hybrid_cache = std::thread::spawn(move || {
            rt_clone.block_on(async {
                HybridCacheBuilder::new()
                    .with_name("pixeval_foyer")
                    .with_policy(HybridCachePolicy::WriteOnEviction)
                    .with_flush_on_close(true)
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
                        .with_block_size(block_size),
                    )
                    .with_recover_mode(foyer::RecoverMode::Quiet)
                    .build()
                    .await
                    .map_err(|e| CacheError::Io {
                        message: format!("Failed to build foyer HybridCache: {e}"),
                    })
            })
        })
        .join()
        .map_err(|_| CacheError::Io {
            message: "Cache initialization thread panicked".to_string(),
        })??;

        let http_client = build_http_client(true, 100, &HashMap::new(), None);

        Ok(Arc::new(Self {
            hybrid_cache,
            rt,
            http_client: Arc::new(parking_lot::RwLock::new(http_client)),
            storage_capacity,
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
            return Some(entry.value().clone());
        }

        // Storage path
        let key_clone = key.clone();
        let cache = self.hybrid_cache.clone();
        self.block_on(async {
            cache
                .get(&key_clone)
                .await
                .ok()
                .flatten()
                .map(|e| e.value().clone())
        })
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

        self.hybrid_cache.insert_with_properties(
            key,
            data,
            foyer::HybridCacheProperties::default().with_age(foyer::Age::Fresh),
        );
        Ok(())
    }

    pub fn remove(&self, key: String) -> bool {
        if self.hybrid_cache.contains(&key) {
            self.hybrid_cache.remove(&key);
            true
        } else {
            false
        }
    }

    pub fn close(&self) {
        let cache = self.hybrid_cache.clone();
        self.block_on(async move {
            let _ = cache.close().await;
        });
    }

    pub fn clear(&self) -> Result<(), CacheError> {
        let cache = self.hybrid_cache.clone();
        self.block_on(async {
            let _ = cache.clear().await;
        });

        Ok(())
    }

    pub fn stats(&self) -> CacheStats {
        CacheStats {
            entry_count: self.hybrid_cache.memory().entries() as u32,
            total_allocated_bytes: self.storage_capacity,
            total_used_bytes: self.hybrid_cache.memory().usage() as u64,
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
        async_compat::Compat::new(async move {
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
            req = req.header("User-Agent", "PixivAndroidApp/6.199.0 (Android 15.0; Pixel 8)");
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
        let mut progressive_decoder = crate::progressive::ProgressiveDecoder::new(None);
        let mut last_progress_report = std::time::Instant::now();
        let mut last_preview_report = std::time::Instant::now();

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

                if now.duration_since(last_preview_report) >= std::time::Duration::from_millis(250) {
                    if let Some(frame) = progressive_decoder.decode(&buffer) {
                        last_preview_report = now;
                        cb.on_preview_frame(frame);
                    }
                }
            }
        }

        if let Some(cb) = &callback {
            cb.on_progress(buffer.len() as u64, buffer.len() as u64);
        }

        let _ = self.put(url, buffer.clone());
        Ok(buffer)
        })
        .await
    }
}


impl Drop for CacheEngine {
    fn drop(&mut self) {
        self.close();
    }
}

impl CacheEngine {
    fn block_on<F, R>(&self, f: F) -> R
    where
        F: std::future::Future<Output = R>,
    {
        if tokio::runtime::Handle::try_current().is_ok() {
            tokio::task::block_in_place(|| {
                tokio::runtime::Handle::current().block_on(f)
            })
        } else {
            self.rt.block_on(f)
        }
    }
}
