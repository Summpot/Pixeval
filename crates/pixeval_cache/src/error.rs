use thiserror::Error;

#[derive(Error, Debug, Clone, PartialEq, Eq, uniffi::Error)]
pub enum CacheError {
    #[error("I/O error: {message}")]
    Io { message: String },

    #[error("Cache is full and reached maximum file count")]
    OutOfMemory,

    #[error("Memory mapping error: {message}")]
    MmapFailed { message: String },

    #[error("Invalid cache header or corrupted data")]
    CorruptedData,

    #[error("Cache engine is closed")]
    Closed,

    #[error("Item size {size} exceeds maximum allowable item limit {limit}")]
    ItemTooLarge { size: u64, limit: u64 },

    #[error("Network error: {message}")]
    Network { message: String },

    #[error("HTTP error status: {code}")]
    Http { code: u16 },

    #[error("Operation cancelled")]
    Cancelled,
}

impl From<std::io::Error> for CacheError {
    fn from(err: std::io::Error) -> Self {
        Self::Io {
            message: err.to_string(),
        }
    }
}
