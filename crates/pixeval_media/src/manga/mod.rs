// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::fs::File;
use std::io::{BufWriter, Write};
use std::path::Path;

use zip::write::SimpleFileOptions;
use zip::CompressionMethod;

use crate::error::MediaError;

#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq)]
pub enum MangaArchiveFormat {
    Zip,
    Cbz,
}

/// Pack multiple manga/illustration pages into a CBZ or ZIP archive.
pub fn pack_manga(
    file_paths: Vec<String>,
    output_path: &str,
    _format: MangaArchiveFormat,
) -> Result<(), MediaError> {
    if file_paths.is_empty() {
        return Err(MediaError::InvalidInput {
            message: "No pages to pack".into(),
        });
    }

    let temp_path = format!("{output_path}.pixevaltmp");
    let temp_file = File::create(&temp_path).map_err(|e| MediaError::Io {
        message: format!("Failed to create temp archive: {e}"),
    })?;
    let writer = BufWriter::new(temp_file);
    let mut zip = zip::ZipWriter::new(writer);

    let options = SimpleFileOptions::default()
        .compression_method(CompressionMethod::Deflated);

    for (index, file_path) in file_paths.iter().enumerate() {
        let path = Path::new(file_path);
        let ext = path.extension().and_then(|s| s.to_str()).unwrap_or("jpg");
        let entry_name = format!("{index:04}.{ext}");

        let data = std::fs::read(path).map_err(|e| MediaError::Io {
            message: format!("Failed to read page {file_path}: {e}"),
        })?;

        zip.start_file(entry_name, options)?;
        zip.write_all(&data)?;
    }

    zip.finish()?;

    // Atomic replacement
    if Path::new(output_path).exists() {
        let _ = std::fs::remove_file(output_path);
    }
    if let Some(parent) = Path::new(output_path).parent() {
        let _ = std::fs::create_dir_all(parent);
    }
    std::fs::rename(&temp_path, output_path).map_err(|e| MediaError::Io {
        message: format!("Failed to commit manga archive: {e}"),
    })?;

    Ok(())
}
