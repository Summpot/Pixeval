use futures_util::StreamExt;
use parking_lot::RwLock;
use pixeval_maho::{DnsResolver, MahoConfig, MahoProxyServer, IMAGE_HOST, IMAGE_HOST2};
use std::collections::{HashMap, VecDeque};
use std::fs;
use std::net::{IpAddr, SocketAddr};
use std::path::{Path, PathBuf};
use std::sync::Arc;

use crate::error::CacheError;
use crate::mmap_chunk::MmapChunk;

const INDEX_MAGIC: &[u8; 8] = b"PXCACHE1";
const INDEX_VERSION: u32 = 1;

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Record)]
pub struct CacheStats {
    pub file_count: u32,
    pub entry_count: u32,
    pub total_allocated_bytes: u64,
    pub total_used_bytes: u64,
}

#[uniffi::export(callback_interface)]
pub trait CachePreviewCallback: Send + Sync {
    fn on_preview_frame(&self, frame_data: Vec<u8>);
    fn on_progress(&self, downloaded_bytes: u64, total_bytes: u64);
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct CacheEntry {
    pub chunk_id: u32,
    pub offset: usize,
    pub len: usize,
}

struct CacheInner {
    cache_dir: PathBuf,
    initial_file_size: usize,
    max_files: u32,
    chunks: Vec<MmapChunk>,
    entries: HashMap<String, CacheEntry>,
    lru_order: VecDeque<String>,
    fragmented_bytes: usize,
}

fn save_index(
    dir: &Path,
    entries: &HashMap<String, CacheEntry>,
    lru_order: &VecDeque<String>,
) -> Result<(), CacheError> {
    let tmp_path = dir.join("cache_index.bin.tmp");
    let final_path = dir.join("cache_index.bin");

    let mut buf = Vec::new();
    buf.extend_from_slice(INDEX_MAGIC);
    buf.extend_from_slice(&INDEX_VERSION.to_le_bytes());
    buf.extend_from_slice(&(entries.len() as u32).to_le_bytes());

    for (key, entry) in entries {
        let key_bytes = key.as_bytes();
        let key_len = key_bytes.len().min(u16::MAX as usize) as u16;
        buf.extend_from_slice(&key_len.to_le_bytes());
        buf.extend_from_slice(&key_bytes[..key_len as usize]);
        buf.extend_from_slice(&entry.chunk_id.to_le_bytes());
        buf.extend_from_slice(&(entry.offset as u64).to_le_bytes());
        buf.extend_from_slice(&(entry.len as u64).to_le_bytes());
    }

    buf.extend_from_slice(&(lru_order.len() as u32).to_le_bytes());
    for key in lru_order {
        let key_bytes = key.as_bytes();
        let key_len = key_bytes.len().min(u16::MAX as usize) as u16;
        buf.extend_from_slice(&key_len.to_le_bytes());
        buf.extend_from_slice(&key_bytes[..key_len as usize]);
    }

    fs::write(&tmp_path, &buf)?;
    fs::rename(&tmp_path, &final_path)?;
    Ok(())
}

fn load_index(dir: &Path) -> Option<(HashMap<String, CacheEntry>, VecDeque<String>)> {
    let path = dir.join("cache_index.bin");
    let bytes = fs::read(&path).ok()?;
    if bytes.len() < 16 {
        return None;
    }
    if &bytes[0..8] != INDEX_MAGIC {
        return None;
    }
    let version = u32::from_le_bytes(bytes[8..12].try_into().ok()?);
    if version != INDEX_VERSION {
        return None;
    }

    let entry_count = u32::from_le_bytes(bytes[12..16].try_into().ok()?) as usize;
    let mut offset = 16;
    let mut entries = HashMap::with_capacity(entry_count);

    for _ in 0..entry_count {
        if offset + 2 > bytes.len() {
            return None;
        }
        let key_len = u16::from_le_bytes(bytes[offset..offset + 2].try_into().ok()?) as usize;
        offset += 2;

        if offset + key_len + 4 + 8 + 8 > bytes.len() {
            return None;
        }
        let key = String::from_utf8(bytes[offset..offset + key_len].to_vec()).ok()?;
        offset += key_len;

        let chunk_id = u32::from_le_bytes(bytes[offset..offset + 4].try_into().ok()?);
        offset += 4;
        let entry_offset = u64::from_le_bytes(bytes[offset..offset + 8].try_into().ok()?) as usize;
        offset += 8;
        let len = u64::from_le_bytes(bytes[offset..offset + 8].try_into().ok()?) as usize;
        offset += 8;

        entries.insert(
            key,
            CacheEntry {
                chunk_id,
                offset: entry_offset,
                len,
            },
        );
    }

    let mut lru_order = VecDeque::new();
    if offset + 4 <= bytes.len() {
        let lru_count = u32::from_le_bytes(bytes[offset..offset + 4].try_into().ok()?) as usize;
        offset += 4;
        for _ in 0..lru_count {
            if offset + 2 > bytes.len() {
                break;
            }
            let key_len = u16::from_le_bytes(bytes[offset..offset + 2].try_into().ok()?) as usize;
            offset += 2;
            if offset + key_len > bytes.len() {
                break;
            }
            if let Ok(key) = String::from_utf8(bytes[offset..offset + key_len].to_vec()) {
                if entries.contains_key(&key) {
                    lru_order.push_back(key);
                }
            }
            offset += key_len;
        }
    }

    for key in entries.keys() {
        if !lru_order.contains(key) {
            lru_order.push_back(key.clone());
        }
    }

    Some((entries, lru_order))
}

fn build_http_client(
    domain_fronting_enabled: bool,
    host_ips: &HashMap<String, Vec<String>>,
    proxy_url: Option<&str>,
    maho_proxy_url: Option<&str>,
) -> reqwest::Client {
    let mut builder = reqwest::Client::builder()
        .user_agent("PixivAndroidApp/6.140.2 (Android 15.0)")
        .connect_timeout(std::time::Duration::from_secs(10))
        .timeout(std::time::Duration::from_secs(30));

    if let Some(proxy_str) = proxy_url {
        let trimmed = proxy_str.trim();
        if !trimmed.is_empty() {
            if let Ok(proxy) = reqwest::Proxy::all(trimmed) {
                builder = builder.proxy(proxy);
            }
        }
    } else if let Some(m_proxy) = maho_proxy_url {
        if let Ok(proxy) = reqwest::Proxy::all(m_proxy) {
            builder = builder.proxy(proxy);
        }
    }

    if domain_fronting_enabled {
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

        for host in [IMAGE_HOST, IMAGE_HOST2] {
            if let Some(ips) = resolver.get_static_ips(host) {
                let addrs: Vec<SocketAddr> =
                    ips.into_iter().map(|ip| SocketAddr::new(ip, 443)).collect();
                if !addrs.is_empty() {
                    builder = builder.resolve_to_addrs(host, &addrs);
                }
            }
        }
    }

    builder.build().unwrap_or_default()
}

#[derive(uniffi::Object)]
pub struct CacheEngine {
    inner: Arc<RwLock<CacheInner>>,
    http_client: Arc<parking_lot::RwLock<reqwest::Client>>,
    maho_proxy: Arc<parking_lot::RwLock<Option<Arc<MahoProxyServer>>>>,
}

#[uniffi::export(async_runtime = "tokio")]
impl CacheEngine {
    #[uniffi::constructor]
    pub fn new(
        cache_dir: String,
        initial_file_size: u64,
        max_files: u32,
    ) -> Result<Arc<Self>, CacheError> {
        let dir = PathBuf::from(cache_dir);
        fs::create_dir_all(&dir)?;

        // Clean up legacy *.cache files if present
        if let Ok(entries) = fs::read_dir(&dir) {
            for entry in entries.flatten() {
                if let Some(ext) = entry.path().extension() {
                    if ext == "cache" {
                        let _ = fs::remove_file(entry.path());
                    }
                }
            }
        }

        let initial_size = initial_file_size.max(64) as usize;
        let max_files = max_files.max(1);

        let mut chunks = Vec::new();
        let mut entries = HashMap::new();
        let mut lru_order = VecDeque::new();

        let loaded_index = load_index(&dir);
        let mut existing_chunk_ids: Vec<u32> = Vec::new();
        if let Ok(dir_entries) = fs::read_dir(&dir) {
            for entry in dir_entries.flatten() {
                let file_name = entry.file_name().to_string_lossy().to_string();
                if file_name.starts_with("cache_chunk_") && file_name.ends_with(".bin") {
                    let num_part = &file_name["cache_chunk_".len()..file_name.len() - ".bin".len()];
                    if let Ok(id) = num_part.parse::<u32>() {
                        existing_chunk_ids.push(id);
                    }
                }
            }
        }
        existing_chunk_ids.sort();

        if let Some((idx_entries, idx_lru)) = loaded_index {
            for &chunk_id in &existing_chunk_ids {
                if let Ok(chunk) = MmapChunk::open_or_create(&dir, chunk_id, initial_size) {
                    chunks.push(chunk);
                }
            }
            if chunks.is_empty() {
                chunks.push(MmapChunk::open_or_create(&dir, 0, initial_size)?);
            }
            entries = idx_entries;
            lru_order = idx_lru;
            for chunk in &mut chunks {
                let mut max_end = 0;
                for entry in entries.values() {
                    if entry.chunk_id == chunk.id {
                        let aligned = (entry.len + 7) & !7;
                        max_end = max_end.max(entry.offset + aligned);
                    }
                }
                chunk.bump_offset = max_end;
            }
        } else {
            chunks.push(MmapChunk::open_or_create(&dir, 0, initial_size)?);
        }

        let inner = CacheInner {
            cache_dir: dir,
            initial_file_size: initial_size,
            max_files,
            chunks,
            entries,
            lru_order,
            fragmented_bytes: 0,
        };

        let maho_config = Arc::new(MahoConfig::default());
        let maho_proxy: Option<Arc<MahoProxyServer>> = MahoProxyServer::start_sync(maho_config)
            .ok()
            .map(Arc::new);
        let maho_proxy_url = maho_proxy.as_ref().map(|p| p.proxy_url());
        let http_client = build_http_client(true, &HashMap::new(), None, maho_proxy_url.as_deref());

        Ok(Arc::new(Self {
            inner: Arc::new(RwLock::new(inner)),
            http_client: Arc::new(parking_lot::RwLock::new(http_client)),
            maho_proxy: Arc::new(parking_lot::RwLock::new(maho_proxy)),
        }))
    }

    pub fn update_network_options(
        &self,
        domain_fronting_enabled: bool,
        split_delay_ms: u64,
        host_ips: HashMap<String, Vec<String>>,
        proxy_url: Option<String>,
    ) {
        let resolver = DnsResolver::new();
        for (host, ips) in &host_ips {
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

        let maho_proxy: Option<Arc<MahoProxyServer>> = if domain_fronting_enabled
            && proxy_url.as_deref().unwrap_or("").trim().is_empty()
        {
            MahoProxyServer::start_sync(maho_config)
                .ok()
                .map(Arc::new)
        } else {
            None
        };
        let maho_proxy_url = maho_proxy.as_ref().map(|p| p.proxy_url());

        let new_client = build_http_client(
            domain_fronting_enabled,
            &host_ips,
            proxy_url.as_deref(),
            maho_proxy_url.as_deref(),
        );

        *self.http_client.write() = new_client;
        *self.maho_proxy.write() = maho_proxy;
    }

    pub fn set_proxy(&self, proxy_url: Option<String>) {
        self.update_network_options(true, 100, HashMap::new(), proxy_url);
    }

    pub fn get(&self, key: String) -> Option<Vec<u8>> {
        let mut inner = self.inner.write();
        let entry = *inner.entries.get(&key)?;

        if let Some(pos) = inner.lru_order.iter().position(|k| k == &key) {
            inner.lru_order.remove(pos);
        }
        inner.lru_order.push_back(key);

        let chunk = inner.chunks.iter().find(|c| c.id == entry.chunk_id)?;
        let slice = chunk.read(entry.offset, entry.len)?;
        Some(slice.to_vec())
    }

    pub fn put(&self, key: String, data: Vec<u8>) -> Result<(), CacheError> {
        let mut inner = self.inner.write();

        let max_item_limit = (inner.initial_file_size as u64)
            .max((inner.initial_file_size as u64) * (inner.max_files as u64).min(4))
            .max(64 * 1024 * 1024);
        if data.len() as u64 > max_item_limit {
            return Err(CacheError::ItemTooLarge {
                size: data.len() as u64,
                limit: max_item_limit,
            });
        }

        let aligned_len = (data.len() + 7) & !7;

        // If key already exists and can fit into previous slot, overwrite in place
        if let Some(existing) = inner.entries.get(&key).copied() {
            let existing_aligned = (existing.len + 7) & !7;
            if aligned_len <= existing_aligned {
                if let Some(chunk) = inner.chunks.iter_mut().find(|c| c.id == existing.chunk_id) {
                    chunk.write_at(existing.offset, &data)?;
                    let entry = inner.entries.get_mut(&key).unwrap();
                    entry.len = data.len();
                    if let Some(pos) = inner.lru_order.iter().position(|k| k == &key) {
                        inner.lru_order.remove(pos);
                    }
                    inner.lru_order.push_back(key);
                    let _ = save_index(&inner.cache_dir, &inner.entries, &inner.lru_order);
                    return Ok(());
                }
            } else {
                // Key size expanded, old slot becomes fragmented dead space
                inner.fragmented_bytes += existing_aligned;
            }
        }

        // Try to allocate in any existing chunk
        let mut allocated = None;
        for chunk in &mut inner.chunks {
            if chunk.can_allocate(aligned_len) {
                let offset = chunk.allocate(&data)?;
                allocated = Some((chunk.id, offset));
                break;
            }
        }

        let (chunk_id, offset) = if let Some(alloc) = allocated {
            alloc
        } else if (inner.chunks.len() as u32) < inner.max_files {
            let new_id = inner.chunks.len() as u32;
            let chunk_size = inner.initial_file_size.max(aligned_len);
            let mut new_chunk = MmapChunk::open_or_create(&inner.cache_dir, new_id, chunk_size)?;
            let offset = new_chunk.allocate(&data)?;
            let chunk_id = new_chunk.id;
            inner.chunks.push(new_chunk);
            (chunk_id, offset)
        } else if inner.fragmented_bytes > 0 {
            // Memory is fragmented with abandoned slots: defragment without evicting
            defragment_inner(&mut inner)?;
            let mut reallocated = None;
            for chunk in &mut inner.chunks {
                if chunk.can_allocate(aligned_len) {
                    let offset = chunk.allocate(&data)?;
                    reallocated = Some((chunk.id, offset));
                    break;
                }
            }
            if let Some(alloc) = reallocated {
                alloc
            } else if (inner.chunks.len() as u32) < inner.max_files {
                let new_id = inner.chunks.len() as u32;
                let chunk_size = inner.initial_file_size.max(aligned_len);
                let mut new_chunk = MmapChunk::open_or_create(&inner.cache_dir, new_id, chunk_size)?;
                let offset = new_chunk.allocate(&data)?;
                let chunk_id = new_chunk.id;
                inner.chunks.push(new_chunk);
                (chunk_id, offset)
            } else {
                return Err(CacheError::OutOfMemory);
            }
        } else {
            return Err(CacheError::OutOfMemory);
        };

        if inner
            .entries
            .insert(
                key.clone(),
                CacheEntry {
                    chunk_id,
                    offset,
                    len: data.len(),
                },
            )
            .is_some()
            && let Some(pos) = inner.lru_order.iter().position(|k| k == &key)
        {
            inner.lru_order.remove(pos);
        }
        inner.lru_order.push_back(key);

        let _ = save_index(&inner.cache_dir, &inner.entries, &inner.lru_order);
        Ok(())
    }

    pub fn remove(&self, key: String) -> bool {
        let mut inner = self.inner.write();
        if let Some(entry) = inner.entries.remove(&key) {
            let aligned = (entry.len + 7) & !7;
            inner.fragmented_bytes += aligned;
            if let Some(pos) = inner.lru_order.iter().position(|k| k == &key) {
                inner.lru_order.remove(pos);
            }
            let _ = save_index(&inner.cache_dir, &inner.entries, &inner.lru_order);
            true
        } else {
            false
        }
    }

    pub fn clear(&self) -> Result<(), CacheError> {
        let mut inner = self.inner.write();
        inner.entries.clear();
        inner.lru_order.clear();
        inner.fragmented_bytes = 0;

        let chunk_paths: Vec<PathBuf> = inner.chunks.iter().map(|c| c.path.clone()).collect();
        inner.chunks.clear();
        for path in chunk_paths {
            let _ = fs::remove_file(&path);
        }

        let idx_path = inner.cache_dir.join("cache_index.bin");
        let _ = fs::remove_file(&idx_path);
        let tmp_idx = inner.cache_dir.join("cache_index.bin.tmp");
        let _ = fs::remove_file(&tmp_idx);

        if let Ok(entries) = fs::read_dir(&inner.cache_dir) {
            for entry in entries.flatten() {
                if let Some(ext) = entry.path().extension() {
                    if ext == "cache" {
                        let _ = fs::remove_file(entry.path());
                    }
                }
            }
        }

        let first_chunk = MmapChunk::open_or_create(&inner.cache_dir, 0, inner.initial_file_size)?;
        inner.chunks.push(first_chunk);
        Ok(())
    }

    pub fn purge_compact(&self) -> Result<u64, CacheError> {
        let mut inner = self.inner.write();
        let total_entries = inner.lru_order.len();
        if total_entries == 0 {
            return Ok(0);
        }

        let retain_count = (total_entries / 2).max(1);
        let evict_count = total_entries - retain_count;

        for _ in 0..evict_count {
            if let Some(old_key) = inner.lru_order.pop_front() {
                inner.entries.remove(&old_key);
            }
        }

        defragment_inner(&mut inner)?;
        Ok(evict_count as u64)
    }

    pub fn purge_to_size(&self, max_bytes: u64) -> Result<u64, CacheError> {
        let mut inner = self.inner.write();
        let current_used: u64 = inner
            .entries
            .values()
            .map(|e| ((e.len + 7) & !7) as u64)
            .sum();
        if current_used <= max_bytes {
            return Ok(0);
        }

        let mut target_used = current_used;
        let mut evict_count = 0u64;

        while target_used > max_bytes {
            if let Some(old_key) = inner.lru_order.pop_front() {
                if let Some(entry) = inner.entries.remove(&old_key) {
                    let aligned = ((entry.len + 7) & !7) as u64;
                    target_used = target_used.saturating_sub(aligned);
                    evict_count += 1;
                }
            } else {
                break;
            }
        }

        if evict_count == 0 {
            return Ok(0);
        }

        defragment_inner(&mut inner)?;
        Ok(evict_count)
    }

    pub fn stats(&self) -> CacheStats {
        let inner = self.inner.read();
        let file_count = inner.chunks.len() as u32;
        let entry_count = inner.entries.len() as u32;
        let total_allocated = inner.chunks.iter().map(|c| c.capacity as u64).sum();
        let total_used = inner
            .entries
            .values()
            .map(|e| ((e.len + 7) & !7) as u64)
            .sum();

        CacheStats {
            file_count,
            entry_count,
            total_allocated_bytes: total_allocated,
            total_used_bytes: total_used,
        }
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
            let bytes =
                fs::read(path_str).map_err(|e| CacheError::Io { message: e.to_string() })?;
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

        let resp = req
            .send()
            .await
            .map_err(|e| CacheError::Network { message: e.to_string() })?;
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
            let chunk = chunk_res.map_err(|e| CacheError::Network { message: e.to_string() })?;
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

impl Drop for CacheEngine {
    fn drop(&mut self) {
        let inner = self.inner.read();
        let _ = save_index(&inner.cache_dir, &inner.entries, &inner.lru_order);
    }
}

fn defragment_inner(inner: &mut CacheInner) -> Result<(), CacheError> {
    let mut active_data: Vec<(String, Vec<u8>)> = Vec::new();
    for key in &inner.lru_order {
        if let Some(entry) = inner.entries.get(key)
            && let Some(chunk) = inner.chunks.iter().find(|c| c.id == entry.chunk_id)
            && let Some(slice) = chunk.read(entry.offset, entry.len)
        {
            active_data.push((key.clone(), slice.to_vec()));
        }
    }

    let chunk_paths: Vec<PathBuf> = inner.chunks.iter().map(|c| c.path.clone()).collect();
    inner.chunks.clear();
    for path in chunk_paths {
        let _ = fs::remove_file(&path);
    }

    inner.entries.clear();
    inner.lru_order.clear();
    inner.fragmented_bytes = 0;

    let mut current_chunk =
        MmapChunk::open_or_create(&inner.cache_dir, 0, inner.initial_file_size)?;
    for (key, data) in active_data {
        let aligned = (data.len() + 7) & !7;
        if !current_chunk.can_allocate(aligned) {
            let new_id = inner.chunks.len() as u32 + 1;
            let chunk_size = inner.initial_file_size.max(aligned);
            let prev = std::mem::replace(
                &mut current_chunk,
                MmapChunk::open_or_create(&inner.cache_dir, new_id, chunk_size)?,
            );
            inner.chunks.push(prev);
        }
        let offset = current_chunk.allocate(&data)?;
        let chunk_id = current_chunk.id;
        inner.entries.insert(
            key.clone(),
            CacheEntry {
                chunk_id,
                offset,
                len: data.len(),
            },
        );
        inner.lru_order.push_back(key);
    }
    inner.chunks.push(current_chunk);
    let _ = save_index(&inner.cache_dir, &inner.entries, &inner.lru_order);

    Ok(())
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
