// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::fs;
use std::path::Path;

use object::{read::Object, File, ObjectSymbol};

use crate::error::PluginError;

/// Returns the expected native dynamic library extension for the current platform (without leading dot).
pub fn native_library_extension() -> &'static str {
    if cfg!(target_os = "windows") {
        "dll"
    } else if cfg!(target_os = "macos") || cfg!(target_os = "ios") {
        "dylib"
    } else {
        "so"
    }
}

/// Checks if a file path has the expected dynamic library extension for the current platform.
pub fn is_native_library_path(path: &Path) -> bool {
    path.extension()
        .and_then(|ext| ext.to_str())
        .map(|ext| ext.eq_ignore_ascii_case(native_library_extension()))
        .unwrap_or(false)
}

/// Parses the binary export table (PE exports, Mach-O exports, or ELF dynamic exports)
/// and extracts all exported function and symbol names.
pub fn inspect_binary_exports(bytes: &[u8]) -> Result<Vec<String>, PluginError> {
    let file = File::parse(bytes).map_err(|e| PluginError::LibraryLoadFailed {
        message: format!("Failed to parse binary object: {e}"),
    })?;

    let mut exports = Vec::new();

    if let Ok(exp_list) = file.exports() {
        for exp in exp_list {
            if let Ok(name) = std::str::from_utf8(exp.name()) {
                exports.push(name.to_string());
            }
        }
    }

    if exports.is_empty() {
        for sym in file.dynamic_symbols() {
            if let Ok(name) = sym.name() {
                if !name.is_empty() {
                    exports.push(name.to_string());
                }
            }
        }
    }

    Ok(exports)
}

/// Statically checks if the binary exports the Pixeval extension entry points
/// (`GetExtensionsHost` for COM or `pixeval_plugin_metadata` for C-ABI).
///
/// Unlike text pattern searching, this parses the actual object export directory,
/// preventing false positives from string literals or comments.
pub fn has_extension_entry_point(bytes: &[u8]) -> bool {
    let file = match File::parse(bytes) {
        Ok(f) => f,
        Err(_) => return false,
    };

    if let Ok(exports) = file.exports() {
        for exp in exports {
            let name = exp.name();
            if name == b"GetExtensionsHost" || name == b"pixeval_plugin_metadata" {
                return true;
            }
        }
    }

    for sym in file.dynamic_symbols() {
        if let Ok(name) = sym.name() {
            if name == "GetExtensionsHost" || name == "pixeval_plugin_metadata" {
                return true;
            }
        }
    }

    false
}

/// Reads the file and statically inspects whether it exports the extension entry points.
pub fn inspect_file_entry_point(path: &Path) -> Result<bool, PluginError> {
    if !path.is_file() {
        return Ok(false);
    }

    let bytes = fs::read(path).map_err(|e| PluginError::Io {
        message: format!("Failed to read file {}: {e}", path.display()),
    })?;

    Ok(has_extension_entry_point(&bytes))
}
