// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::sync::Arc;

use crate::error::MediaError;
use crate::manga::{self, MangaArchiveFormat};
use crate::transcode::{self, ImageCodecFormat, TranscodeOptions};
use crate::ugoira::{self, UgoiraFormat};

#[derive(uniffi::Object, Default)]
pub struct MediaEngine;

#[uniffi::export]
impl MediaEngine {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Arc::new(Self)
    }

    /// Check if MP4 synthesis is supported on current OS.
    pub fn is_mp4_supported(&self) -> bool {
        ugoira::is_mp4_supported()
    }

    /// Synthesize Ugoira from a Pixiv zip archive.
    pub fn synthesize_ugoira_from_zip(
        &self,
        zip_path: String,
        output_path: String,
        format: UgoiraFormat,
        frame_delays_ms: Vec<u32>,
    ) -> Result<(), MediaError> {
        ugoira::synthesize_ugoira_from_zip(&zip_path, &output_path, format, frame_delays_ms)
    }

    /// Synthesize Ugoira from individual frame file paths.
    pub fn synthesize_ugoira_from_frames(
        &self,
        frame_paths: Vec<String>,
        output_path: String,
        format: UgoiraFormat,
        frame_delays_ms: Vec<u32>,
    ) -> Result<(), MediaError> {
        ugoira::synthesize_ugoira_from_frames(frame_paths, &output_path, format, frame_delays_ms)
    }

    /// Synthesize Ugoira automatically: detects whether input_path is a zip file or a folder of frames.
    pub fn synthesize_ugoira(
        &self,
        input_path: String,
        output_path: String,
        format: UgoiraFormat,
        frame_delays_ms: Vec<u32>,
    ) -> Result<(), MediaError> {
        let path = std::path::Path::new(&input_path);
        if path.is_file() && path.extension().and_then(|s| s.to_str()).map(|s| s.eq_ignore_ascii_case("zip")).unwrap_or(false) {
            ugoira::synthesize_ugoira_from_zip(&input_path, &output_path, format, frame_delays_ms)
        } else if path.is_dir() {
            let mut entries: Vec<String> = std::fs::read_dir(path)?
                .filter_map(|e| e.ok())
                .filter(|e| e.file_type().map(|t| t.is_file()).unwrap_or(false))
                .filter_map(|e| e.path().to_str().map(|s| s.to_string()))
                .collect();
            entries.sort();
            ugoira::synthesize_ugoira_from_frames(entries, &output_path, format, frame_delays_ms)
        } else {
            // Assume it's a zip file path even if extension doesn't match
            ugoira::synthesize_ugoira_from_zip(&input_path, &output_path, format, frame_delays_ms)
        }
    }

    /// Pack multi-page manga into a CBZ or ZIP archive.
    pub fn pack_manga(
        &self,
        file_paths: Vec<String>,
        output_path: String,
        format: MangaArchiveFormat,
    ) -> Result<(), MediaError> {
        manga::pack_manga(file_paths, &output_path, format)
    }

    /// Transcode an image file.
    pub fn transcode_file(
        &self,
        input_path: String,
        output_path: String,
        target_format: ImageCodecFormat,
        options: Option<TranscodeOptions>,
    ) -> Result<(), MediaError> {
        transcode::transcode_file(&input_path, &output_path, target_format, options)
    }

    /// Transcode image bytes in memory.
    pub fn transcode_bytes(
        &self,
        input_bytes: Vec<u8>,
        target_format: ImageCodecFormat,
        options: Option<TranscodeOptions>,
    ) -> Result<Vec<u8>, MediaError> {
        transcode::transcode_bytes(input_bytes, target_format, options)
    }
}
