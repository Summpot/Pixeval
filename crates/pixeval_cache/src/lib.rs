// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

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

    fn create_test_dir() -> tempfile::TempDir {
        let repo_tmp = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .parent()
            .and_then(|p| p.parent())
            .map(|p| p.join("target").join("tmp"))
            .unwrap_or_else(std::env::temp_dir);
        let _ = std::fs::create_dir_all(&repo_tmp);
        tempfile::Builder::new()
            .prefix("test_cache_")
            .tempdir_in(&repo_tmp)
            .unwrap_or_else(|_| {
                tempfile::Builder::new()
                    .prefix("test_cache_")
                    .tempdir()
                    .unwrap()
            })
    }

    #[test]
    fn test_cache_put_get_remove() {
        let dir = create_test_dir();
        let cache = CacheEngine::new(dir.path().to_string_lossy().to_string(), 8192, 5).unwrap();

        assert_eq!(cache.get("key1".to_string()), None);

        cache
            .put("key1".to_string(), b"hello world".to_vec())
            .unwrap();
        assert_eq!(cache.get("key1".to_string()), Some(b"hello world".to_vec()));

        let stats = cache.stats();
        assert_eq!(stats.entry_count, 1);
        // 11 bytes aligned to 8 is 16 bytes
        assert_eq!(stats.total_used_bytes, 16);

        assert!(cache.remove("key1".to_string()));
        assert_eq!(cache.get("key1".to_string()), None);
    }

    #[test]
    fn test_multi_chunk_expansion_and_compact() {
        let dir = create_test_dir();
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
        let dir = create_test_dir();
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
        let dir = create_test_dir();
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

    #[test]
    fn test_restart_persistence_and_index() {
        let dir = create_test_dir();
        let dir_path = dir.path().to_string_lossy().to_string();

        {
            let cache = CacheEngine::new(dir_path.clone(), 8192, 4).unwrap();
            cache.put("k1".to_string(), b"hello_persisted".to_vec()).unwrap();
            cache.put("k2".to_string(), b"second_entry".to_vec()).unwrap();
            assert_eq!(cache.get("k1".to_string()), Some(b"hello_persisted".to_vec()));
            assert_eq!(cache.get("k2".to_string()), Some(b"second_entry".to_vec()));
        } // Drop cache here

        {
            let cache2 = CacheEngine::new(dir_path, 8192, 4).unwrap();
            assert_eq!(cache2.get("k1".to_string()), Some(b"hello_persisted".to_vec()));
            assert_eq!(cache2.get("k2".to_string()), Some(b"second_entry".to_vec()));
            let stats = cache2.stats();
            assert_eq!(stats.entry_count, 2);
        }
    }

    #[test]
    fn test_item_too_large() {
        let dir = create_test_dir();
        let cache = CacheEngine::new(dir.path().to_string_lossy().to_string(), 64, 2).unwrap();
        let huge = vec![0u8; 100 * 1024 * 1024];
        let res = cache.put("huge".to_string(), huge);
        assert!(matches!(res, Err(CacheError::ItemTooLarge { .. })));
    }

    #[tokio::test]
    async fn test_get_or_fetch_local_file() {
        let dir = create_test_dir();
        let cache = CacheEngine::new(dir.path().to_string_lossy().to_string(), 8192, 4).unwrap();
        let file_path = dir.path().join("sample.txt");
        std::fs::write(&file_path, b"local file content").unwrap();

        let file_url = format!("file://{}", file_path.to_string_lossy());
        let fetched = cache.get_or_fetch(file_url.clone(), None, None).await.unwrap();
        assert_eq!(fetched, b"local file content");
        assert_eq!(cache.get(file_url), Some(b"local file content".to_vec()));
    }
}
