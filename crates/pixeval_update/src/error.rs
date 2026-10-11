// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use pixeval_maho::MahoError;

#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum UpdateError {
    #[error("Network error: {message}")]
    Network { message: String },

    #[error("HTTP error ({status_code}): {message}")]
    Http { status_code: u16, message: String },

    #[error("Version parse error: {message}")]
    VersionParse { message: String },

    #[error("Checksum mismatch: expected '{expected}', actual '{actual}'")]
    ChecksumMismatch { expected: String, actual: String },

    #[error("IO error: {message}")]
    Io { message: String },

    #[error("Operation cancelled")]
    Cancelled,

    #[error("JSON serialization error: {message}")]
    Json { message: String },
}

impl From<std::io::Error> for UpdateError {
    fn from(err: std::io::Error) -> Self {
        UpdateError::Io {
            message: err.to_string(),
        }
    }
}

impl From<serde_json::Error> for UpdateError {
    fn from(err: serde_json::Error) -> Self {
        UpdateError::Json {
            message: err.to_string(),
        }
    }
}

impl From<MahoError> for UpdateError {
    fn from(err: MahoError) -> Self {
        match err {
            MahoError::Http { code } => UpdateError::Http {
                status_code: code,
                message: format!("HTTP status {code}"),
            },
            MahoError::Json { message } => UpdateError::Json { message },
            MahoError::Io { message } => UpdateError::Io { message },
            other => UpdateError::Network {
                message: other.to_string(),
            },
        }
    }
}
