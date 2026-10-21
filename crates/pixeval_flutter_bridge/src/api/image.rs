use crate::state::APP_STATE;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct CacheStatsDto {
    pub entry_count: u32,
    pub total_allocated_bytes: u64,
    pub total_used_bytes: u64,
}

pub fn init_image_cache(cache_dir: String, capacity_bytes: Option<u64>) -> Result<(), String> {
    let cache = pixeval_cache::ImageCache::new(cache_dir, capacity_bytes)
        .map_err(|e| format!("Failed to initialize ImageCache: {e}"))?;

    // Apply current network configuration if present
    let config = APP_STATE.current_config.read().clone();
    cache.update_network_options(
        config.domain_fronting_enabled,
        config.split_delay_ms,
        std::collections::HashMap::new(),
        config.proxy_url,
    );

    let mut write = APP_STATE.image_cache.write();
    if let Some(old) = write.take() {
        old.close();
    }
    *write = Some(cache);
    Ok(())
}

pub async fn fetch_image(url: String, referer: Option<String>) -> Result<Vec<u8>, String> {
    let cache = APP_STATE.get_or_init_cache()?;
    cache
        .get_or_fetch(url, referer, None)
        .await
        .map_err(|e| format!("Image fetch failed: {e}"))
}

pub fn get_cached_image(url: String) -> Option<Vec<u8>> {
    let read = APP_STATE.image_cache.read();
    let cache = read.as_ref()?;
    cache.get(url)
}

pub fn clear_image_cache() -> Result<(), String> {
    let read = APP_STATE.image_cache.read();
    if let Some(cache) = read.as_ref() {
        cache.clear().map_err(|e| format!("Failed to clear image cache: {e}"))?;
    }
    Ok(())
}

pub fn get_cache_stats() -> CacheStatsDto {
    let read = APP_STATE.image_cache.read();
    if let Some(cache) = read.as_ref() {
        let stats = cache.stats();
        CacheStatsDto {
            entry_count: stats.entry_count,
            total_allocated_bytes: stats.total_allocated_bytes,
            total_used_bytes: stats.total_used_bytes,
        }
    } else {
        CacheStatsDto {
            entry_count: 0,
            total_allocated_bytes: 0,
            total_used_bytes: 0,
        }
    }
}
