// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::fs::File;
use std::io::{Cursor, Write};
use std::path::Path;

use image::codecs::jpeg::JpegEncoder;
use image::codecs::webp::WebPEncoder;
use image::{ExtendedColorType, ImageFormat};

use crate::error::MediaError;

#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq)]
pub enum ImageCodecFormat {
    Jpeg,
    Png,
    Webp,
    Avif,
}

#[derive(uniffi::Record, Clone, Debug, Default)]
pub struct TranscodeOptions {
    pub quality: Option<u8>,
    pub lossless: bool,
}

/// Transcode an image buffer into the desired target format.
pub fn transcode_bytes(
    input_bytes: Vec<u8>,
    target_format: ImageCodecFormat,
    options: Option<TranscodeOptions>,
) -> Result<Vec<u8>, MediaError> {
    let dyn_img = image::load_from_memory(&input_bytes)?;
    let options = options.unwrap_or_default();
    let quality = options.quality.unwrap_or(85).clamp(1, 100);

    let mut out = Vec::new();
    match target_format {
        ImageCodecFormat::Jpeg => {
            let rgb = dyn_img.to_rgb8();
            let mut encoder = JpegEncoder::new_with_quality(&mut out, quality);
            encoder.encode(
                rgb.as_raw(),
                rgb.width(),
                rgb.height(),
                ExtendedColorType::Rgb8,
            )?;
        }
        ImageCodecFormat::Png => {
            dyn_img.write_to(&mut Cursor::new(&mut out), ImageFormat::Png)?;
        }
        ImageCodecFormat::Webp => {
            let rgba = dyn_img.to_rgba8();
            if options.lossless {
                WebPEncoder::new_lossless(&mut out).encode(
                    rgba.as_raw(),
                    rgba.width(),
                    rgba.height(),
                    ExtendedColorType::Rgba8,
                )?;
            } else {
                dyn_img.write_to(&mut Cursor::new(&mut out), ImageFormat::WebP)?;
            }
        }
        ImageCodecFormat::Avif => {
            dyn_img.write_to(&mut Cursor::new(&mut out), ImageFormat::Avif)?;
        }
    }

    Ok(out)
}

/// Transcode an image file on disk and atomically save to the output path.
pub fn transcode_file(
    input_path: &str,
    output_path: &str,
    target_format: ImageCodecFormat,
    options: Option<TranscodeOptions>,
) -> Result<(), MediaError> {
    let input_bytes = std::fs::read(input_path)?;
    let output_bytes = transcode_bytes(input_bytes, target_format, options)?;

    let temp_path = format!("{output_path}.pixevaltmp");
    let mut temp_file = File::create(&temp_path)?;
    temp_file.write_all(&output_bytes)?;
    drop(temp_file);

    // Atomic replacement
    if Path::new(output_path).exists() {
        let _ = std::fs::remove_file(output_path);
    }
    if let Some(parent) = Path::new(output_path).parent() {
        let _ = std::fs::create_dir_all(parent);
    }
    std::fs::rename(&temp_path, output_path).map_err(|e| MediaError::Io {
        message: format!("Failed to move transcoded file: {e}"),
    })?;

    Ok(())
}
