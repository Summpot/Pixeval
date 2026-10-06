// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::fs::File;
use std::io::{BufReader, BufWriter, Read, Write};
use std::path::Path;

use image::codecs::gif::{GifEncoder, Repeat};
use image::codecs::webp::WebPEncoder;
use image::{Delay, Frame};

use crate::error::MediaError;

#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq)]
pub enum UgoiraFormat {
    Original,
    Gif,
    Apng,
    Webp,
    Mp4,
}

pub fn is_mp4_supported() -> bool {
    // OS native MP4 encoder fallback check.
    // If not implemented or not supported on this platform, return false so the UI disables the option.
    false
}

/// Synthesize Ugoira frames from a Pixiv zip archive into the target animation format.
pub fn synthesize_ugoira_from_zip(
    zip_path: &str,
    output_path: &str,
    format: UgoiraFormat,
    frame_delays_ms: Vec<u32>,
) -> Result<(), MediaError> {
    if format == UgoiraFormat::Mp4 && !is_mp4_supported() {
        return Err(MediaError::UnsupportedFormat {
            message: "MP4 encoding is not supported on this platform".into(),
        });
    }

    let file = File::open(zip_path).map_err(|e| MediaError::Io {
        message: format!("Failed to open zip: {e}"),
    })?;
    let mut archive = zip::ZipArchive::new(BufReader::new(file))?;

    let mut entry_names: Vec<String> = archive
        .file_names()
        .filter(|s| !s.ends_with('/') && !s.starts_with("__MACOSX"))
        .map(|s| s.to_string())
        .collect();
    entry_names.sort();

    if entry_names.is_empty() {
        return Err(MediaError::InvalidInput {
            message: "Zip contains no frames".into(),
        });
    }

    if format == UgoiraFormat::Original {
        return extract_zip_to_original(archive, entry_names, output_path, &frame_delays_ms);
    }

    // Read frame buffers
    let mut frames_data: Vec<(Vec<u8>, u32)> = Vec::with_capacity(entry_names.len());
    for (i, name) in entry_names.iter().enumerate() {
        let mut entry = archive.by_name(name)?;
        let mut buf = Vec::with_capacity(entry.size() as usize);
        entry.read_to_end(&mut buf)?;
        let delay = frame_delays_ms.get(i).copied().unwrap_or(100);
        frames_data.push((buf, delay));
    }

    synthesize_from_frame_bytes(&frames_data, output_path, format)
}

/// Synthesize Ugoira from loose frame files on disk.
pub fn synthesize_ugoira_from_frames(
    frame_paths: Vec<String>,
    output_path: &str,
    format: UgoiraFormat,
    frame_delays_ms: Vec<u32>,
) -> Result<(), MediaError> {
    if format == UgoiraFormat::Mp4 && !is_mp4_supported() {
        return Err(MediaError::UnsupportedFormat {
            message: "MP4 encoding is not supported on this platform".into(),
        });
    }

    if frame_paths.is_empty() {
        return Err(MediaError::InvalidInput {
            message: "No frame paths provided".into(),
        });
    }

    if format == UgoiraFormat::Original {
        let out_dir = Path::new(output_path);
        std::fs::create_dir_all(out_dir)?;
        for (i, p) in frame_paths.iter().enumerate() {
            let src = Path::new(p);
            let ext = src.extension().and_then(|s| s.to_str()).unwrap_or("jpg");
            let dst = out_dir.join(format!("{i:06}.{ext}"));
            std::fs::copy(src, dst)?;
        }
        let csv_path = out_dir.join("intervals in milliseconds.csv");
        let csv_content = frame_delays_ms
            .iter()
            .map(|d| d.to_string())
            .collect::<Vec<_>>()
            .join(",");
        std::fs::write(csv_path, csv_content)?;
        return Ok(());
    }

    let mut frames_data: Vec<(Vec<u8>, u32)> = Vec::with_capacity(frame_paths.len());
    for (i, p) in frame_paths.iter().enumerate() {
        let buf = std::fs::read(p)?;
        let delay = frame_delays_ms.get(i).copied().unwrap_or(100);
        frames_data.push((buf, delay));
    }

    synthesize_from_frame_bytes(&frames_data, output_path, format)
}

fn extract_zip_to_original(
    mut archive: zip::ZipArchive<BufReader<File>>,
    entry_names: Vec<String>,
    output_dir_path: &str,
    frame_delays_ms: &[u32],
) -> Result<(), MediaError> {
    let out_dir = Path::new(output_dir_path);
    std::fs::create_dir_all(out_dir)?;

    for (i, name) in entry_names.iter().enumerate() {
        let mut entry = archive.by_name(name)?;
        let ext = Path::new(name)
            .extension()
            .and_then(|s| s.to_str())
            .unwrap_or("jpg");
        let dest = out_dir.join(format!("{i:06}.{ext}"));
        let mut outfile = File::create(dest)?;
        std::io::copy(&mut entry, &mut outfile)?;
    }

    let csv_path = out_dir.join("intervals in milliseconds.csv");
    let csv_content = frame_delays_ms
        .iter()
        .map(|d| d.to_string())
        .collect::<Vec<_>>()
        .join(",");
    std::fs::write(csv_path, csv_content)?;

    Ok(())
}

fn synthesize_from_frame_bytes(
    frames_data: &[(Vec<u8>, u32)],
    output_path: &str,
    format: UgoiraFormat,
) -> Result<(), MediaError> {
    let temp_path = format!("{output_path}.pixevaltmp");
    let temp_file = File::create(&temp_path).map_err(|e| MediaError::Io {
        message: format!("Failed to create temp file: {e}"),
    })?;
    let mut writer = BufWriter::new(temp_file);

    let result = match format {
        UgoiraFormat::Gif => encode_gif(&mut writer, frames_data),
        UgoiraFormat::Apng => encode_apng(&mut writer, frames_data),
        UgoiraFormat::Webp => encode_webp(&mut writer, frames_data),
        UgoiraFormat::Mp4 => Err(MediaError::UnsupportedFormat {
            message: "MP4 encoding is not supported on this platform".into(),
        }),
        UgoiraFormat::Original => Err(MediaError::InvalidInput {
            message: "Original format must be extracted to folder".into(),
        }),
    };

    drop(writer);

    if let Err(e) = result {
        let _ = std::fs::remove_file(&temp_path);
        return Err(e);
    }

    // Atomic replacement
    if Path::new(output_path).exists() {
        let _ = std::fs::remove_file(output_path);
    }
    if let Some(parent) = Path::new(output_path).parent() {
        let _ = std::fs::create_dir_all(parent);
    }
    std::fs::rename(&temp_path, output_path).map_err(|e| MediaError::Io {
        message: format!("Failed to move temp file to output: {e}"),
    })?;

    Ok(())
}

fn encode_gif<W: Write>(writer: &mut W, frames_data: &[(Vec<u8>, u32)]) -> Result<(), MediaError> {
    let mut encoder = GifEncoder::new(writer);
    encoder
        .set_repeat(Repeat::Infinite)
        .map_err(|e| MediaError::Synthesis {
            message: format!("Failed to set gif repeat: {e}"),
        })?;

    for (bytes, delay_ms) in frames_data {
        let dyn_img = image::load_from_memory(bytes)?;
        let rgba = dyn_img.to_rgba8();
        let frame = Frame::from_parts(rgba, 0, 0, Delay::from_numer_denom_ms(*delay_ms, 1));
        encoder
            .encode_frame(frame)
            .map_err(|e| MediaError::Synthesis {
                message: format!("Failed to encode gif frame: {e}"),
            })?;
    }

    Ok(())
}

fn encode_apng<W: Write>(writer: &mut W, frames_data: &[(Vec<u8>, u32)]) -> Result<(), MediaError> {
    if frames_data.is_empty() {
        return Ok(());
    }

    let first_img = image::load_from_memory(&frames_data[0].0)?;
    let (width, height) = (first_img.width(), first_img.height());

    let mut encoder = png::Encoder::new(writer, width, height);
    encoder.set_color(png::ColorType::Rgba);
    encoder.set_depth(png::BitDepth::Eight);
    encoder
        .set_animated(frames_data.len() as u32, 0)
        .map_err(|e| MediaError::Synthesis {
            message: format!("Failed to set APNG animation: {e}"),
        })?;
    let first_delay = frames_data[0].1;
    encoder
        .set_frame_delay(first_delay as u16, 1000)
        .map_err(|e| MediaError::Synthesis {
            message: format!("Failed to set APNG delay: {e}"),
        })?;

    let mut png_writer = encoder
        .write_header()
        .map_err(|e| MediaError::Synthesis {
            message: format!("Failed to write APNG header: {e}"),
        })?;

    for (bytes, delay_ms) in frames_data {
        let dyn_img = image::load_from_memory(bytes)?;
        let rgba = dyn_img.to_rgba8();
        png_writer
            .set_frame_delay(*delay_ms as u16, 1000)
            .map_err(|e| MediaError::Synthesis {
                message: format!("Failed to set frame delay: {e}"),
            })?;
        png_writer
            .write_image_data(rgba.as_raw())
            .map_err(|e| MediaError::Synthesis {
                message: format!("Failed to write APNG frame: {e}"),
            })?;
    }

    png_writer
        .finish()
        .map_err(|e| MediaError::Synthesis {
            message: format!("Failed to finish APNG: {e}"),
        })?;

    Ok(())
}

fn encode_webp<W: Write>(writer: &mut W, frames_data: &[(Vec<u8>, u32)]) -> Result<(), MediaError> {
    if frames_data.is_empty() {
        return Ok(());
    }

    let first_img = image::load_from_memory(&frames_data[0].0)?;
    let (width, height) = (first_img.width(), first_img.height());

    // Collect encoded single-frame WebP subchunks
    let mut anmf_chunks = Vec::with_capacity(frames_data.len());
    for (bytes, delay_ms) in frames_data {
        let dyn_img = image::load_from_memory(bytes)?;
        let rgba = dyn_img.to_rgba8();

        let mut single_webp = Vec::new();
        WebPEncoder::new_lossless(&mut single_webp).encode(
            &rgba,
            width,
            height,
            image::ExtendedColorType::Rgba8,
        )?;

        if single_webp.len() < 12 || &single_webp[0..4] != b"RIFF" || &single_webp[8..12] != b"WEBP" {
            return Err(MediaError::Synthesis {
                message: "Invalid WebP frame generated".into(),
            });
        }

        // Subchunks begin after 12-byte RIFF header
        let subchunk = single_webp[12..].to_vec();
        anmf_chunks.push((subchunk, *delay_ms));
    }

    // Assemble WebP container
    let mut webp_buf = Vec::new();
    webp_buf.extend_from_slice(b"RIFF");
    webp_buf.extend_from_slice(&[0, 0, 0, 0]); // placeholder for size
    webp_buf.extend_from_slice(b"WEBP");

    // VP8X Chunk
    webp_buf.extend_from_slice(b"VP8X");
    webp_buf.extend_from_slice(&10u32.to_le_bytes());
    // Animation flag = 0x02
    let flags: u32 = 0x00000002;
    webp_buf.extend_from_slice(&flags.to_le_bytes());
    let w_m1 = width - 1;
    let h_m1 = height - 1;
    webp_buf.extend_from_slice(&[w_m1 as u8, (w_m1 >> 8) as u8, (w_m1 >> 16) as u8]);
    webp_buf.extend_from_slice(&[h_m1 as u8, (h_m1 >> 8) as u8, (h_m1 >> 16) as u8]);

    // ANIM Chunk
    webp_buf.extend_from_slice(b"ANIM");
    webp_buf.extend_from_slice(&6u32.to_le_bytes());
    webp_buf.extend_from_slice(&[0, 0, 0, 0]); // Background color RGBA
    webp_buf.extend_from_slice(&0u16.to_le_bytes()); // Loop count (0 = infinite)

    // ANMF Chunks
    for (subchunk, delay_ms) in &anmf_chunks {
        webp_buf.extend_from_slice(b"ANMF");
        let payload_len = 16 + subchunk.len();
        webp_buf.extend_from_slice(&(payload_len as u32).to_le_bytes());
        // Frame X (3 bytes), Frame Y (3 bytes)
        webp_buf.extend_from_slice(&[0, 0, 0, 0, 0, 0]);
        // Frame W - 1 (3 bytes), Frame H - 1 (3 bytes)
        webp_buf.extend_from_slice(&[w_m1 as u8, (w_m1 >> 8) as u8, (w_m1 >> 16) as u8]);
        webp_buf.extend_from_slice(&[h_m1 as u8, (h_m1 >> 8) as u8, (h_m1 >> 16) as u8]);
        // Duration (3 bytes)
        webp_buf.extend_from_slice(&[*delay_ms as u8, (*delay_ms >> 8) as u8, (*delay_ms >> 16) as u8]);
        // Flags (1 byte: do not blend = 0x01, dispose to bg = 0x02 -> 0x01)
        webp_buf.push(1);
        // Subchunks
        webp_buf.extend_from_slice(subchunk);
        if subchunk.len() % 2 != 0 {
            webp_buf.push(0); // padding
        }
    }

    let total_size = (webp_buf.len() - 8) as u32;
    webp_buf[4..8].copy_from_slice(&total_size.to_le_bytes());

    writer.write_all(&webp_buf)?;
    Ok(())
}
