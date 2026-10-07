// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use thiserror::Error;

#[derive(Error, Debug, Clone, PartialEq, Eq, uniffi::Error)]
pub enum CacheError {
    #[error("I/O error: {message}")]
    Io { message: String },

    #[error("Invalid cache header or corrupted data")]
    CorruptedData,

    #[error("Item size {size} exceeds maximum allowable item limit {limit}")]
    ItemTooLarge { size: u64, limit: u64 },

    #[error("Network error: {message}")]
    Network { message: String },

    #[error("HTTP error status: {code}")]
    Http { code: u16 },

    #[error("Operation cancelled")]
    Cancelled,

    #[error("Image codec error: {message}")]
    Codec { message: String },
}

impl From<std::io::Error> for CacheError {
    fn from(err: std::io::Error) -> Self {
        Self::Io {
            message: err.to_string(),
        }
    }
}

impl From<foyer::Error> for CacheError {
    fn from(err: foyer::Error) -> Self {
        Self::Io {
            message: err.to_string(),
        }
    }
}

