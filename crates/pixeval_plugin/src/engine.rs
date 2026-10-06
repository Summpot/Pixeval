// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::ffi::CStr;
use std::fs;
use std::os::raw::c_char;
use std::path::Path;
use std::sync::Arc;

use parking_lot::RwLock;

use crate::error::PluginError;
use crate::models::{DiscoveredPlugin, PluginMetadata};

struct LoadedPluginInstance {
    metadata: PluginMetadata,
    #[allow(dead_code)]
    library: Option<libloading::Library>,
}

#[derive(uniffi::Object)]
pub struct PluginHostEngine {
    current_sdk_version: String,
    loaded_plugins: Arc<RwLock<HashMap<String, LoadedPluginInstance>>>,
}

#[uniffi::export]
impl PluginHostEngine {
    #[uniffi::constructor]
    pub fn new(current_sdk_version: String) -> Arc<Self> {
        Arc::new(Self {
            current_sdk_version,
            loaded_plugins: Arc::new(RwLock::new(HashMap::new())),
        })
    }

    pub fn current_sdk_version(&self) -> String {
        self.current_sdk_version.clone()
    }

    pub fn enumerate_plugins(&self, directory: String) -> Vec<DiscoveredPlugin> {
        let base = Path::new(&directory);
        if !base.is_dir() {
            return Vec::new();
        }

        let ext = if cfg!(target_os = "windows") {
            "dll"
        } else if cfg!(target_os = "macos") {
            "dylib"
        } else {
            "so"
        };

        let mut results = Vec::new();
        let mut stack = vec![base.to_path_buf()];

        while let Some(current_dir) = stack.pop() {
            if let Ok(entries) = fs::read_dir(&current_dir) {
                for entry in entries.flatten() {
                    let path = entry.path();
                    if path.is_dir() {
                        stack.push(path);
                    } else if path.is_file() {
                        if let Some(e) = path.extension() {
                            if e.to_string_lossy().eq_ignore_ascii_case(ext) {
                                let file_name = path
                                    .file_name()
                                    .map(|n| n.to_string_lossy().to_string())
                                    .unwrap_or_default();
                                let relative_path = path
                                    .strip_prefix(base)
                                    .map(|p| p.to_string_lossy().to_string())
                                    .unwrap_or_else(|_| file_name.clone());

                                results.push(DiscoveredPlugin {
                                    library_path: path.to_string_lossy().to_string(),
                                    file_name,
                                    relative_path,
                                });
                            }
                        }
                    }
                }
            }
        }

        results.sort_by(|a, b| a.relative_path.cmp(&b.relative_path));
        results
    }

    pub fn inspect_library(&self, path: String) -> Result<PluginMetadata, PluginError> {
        let p = Path::new(&path);
        if !p.exists() {
            return Err(PluginError::LibraryLoadFailed {
                message: format!("File does not exist: {path}"),
            });
        }

        // 1. Check for companion metadata JSON: e.g. <lib_name>.json or plugin.json in same dir
        let json_meta = Self::try_read_companion_manifest(p);

        // 2. Try loading dynamic library
        unsafe {
            let lib = libloading::Library::new(p).map_err(|e| PluginError::LibraryLoadFailed {
                message: e.to_string(),
            })?;

            // Try C-ABI metadata export: extern "C" fn() -> *const c_char
            if let Ok(sym) = lib.get::<unsafe extern "C" fn() -> *const c_char>(b"pixeval_plugin_metadata\0") {
                let ptr = sym();
                if !ptr.is_null() {
                    let c_str = CStr::from_ptr(ptr);
                    if let Ok(json_str) = c_str.to_str() {
                        if let Ok(mut meta) = serde_json::from_str::<PluginMetadata>(json_str) {
                            meta.library_path = path.clone();
                            if !self.current_sdk_version.is_empty()
                                && !meta.sdk_version.is_empty()
                                && meta.sdk_version != self.current_sdk_version
                            {
                                return Err(PluginError::OutdatedSdk {
                                    current_version: self.current_sdk_version.clone(),
                                    plugin_version: meta.sdk_version,
                                });
                            }
                            return Ok(meta);
                        }
                    }
                }
            }

            // Check if GetExtensionsHost entry point exists
            let has_get_extensions_host = lib.get::<*const ()>(b"GetExtensionsHost\0").is_ok();

            if let Some(mut meta) = json_meta {
                meta.library_path = path.clone();
                if !self.current_sdk_version.is_empty()
                    && !meta.sdk_version.is_empty()
                    && meta.sdk_version != self.current_sdk_version
                {
                    return Err(PluginError::OutdatedSdk {
                        current_version: self.current_sdk_version.clone(),
                        plugin_version: meta.sdk_version,
                    });
                }
                return Ok(meta);
            }

            if has_get_extensions_host {
                let file_stem = p
                    .file_stem()
                    .map(|s| s.to_string_lossy().to_string())
                    .unwrap_or_else(|| "Unknown".to_string());

                return Ok(PluginMetadata {
                    id: file_stem.clone(),
                    name: file_stem,
                    author: "Native Plugin".to_string(),
                    version: "1.0.0".to_string(),
                    description: "Native COM / C-ABI Extension Host".to_string(),
                    sdk_version: self.current_sdk_version.clone(),
                    library_path: path,
                    extensions: Vec::new(),
                    is_active: true,
                });
            }

            Err(PluginError::MissingEntryPoint {
                entry_point: "GetExtensionsHost or pixeval_plugin_metadata".to_string(),
            })
        }
    }

    pub fn load_plugin(&self, path: String) -> Result<PluginMetadata, PluginError> {
        let metadata = self.inspect_library(path.clone())?;

        unsafe {
            let lib = libloading::Library::new(&path).map_err(|e| PluginError::LibraryLoadFailed {
                message: e.to_string(),
            })?;

            let id = metadata.id.clone();
            self.loaded_plugins.write().insert(
                id,
                LoadedPluginInstance {
                    metadata: metadata.clone(),
                    library: Some(lib),
                },
            );
        }

        Ok(metadata)
    }

    pub fn unload_plugin(&self, plugin_id: String) -> Result<bool, PluginError> {
        let mut lock = self.loaded_plugins.write();
        if lock.remove(&plugin_id).is_some() {
            Ok(true)
        } else {
            Err(PluginError::NotFound { id: plugin_id })
        }
    }

    pub fn get_loaded_plugins(&self) -> Vec<PluginMetadata> {
        self.loaded_plugins
            .read()
            .values()
            .map(|inst| inst.metadata.clone())
            .collect()
    }

    pub fn get_plugin(&self, plugin_id: String) -> Option<PluginMetadata> {
        self.loaded_plugins
            .read()
            .get(&plugin_id)
            .map(|inst| inst.metadata.clone())
    }

    pub fn set_plugin_active(&self, plugin_id: String, active: bool) -> Result<bool, PluginError> {
        let mut lock = self.loaded_plugins.write();
        if let Some(inst) = lock.get_mut(&plugin_id) {
            inst.metadata.is_active = active;
            Ok(true)
        } else {
            Err(PluginError::NotFound { id: plugin_id })
        }
    }

    pub fn register_metadata(&self, metadata: PluginMetadata) {
        let id = metadata.id.clone();
        self.loaded_plugins.write().insert(
            id,
            LoadedPluginInstance {
                metadata,
                library: None,
            },
        );
    }
}

impl PluginHostEngine {
    fn try_read_companion_manifest(lib_path: &Path) -> Option<PluginMetadata> {
        let stem = lib_path.file_stem()?.to_string_lossy();
        let parent = lib_path.parent()?;

        let candidates = [
            parent.join(format!("{stem}.json")),
            parent.join("plugin.json"),
        ];

        for candidate in &candidates {
            if candidate.is_file() {
                if let Ok(content) = fs::read_to_string(candidate) {
                    if let Ok(meta) = serde_json::from_str::<PluginMetadata>(&content) {
                        return Some(meta);
                    }
                }
            }
        }
        None
    }
}
