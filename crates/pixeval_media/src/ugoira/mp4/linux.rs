// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::error::MediaError;

pub fn is_supported() -> bool {
    #[cfg(target_os = "linux")]
    {
        // Dynamically probe GStreamer on Linux
        let lib = unsafe {
            libloading::Library::new("libgstreamer-1.0.so.0")
                .or_else(|_| libloading::Library::new("libgstreamer-1.0.so"))
        };
        if let Ok(lib) = lib {
            unsafe {
                let init_check: Result<
                    libloading::Symbol<unsafe extern "C" fn(*mut i32, *mut *mut *mut std::ffi::c_char) -> i32>,
                    _,
                > = lib.get(b"gst_init_check");
                init_check.is_ok()
            }
        } else {
            false
        }
    }
    #[cfg(not(target_os = "linux"))]
    {
        false
    }
}

pub fn encode_mp4(_output_path: &str, _frames_data: &[(Vec<u8>, u32)]) -> Result<(), MediaError> {
    Err(MediaError::UnsupportedFormat {
        message: "Native Linux GStreamer encoder is not yet supported in this build".into(),
    })
}
