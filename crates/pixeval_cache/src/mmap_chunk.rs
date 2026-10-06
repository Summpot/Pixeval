use memmap2::MmapMut;
use std::fs::{File, OpenOptions};
use std::path::{Path, PathBuf};

use crate::error::CacheError;

pub struct MmapChunk {
    pub id: u32,
    pub path: PathBuf,
    _file: File,
    mmap: MmapMut,
    pub capacity: usize,
    pub bump_offset: usize,
}

impl MmapChunk {
    pub fn create(dir: &Path, id: u32, size: usize) -> Result<Self, CacheError> {
        let path = dir.join(format!("cache_chunk_{id:04}.bin"));
        let file = OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .truncate(true)
            .open(&path)?;

        file.set_len(size as u64)?;
        let mmap = unsafe { MmapMut::map_mut(&file)? };

        Ok(Self {
            id,
            path,
            _file: file,
            mmap,
            capacity: size,
            bump_offset: 0,
        })
    }

    pub fn can_allocate(&self, bytes: usize) -> bool {
        self.bump_offset + bytes <= self.capacity
    }

    pub fn allocate(&mut self, data: &[u8]) -> Result<usize, CacheError> {
        let aligned_len = (data.len() + 7) & !7;
        if self.bump_offset + aligned_len > self.capacity {
            return Err(CacheError::OutOfMemory);
        }

        let offset = self.bump_offset;
        self.mmap[offset..offset + data.len()].copy_from_slice(data);
        self.bump_offset += aligned_len;
        Ok(offset)
    }

    pub fn read(&self, offset: usize, len: usize) -> Option<&[u8]> {
        if offset + len <= self.capacity {
            Some(&self.mmap[offset..offset + len])
        } else {
            None
        }
    }

    pub fn write_at(&mut self, offset: usize, data: &[u8]) -> Result<(), CacheError> {
        if offset + data.len() > self.capacity {
            return Err(CacheError::OutOfMemory);
        }
        self.mmap[offset..offset + data.len()].copy_from_slice(data);
        Ok(())
    }

    pub fn flush(&self) -> Result<(), CacheError> {
        self.mmap
            .flush()
            .map_err(|e| CacheError::Io { message: e.to_string() })
    }
}
