// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::fs::{self, File};
use std::io::{Read, Seek};
use std::path::{Path, PathBuf};

use crate::binary::{has_extension_entry_point, is_native_library_path};
use crate::error::PluginError;
use crate::models::PluginInstallResult;

/// Sanitizes and validates a Zip entry's path, protecting against ZipSlip directory traversal attacks.
pub fn validate_zip_entry_path(entry_name: &str) -> Result<PathBuf, PluginError> {
    if entry_name.is_empty() {
        return Err(PluginError::Security {
            message: "Empty zip entry path".to_string(),
        });
    }

    let normalized = entry_name.replace('\\', "/");
    let trimmed = normalized.trim_matches('/');

    if trimmed.is_empty() {
        return Err(PluginError::Security {
            message: "Root-only zip entry path".to_string(),
        });
    }

    // Disallow absolute paths, drive letters, and parent directory traversal
    for segment in trimmed.split('/') {
        if segment.is_empty() || segment == "." || segment == ".." || segment.contains(':') {
            return Err(PluginError::Security {
                message: format!("Unsafe zip entry path component: {entry_name}"),
            });
        }
    }

    Ok(PathBuf::from(trimmed))
}

/// Checks whether all entries in the zip archive share a single common top-level directory.
pub fn contains_single_top_level_directory<R: Read + Seek>(
    archive: &mut zip::ZipArchive<R>,
) -> Result<bool, PluginError> {
    let mut root_dir: Option<String> = None;
    let mut has_entries = false;

    for i in 0..archive.len() {
        let entry = archive.by_index(i).map_err(|e| PluginError::InvalidArchive {
            message: format!("Failed to read zip entry #{i}: {e}"),
        })?;

        let entry_name = entry.name();
        let safe_path = match validate_zip_entry_path(entry_name) {
            Ok(p) => p,
            Err(_) => return Ok(false),
        };

        has_entries = true;
        let components: Vec<_> = safe_path.components().map(|c| c.as_os_str().to_string_lossy().to_string()).collect();
        if components.is_empty() {
            return Ok(false);
        }

        let first = &components[0];
        match &root_dir {
            None => root_dir = Some(first.clone()),
            Some(existing) => {
                if !existing.eq_ignore_ascii_case(first) {
                    return Ok(false);
                }
            }
        }

        // If an entry only has 1 component and is not a directory, it's a file at root level
        if components.len() == 1 && !entry.is_dir() && !entry_name.ends_with('/') && !entry_name.ends_with('\\') {
            return Ok(false);
        }
    }

    Ok(has_entries && root_dir.is_some())
}

/// Discovers all entries in the archive that are native dynamic libraries
/// and statically export the extension entry points.
pub fn find_archive_host_libraries<R: Read + Seek>(
    archive: &mut zip::ZipArchive<R>,
) -> Result<Vec<String>, PluginError> {
    let mut host_libraries = Vec::new();

    for i in 0..archive.len() {
        let mut entry = archive.by_index(i).map_err(|e| PluginError::InvalidArchive {
            message: format!("Failed to inspect zip entry #{i}: {e}"),
        })?;

        if entry.is_dir() {
            continue;
        }

        let safe_path = match validate_zip_entry_path(entry.name()) {
            Ok(p) => p,
            Err(_) => continue,
        };

        if !is_native_library_path(&safe_path) {
            continue;
        }

        // Read library bytes from the zip entry and inspect exports
        let mut bytes = Vec::with_capacity(entry.size() as usize);
        if entry.read_to_end(&mut bytes).is_ok() && has_extension_entry_point(&bytes) {
            host_libraries.push(safe_path.to_string_lossy().to_string());
        }
    }

    host_libraries.sort();
    Ok(host_libraries)
}

/// Safely inspects, verifies, and unpacks a plugin archive (.zip) into the extensions folder.
pub fn extract_plugin_archive(
    archive_path: &Path,
    extensions_dir: &Path,
) -> Result<PluginInstallResult, PluginError> {
    let file = File::open(archive_path).map_err(|e| PluginError::Io {
        message: format!("Failed to open archive {}: {e}", archive_path.display()),
    })?;

    let mut archive = zip::ZipArchive::new(file).map_err(|e| PluginError::InvalidArchive {
        message: format!("Invalid zip archive {}: {e}", archive_path.display()),
    })?;

    let host_relative_paths = find_archive_host_libraries(&mut archive)?;
    if host_relative_paths.is_empty() {
        return Ok(PluginInstallResult {
            destination_dir: extensions_dir.to_string_lossy().to_string(),
            installed_host_libraries: Vec::new(),
        });
    }

    let single_root = contains_single_top_level_directory(&mut archive)?;
    let destination_dir = if single_root {
        extensions_dir.to_path_buf()
    } else {
        let stem = archive_path
            .file_stem()
            .map(|s| s.to_string_lossy().to_string())
            .unwrap_or_else(|| "Extension".to_string());
        extensions_dir.join(stem)
    };

    fs::create_dir_all(&destination_dir).map_err(|e| PluginError::Io {
        message: format!(
            "Failed to create target directory {}: {e}",
            destination_dir.display()
        ),
    })?;

    for i in 0..archive.len() {
        let mut entry = archive.by_index(i).map_err(|e| PluginError::InvalidArchive {
            message: format!("Failed to read entry #{i}: {e}"),
        })?;

        let safe_rel_path = validate_zip_entry_path(entry.name())?;
        let out_path = destination_dir.join(&safe_rel_path);

        if entry.is_dir() || entry.name().ends_with('/') || entry.name().ends_with('\\') {
            fs::create_dir_all(&out_path).map_err(|e| PluginError::Io {
                message: format!("Failed to create directory {}: {e}", out_path.display()),
            })?;
        } else {
            if let Some(parent) = out_path.parent() {
                fs::create_dir_all(parent).map_err(|e| PluginError::Io {
                    message: format!("Failed to create parent directory {}: {e}", parent.display()),
                })?;
            }

            let mut out_file = File::create(&out_path).map_err(|e| PluginError::Io {
                message: format!("Failed to create file {}: {e}", out_path.display()),
            })?;

            std::io::copy(&mut entry, &mut out_file).map_err(|e| PluginError::Io {
                message: format!("Failed to extract file {}: {e}", out_path.display()),
            })?;
        }
    }

    let installed_host_libraries: Vec<String> = host_relative_paths
        .into_iter()
        .map(|rel| destination_dir.join(rel).to_string_lossy().to_string())
        .collect();

    Ok(PluginInstallResult {
        destination_dir: destination_dir.to_string_lossy().to_string(),
        installed_host_libraries,
    })
}
