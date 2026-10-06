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
}

impl From<std::io::Error> for CacheError {
    fn from(err: std::io::Error) -> Self {
        Self::Io {
            message: err.to_string(),
        }
    }
}
