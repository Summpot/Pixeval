// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::error::MediaError;

pub fn is_supported() -> bool {
    #[cfg(target_os = "macos")]
    {
        // Dynamically probe AVFoundation framework on macOS
        let lib = unsafe {
            libloading::Library::new("/System/Library/Frameworks/AVFoundation.framework/AVFoundation")
        };
        lib.is_ok()
    }
    #[cfg(not(target_os = "macos"))]
    {
        false
    }
}

pub fn encode_mp4(_output_path: &str, _frames_data: &[(Vec<u8>, u32)]) -> Result<(), MediaError> {
    Err(MediaError::UnsupportedFormat {
        message: "Native macOS AVFoundation encoder is not yet supported in this build".into(),
    })
}
