// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

uniffi::setup_scaffolding!();

pub mod engine;
pub mod error;
pub mod planar;
pub mod simd;

pub use engine::*;
pub use error::*;
pub use planar::*;
pub use simd::*;

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
    fn test_foyer_persistence_across_restart() {
        let dir = create_test_dir();
        let dir_str = dir.path().to_string_lossy().to_string();

        {
            let cache1 = CacheEngine::new(dir_str.clone(), 8192, 4).unwrap();
            cache1.put("k_persist".to_string(), b"persisted_payload".to_vec()).unwrap();
            assert_eq!(cache1.get("k_persist".to_string()), Some(b"persisted_payload".to_vec()));
        }

        {
            let cache2 = CacheEngine::new(dir_str, 8192, 4).unwrap();
            let retrieved = cache2.get("k_persist".to_string());
            assert_eq!(retrieved, Some(b"persisted_payload".to_vec()));
        }
    }

    #[test]
    fn test_planar_rgba_and_simd() {
        let width = 64;
        let height = 64;
        let total_pixels = (width * height) as usize;
        let raw_len = total_pixels * 4;

        let mut rgba = vec![0u8; raw_len];
        for i in 0..total_pixels {
            rgba[i * 4] = (i % 256) as u8;
            rgba[i * 4 + 1] = ((i * 2) % 256) as u8;
            rgba[i * 4 + 2] = ((i * 3) % 256) as u8;
            rgba[i * 4 + 3] = 255;
        }

        let encoded = encode_planar_lz4(width, height, &rgba).unwrap();
        assert!(is_planar_lz4(&encoded));

        let mut bgra = vec![0u8; raw_len];
        let (w, h) = decode_planar_lz4_to_bgra(&encoded, &mut bgra).unwrap();
        assert_eq!(w, width);
        assert_eq!(h, height);

        // Verify that B, G, R, A are correctly transformed
        for i in 0..total_pixels {
            assert_eq!(bgra[i * 4], rgba[i * 4 + 2]);     // B == original B
            assert_eq!(bgra[i * 4 + 1], rgba[i * 4 + 1]); // G == original G
            assert_eq!(bgra[i * 4 + 2], rgba[i * 4]);     // R == original R
            assert_eq!(bgra[i * 4 + 3], 255);             // A == original A
        }
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

        let evicted = cache.purge_to_size(40).unwrap();
        assert_eq!(evicted, 2);

        let stats_after = cache.stats();
        assert_eq!(stats_after.entry_count, 1);
        assert_eq!(stats_after.total_used_bytes, 32);
        assert_eq!(cache.get("k3".to_string()), Some(d3));
    }
}
