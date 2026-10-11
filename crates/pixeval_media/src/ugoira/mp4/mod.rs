// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[cfg(target_os = "windows")]
pub mod windows;
#[cfg(target_os = "windows")]
use windows as platform;

#[cfg(target_os = "macos")]
pub mod macos;
#[cfg(target_os = "macos")]
use macos as platform;

#[cfg(target_os = "linux")]
pub mod linux;
#[cfg(target_os = "linux")]
use linux as platform;

#[cfg(not(any(target_os = "windows", target_os = "macos", target_os = "linux")))]
pub mod stub;
#[cfg(not(any(target_os = "windows", target_os = "macos", target_os = "linux")))]
use stub as platform;

// Keep other platform modules checked for syntax
#[cfg(not(target_os = "macos"))]
#[allow(dead_code)]
pub mod macos;

#[cfg(not(target_os = "linux"))]
#[allow(dead_code)]
pub mod linux;

#[cfg(any(target_os = "windows", target_os = "macos", target_os = "linux"))]
#[allow(dead_code)]
pub mod stub;

pub fn is_supported() -> bool {
    platform::is_supported()
}

pub fn encode_mp4(
    output_path: &str,
    frames_data: &[(Vec<u8>, u32)],
) -> Result<(), crate::error::MediaError> {
    platform::encode_mp4(output_path, frames_data)
}
