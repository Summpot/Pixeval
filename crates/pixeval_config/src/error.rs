// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use thiserror::Error;

#[derive(Debug, Error, uniffi::Error)]
pub enum ConfigError {
    #[error("IO error: {message}")]
    Io { message: String },

    #[error("YAML parse or serialize error: {message}")]
    Yaml { message: String },

    #[error("JSON error: {message}")]
    Json { message: String },

    #[error("Migration error: {message}")]
    Migration { message: String },
}

impl From<std::io::Error> for ConfigError {
    fn from(err: std::io::Error) -> Self {
        ConfigError::Io {
            message: err.to_string(),
        }
    }
}

impl From<serde_yaml::Error> for ConfigError {
    fn from(err: serde_yaml::Error) -> Self {
        ConfigError::Yaml {
            message: err.to_string(),
        }
    }
}

impl From<serde_json::Error> for ConfigError {
    fn from(err: serde_json::Error) -> Self {
        ConfigError::Json {
            message: err.to_string(),
        }
    }
}
