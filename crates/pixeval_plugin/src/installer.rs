// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::fs;
use std::path::{Path, PathBuf};

use crate::archive::extract_plugin_archive;
use crate::binary::{inspect_file_entry_point, is_native_library_path};
use crate::error::PluginError;
use crate::models::PluginInstallResult;

/// Atomically verifies and installs a plugin package (either a .zip archive or standalone native library)
/// into the designated extensions directory.
pub fn verify_and_install_plugin(
    package_path: &str,
    extensions_dir: &str,
) -> Result<PluginInstallResult, PluginError> {
    let src_path = Path::new(package_path);
    if !src_path.exists() {
        return Err(PluginError::Io {
            message: format!("Plugin package not found: {package_path}"),
        });
    }

    let ext_dir = Path::new(extensions_dir);
    fs::create_dir_all(ext_dir).map_err(|e| PluginError::Io {
        message: format!("Failed to create extensions directory {}: {e}", ext_dir.display()),
    })?;

    if src_path
        .extension()
        .and_then(|e| e.to_str())
        .map(|e| e.eq_ignore_ascii_case("zip"))
        .unwrap_or(false)
    {
        extract_plugin_archive(src_path, ext_dir)
    } else if is_native_library_path(src_path) {
        install_standalone_library(src_path, ext_dir)
    } else {
        Err(PluginError::InstallFailed {
            message: format!("Unsupported plugin package format: {package_path}"),
        })
    }
}

fn install_standalone_library(
    src_path: &Path,
    extensions_dir: &Path,
) -> Result<PluginInstallResult, PluginError> {
    if !inspect_file_entry_point(src_path)? {
        return Err(PluginError::MissingEntryPoint {
            entry_point: "GetExtensionsHost or pixeval_plugin_metadata".to_string(),
        });
    }

    let file_name = src_path
        .file_name()
        .ok_or_else(|| PluginError::InstallFailed {
            message: format!("Invalid source library filename: {}", src_path.display()),
        })?;

    let dest_path = extensions_dir.join(file_name);
    fs::copy(src_path, &dest_path).map_err(|e| PluginError::Io {
        message: format!(
            "Failed to copy {} to {}: {e}",
            src_path.display(),
            dest_path.display()
        ),
    })?;

    // Copy companion manifest if present (e.g. <stem>.json)
    if let Some(stem) = src_path.file_stem() {
        if let Some(parent) = src_path.parent() {
            let manifest_path = parent.join(format!("{}.json", stem.to_string_lossy()));
            if manifest_path.is_file() {
                let dest_manifest = extensions_dir.join(format!("{}.json", stem.to_string_lossy()));
                let _ = fs::copy(&manifest_path, &dest_manifest);
            }
        }
    }

    Ok(PluginInstallResult {
        destination_dir: extensions_dir.to_string_lossy().to_string(),
        installed_host_libraries: vec![dest_path.to_string_lossy().to_string()],
    })
}

/// Determines the safe relative uninstall target for a given host library path.
///
/// If the library resides directly in the extensions directory, the uninstall target is the file itself.
/// If the library resides inside a subdirectory under extensions directory, the target is that subdirectory.
pub fn get_uninstall_target_relative_path(
    host_library_path: &str,
    extensions_dir: &str,
) -> Option<String> {
    let host_path = Path::new(host_library_path);
    let ext_dir = Path::new(extensions_dir);

    // Normalize paths
    let host_parent = host_path.parent()?;
    let is_direct_child = paths_equal(host_parent, ext_dir);

    let target_path = if is_direct_child {
        host_path.to_path_buf()
    } else {
        // Find the top-level directory directly under extensions_dir
        find_first_child_directory_under_root(host_path, ext_dir)?
    };

    let rel_path = target_path.strip_prefix(ext_dir).ok()?;
    let rel_str = rel_path.to_string_lossy().replace('\\', "/");

    if is_safe_relative_target(&rel_str) {
        Some(rel_str)
    } else {
        None
    }
}

/// Resolves a stored relative uninstall target back to an absolute filesystem path within extensions_dir.
pub fn resolve_uninstall_target(
    relative_target: &str,
    extensions_dir: &str,
) -> Option<PathBuf> {
    if !is_safe_relative_target(relative_target) {
        return None;
    }

    let ext_dir = Path::new(extensions_dir);
    let target = ext_dir.join(relative_target);

    // Verify it doesn't escape extensions_dir
    let normalized_target = normalize_path(&target);
    let normalized_ext = normalize_path(ext_dir);

    if normalized_target.starts_with(&normalized_ext) && normalized_target != normalized_ext {
        Some(normalized_target)
    } else {
        None
    }
}

/// Cleans up pending uninstalls by safely removing each scheduled target file or directory.
/// Returns a list of relative target paths that failed to be deleted.
pub fn clean_pending_uninstalls(
    pending_targets: &[String],
    extensions_dir: &str,
) -> Vec<String> {
    let mut failed = Vec::new();

    for target in pending_targets {
        if let Some(target_path) = resolve_uninstall_target(target, extensions_dir) {
            if target_path.exists() {
                let res = if target_path.is_dir() {
                    fs::remove_dir_all(&target_path)
                } else {
                    fs::remove_file(&target_path)
                };

                if res.is_err() {
                    failed.push(target.clone());
                }
            }
        }
    }

    failed
}

fn is_safe_relative_target(rel: &str) -> bool {
    if rel.is_empty() || rel == "." || rel == ".." {
        return false;
    }
    if rel.starts_with('/') || rel.starts_with('\\') || rel.contains(':') {
        return false;
    }
    for seg in rel.split(['/', '\\']) {
        if seg.is_empty() || seg == "." || seg == ".." {
            return false;
        }
    }
    true
}

fn paths_equal(p1: &Path, p2: &Path) -> bool {
    let n1 = normalize_path(p1);
    let n2 = normalize_path(p2);
    if cfg!(target_os = "windows") {
        n1.to_string_lossy().eq_ignore_ascii_case(&n2.to_string_lossy())
    } else {
        n1 == n2
    }
}

fn find_first_child_directory_under_root(path: &Path, root: &Path) -> Option<PathBuf> {
    let mut curr = path;
    while let Some(parent) = curr.parent() {
        if paths_equal(parent, root) {
            return Some(curr.to_path_buf());
        }
        curr = parent;
    }
    None
}

fn normalize_path(p: &Path) -> PathBuf {
    let mut components = Vec::new();
    for c in p.components() {
        match c {
            std::path::Component::ParentDir => {
                components.pop();
            }
            std::path::Component::CurDir => {}
            _ => components.push(c.as_os_str()),
        }
    }
    let mut res = PathBuf::new();
    for c in components {
        res.push(c);
    }
    res
}
