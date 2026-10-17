// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use serde::{Deserialize, Serialize};

#[boltffi::data]
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, uniffi::Record)]
pub struct PluginExtensionDescriptor {
    pub kind: String,
    pub label: String,
    pub description: String,
    pub identifier: String,
}

#[boltffi::data]
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, uniffi::Record)]
pub struct PluginMetadata {
    pub id: String,
    pub name: String,
    pub author: String,
    pub version: String,
    pub description: String,
    pub sdk_version: String,
    pub library_path: String,
    pub extensions: Vec<PluginExtensionDescriptor>,
    pub is_active: bool,
}

#[boltffi::data]
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, uniffi::Record)]
pub struct DiscoveredPlugin {
    pub library_path: String,
    pub file_name: String,
    pub relative_path: String,
}

#[boltffi::data]
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, uniffi::Record)]
pub struct PluginInstallResult {
    pub destination_dir: String,
    pub installed_host_libraries: Vec<String>,
}

#[boltffi::data]
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, uniffi::Record)]
pub struct LocalExtensionHost {
    pub library_path: String,
    pub uninstall_target_relative_path: String,
}
