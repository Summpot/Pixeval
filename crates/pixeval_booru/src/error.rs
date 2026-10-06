// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(uniffi::Error, thiserror::Error, Debug)]
pub enum BooruError {
    #[error("Post not found on {platform}: {id}")]
    PostNotFound { platform: String, id: String },

    #[error("API error on {platform} ({code}): {message}")]
    ApiError {
        platform: String,
        code: u16,
        message: String,
    },

    #[error("Network error: {message}")]
    NetworkError { message: String },

    #[error("Parse error: {message}")]
    ParseError { message: String },

    #[error("Operation not supported for platform: {message}")]
    NotSupported { message: String },
}

impl From<pixeval_maho::MahoError> for BooruError {
    fn from(err: pixeval_maho::MahoError) -> Self {
        Self::NetworkError {
            message: err.to_string(),
        }
    }
}

impl From<serde_json::Error> for BooruError {
    fn from(err: serde_json::Error) -> Self {
        Self::ParseError {
            message: err.to_string(),
        }
    }
}
