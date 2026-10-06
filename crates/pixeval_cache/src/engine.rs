use parking_lot::RwLock;
use std::collections::{HashMap, VecDeque};
use std::fs;
use std::path::PathBuf;
use std::sync::Arc;

use crate::error::CacheError;
use crate::mmap_chunk::MmapChunk;

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Record)]
pub struct CacheStats {
    pub file_count: u32,
    pub entry_count: u32,
    pub total_allocated_bytes: u64,
    pub total_used_bytes: u64,
}

#[derive(Debug, Clone, Copy)]
struct CacheEntry {
    chunk_id: u32,
    offset: usize,
    len: usize,
}

struct CacheInner {
    cache_dir: PathBuf,
    initial_file_size: usize,
    max_files: u32,
    chunks: Vec<MmapChunk>,
    entries: HashMap<String, CacheEntry>,
    lru_order: VecDeque<String>,
}

#[derive(uniffi::Object)]
pub struct CacheEngine {
    inner: Arc<RwLock<CacheInner>>,
}

#[uniffi::export]
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
        let first_chunk = MmapChunk::create(&dir, 0, initial_size)?;

        let inner = CacheInner {
            cache_dir: dir,
            initial_file_size: initial_size,
            max_files: max_files.max(1),
            chunks: vec![first_chunk],
            entries: HashMap::new(),
            lru_order: VecDeque::new(),
        };

        Ok(Arc::new(Self {
            inner: Arc::new(RwLock::new(inner)),
        }))
    }

    pub fn get(&self, key: String) -> Option<Vec<u8>> {
        let mut inner = self.inner.write();
        let entry = *inner.entries.get(&key)?;

        // update LRU order
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
        let aligned_len = (data.len() + 7) & !7;

        // 6.7: If key already exists and can fit into previous slot, overwrite in place
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
                    return Ok(());
                }
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
        } else {
            // Allocate new chunk if below max_files
            if (inner.chunks.len() as u32) < inner.max_files {
                let new_id = inner.chunks.len() as u32;
                let chunk_size = inner.initial_file_size.max(aligned_len);
                let mut new_chunk = MmapChunk::create(&inner.cache_dir, new_id, chunk_size)?;
                let offset = new_chunk.allocate(&data)?;
                let chunk_id = new_chunk.id;
                inner.chunks.push(new_chunk);
                (chunk_id, offset)
            } else {
                return Err(CacheError::OutOfMemory);
            }
        };

        // If key already existed, remove old position from lru
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

        Ok(())
    }

    pub fn remove(&self, key: String) -> bool {
        let mut inner = self.inner.write();
        if inner.entries.remove(&key).is_some() {
            if let Some(pos) = inner.lru_order.iter().position(|k| k == &key) {
                inner.lru_order.remove(pos);
            }
            true
        } else {
            false
        }
    }

    pub fn clear(&self) -> Result<(), CacheError> {
        let mut inner = self.inner.write();
        inner.entries.clear();
        inner.lru_order.clear();

        // Delete existing chunks from disk: drop chunks first to close open handles on Windows
        let chunk_paths: Vec<PathBuf> = inner.chunks.iter().map(|c| c.path.clone()).collect();
        inner.chunks.clear();
        for path in chunk_paths {
            let _ = fs::remove_file(&path);
        }

        // Clean up any legacy *.cache files
        if let Ok(entries) = fs::read_dir(&inner.cache_dir) {
            for entry in entries.flatten() {
                if let Some(ext) = entry.path().extension() {
                    if ext == "cache" {
                        let _ = fs::remove_file(entry.path());
                    }
                }
            }
        }

        let first_chunk = MmapChunk::create(&inner.cache_dir, 0, inner.initial_file_size)?;
        inner.chunks.push(first_chunk);
        Ok(())
    }

    pub fn purge_compact(&self) -> Result<u64, CacheError> {
        let mut inner = self.inner.write();
        let total_entries = inner.lru_order.len();
        if total_entries == 0 {
            return Ok(0);
        }

        // Retain top half of LRU entries
        let retain_count = (total_entries / 2).max(1);
        let evict_count = total_entries - retain_count;

        for _ in 0..evict_count {
            if let Some(old_key) = inner.lru_order.pop_front() {
                inner.entries.remove(&old_key);
            }
        }

        compact_inner(&mut inner)?;

        Ok(evict_count as u64)
    }

    pub fn purge_to_size(&self, max_bytes: u64) -> Result<u64, CacheError> {
        let mut inner = self.inner.write();
        let current_used: u64 = inner.entries.values().map(|e| e.len as u64).sum();
        if current_used <= max_bytes {
            return Ok(0);
        }

        let mut target_used = current_used;
        let mut evict_count = 0u64;

        while target_used > max_bytes {
            if let Some(old_key) = inner.lru_order.pop_front() {
                if let Some(entry) = inner.entries.remove(&old_key) {
                    target_used = target_used.saturating_sub(entry.len as u64);
                    evict_count += 1;
                }
            } else {
                break;
            }
        }

        if evict_count == 0 {
            return Ok(0);
        }

        compact_inner(&mut inner)?;

        Ok(evict_count)
    }

    pub fn stats(&self) -> CacheStats {
        let inner = self.inner.read();
        let file_count = inner.chunks.len() as u32;
        let entry_count = inner.entries.len() as u32;
        let total_allocated = inner.chunks.iter().map(|c| c.capacity as u64).sum();
        let total_used = inner.entries.values().map(|e| e.len as u64).sum();

        CacheStats {
            file_count,
            entry_count,
            total_allocated_bytes: total_allocated,
            total_used_bytes: total_used,
        }
    }
}

fn compact_inner(inner: &mut CacheInner) -> Result<(), CacheError> {
    // Read all retained entries data into memory
    let mut active_data: Vec<(String, Vec<u8>)> = Vec::new();
    for key in &inner.lru_order {
        if let Some(entry) = inner.entries.get(key)
            && let Some(chunk) = inner.chunks.iter().find(|c| c.id == entry.chunk_id)
            && let Some(slice) = chunk.read(entry.offset, entry.len)
        {
            active_data.push((key.clone(), slice.to_vec()));
        }
    }

    // Reset chunks: drop handles first before deleting files on Windows
    let chunk_paths: Vec<PathBuf> = inner.chunks.iter().map(|c| c.path.clone()).collect();
    inner.chunks.clear();
    for path in chunk_paths {
        let _ = fs::remove_file(&path);
    }

    inner.entries.clear();
    inner.lru_order.clear();

    let mut current_chunk = MmapChunk::create(&inner.cache_dir, 0, inner.initial_file_size)?;
    for (key, data) in active_data {
        let aligned = (data.len() + 7) & !7;
        if !current_chunk.can_allocate(aligned) {
            let new_id = inner.chunks.len() as u32 + 1;
            let chunk_size = inner.initial_file_size.max(aligned);
            let prev = std::mem::replace(
                &mut current_chunk,
                MmapChunk::create(&inner.cache_dir, new_id, chunk_size)?,
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

    Ok(())
}
