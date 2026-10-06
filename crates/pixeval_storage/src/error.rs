// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use thiserror::Error;

#[derive(Debug, Error, uniffi::Error)]
pub enum StorageError {
    #[error("Database error: {message}")]
    Database { message: String },

    #[error("Serialization error: {message}")]
    Serialization { message: String },

    #[error("Record not found: {message}")]
    NotFound { message: String },

    #[error("Constraint violation: {message}")]
    ConstraintViolation { message: String },
}

impl From<rusqlite::Error> for StorageError {
    fn from(err: rusqlite::Error) -> Self {
        StorageError::Database {
            message: err.to_string(),
        }
    }
}

impl From<serde_json::Error> for StorageError {
    fn from(err: serde_json::Error) -> Self {
        StorageError::Serialization {
            message: err.to_string(),
        }
    }
}
