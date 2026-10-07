// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(uniffi::Error, thiserror::Error, Debug)]
pub enum SauceNaoError {
    #[error("Rate limited by SauceNao (wait {wait_seconds}s)")]
    RateLimited { wait_seconds: u64 },

    #[error("Invalid API key")]
    InvalidApiKey,

    #[error("SauceNao API error ({status}): {message}")]
    ApiError { status: i32, message: String },

    #[error("Network error: {message}")]
    NetworkError { message: String },

    #[error("Parse error: {message}")]
    ParseError { message: String },
}

impl From<pixeval_maho::MahoError> for SauceNaoError {
    fn from(err: pixeval_maho::MahoError) -> Self {
        Self::NetworkError {
            message: err.to_string(),
        }
    }
}

impl From<serde_json::Error> for SauceNaoError {
    fn from(err: serde_json::Error) -> Self {
        Self::ParseError {
            message: err.to_string(),
        }
    }
}
