// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum MediaError {
    #[error("I/O error: {message}")]
    Io { message: String },

    #[error("Image error: {message}")]
    Image { message: String },

    #[error("Zip error: {message}")]
    Zip { message: String },

    #[error("Unsupported format: {message}")]
    UnsupportedFormat { message: String },

    #[error("Invalid input: {message}")]
    InvalidInput { message: String },

    #[error("Packaging failed: {message}")]
    Packaging { message: String },

    #[error("Synthesis failed: {message}")]
    Synthesis { message: String },
}

impl From<std::io::Error> for MediaError {
    fn from(err: std::io::Error) -> Self {
        MediaError::Io {
            message: err.to_string(),
        }
    }
}

impl From<image::ImageError> for MediaError {
    fn from(err: image::ImageError) -> Self {
        MediaError::Image {
            message: err.to_string(),
        }
    }
}

impl From<zip::result::ZipError> for MediaError {
    fn from(err: zip::result::ZipError) -> Self {
        MediaError::Zip {
            message: err.to_string(),
        }
    }
}
