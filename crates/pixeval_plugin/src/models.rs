// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, uniffi::Record)]
pub struct PluginExtensionDescriptor {
    pub kind: String,
    pub label: String,
    pub description: String,
    pub identifier: String,
}

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

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, uniffi::Record)]
pub struct DiscoveredPlugin {
    pub library_path: String,
    pub file_name: String,
    pub relative_path: String,
}
