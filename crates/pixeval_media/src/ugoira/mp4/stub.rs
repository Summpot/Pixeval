// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::error::MediaError;

pub fn is_supported() -> bool {
    false
}

pub fn encode_mp4(_output_path: &str, _frames_data: &[(Vec<u8>, u32)]) -> Result<(), MediaError> {
    Err(MediaError::UnsupportedFormat {
        message: "MP4 encoding is not supported on this platform".into(),
    })
}
