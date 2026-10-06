uniffi::setup_scaffolding!();

pub mod engine;
pub mod error;
pub mod mmap_chunk;

pub use engine::*;
pub use error::*;
pub use mmap_chunk::*;

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::tempdir;

    #[test]
    fn test_cache_put_get_remove() {
        let dir = tempdir().unwrap();
        let cache = CacheEngine::new(dir.path().to_string_lossy().to_string(), 8192, 5).unwrap();

        assert_eq!(cache.get("key1".to_string()), None);

        cache
            .put("key1".to_string(), b"hello world".to_vec())
            .unwrap();
        assert_eq!(cache.get("key1".to_string()), Some(b"hello world".to_vec()));

        let stats = cache.stats();
        assert_eq!(stats.entry_count, 1);
        assert_eq!(stats.total_used_bytes, 11);

        assert!(cache.remove("key1".to_string()));
        assert_eq!(cache.get("key1".to_string()), None);
    }

    #[test]
    fn test_multi_chunk_expansion_and_compact() {
        let dir = tempdir().unwrap();
        // small initial file size to trigger chunk expansion
        let cache = CacheEngine::new(dir.path().to_string_lossy().to_string(), 64, 4).unwrap();

        let data1 = vec![1u8; 32];
        let data2 = vec![2u8; 32];
        let data3 = vec![3u8; 32];

        cache.put("k1".to_string(), data1.clone()).unwrap();
        cache.put("k2".to_string(), data2.clone()).unwrap();
        cache.put("k3".to_string(), data3.clone()).unwrap();

        let stats = cache.stats();
        assert!(stats.file_count >= 2);

        assert_eq!(cache.get("k1".to_string()), Some(data1));
        assert_eq!(cache.get("k2".to_string()), Some(data2));
        assert_eq!(cache.get("k3".to_string()), Some(data3));

        // Purge compact
        let evicted = cache.purge_compact().unwrap();
        assert!(evicted >= 1);

        // Remaining entries still readable
        let stats_after = cache.stats();
        assert!(stats_after.entry_count < 3);
    }

    #[test]
    fn test_put_overwrite_in_place() {
        let dir = tempdir().unwrap();
        let cache = CacheEngine::new(dir.path().to_string_lossy().to_string(), 8192, 4).unwrap();

        let initial_data = vec![1u8; 32];
        cache.put("k1".to_string(), initial_data).unwrap();

        let stats1 = cache.stats();
        assert_eq!(stats1.entry_count, 1);
        assert_eq!(stats1.total_used_bytes, 32);

        // Put same key with smaller data: should overwrite in place without creating new chunks
        let new_data = vec![2u8; 16];
        cache.put("k1".to_string(), new_data.clone()).unwrap();

        let stats2 = cache.stats();
        assert_eq!(stats2.entry_count, 1);
        assert_eq!(stats2.total_used_bytes, 16);
        assert_eq!(cache.get("k1".to_string()), Some(new_data));
    }

    #[test]
    fn test_purge_to_size() {
        let dir = tempdir().unwrap();
        let cache = CacheEngine::new(dir.path().to_string_lossy().to_string(), 128, 4).unwrap();

        let d1 = vec![1u8; 32];
        let d2 = vec![2u8; 32];
        let d3 = vec![3u8; 32];

        cache.put("k1".to_string(), d1).unwrap();
        cache.put("k2".to_string(), d2).unwrap();
        cache.put("k3".to_string(), d3.clone()).unwrap();

        let stats = cache.stats();
        assert_eq!(stats.entry_count, 3);
        assert_eq!(stats.total_used_bytes, 96);

        // Target size 40 bytes: k1 and k2 (oldest) should be evicted, k3 retained
        let evicted = cache.purge_to_size(40).unwrap();
        assert_eq!(evicted, 2);

        let stats_after = cache.stats();
        assert_eq!(stats_after.entry_count, 1);
        assert_eq!(stats_after.total_used_bytes, 32);
        assert_eq!(cache.get("k1".to_string()), None);
        assert_eq!(cache.get("k2".to_string()), None);
        assert_eq!(cache.get("k3".to_string()), Some(d3));
    }
}
