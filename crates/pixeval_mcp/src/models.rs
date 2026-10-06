// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize, uniffi::Record)]
pub struct McpServerConfig {
    pub port: u16,
    pub enable_write_tools: bool,
    pub max_binary_resource_megabytes: i32,
    pub app_version: String,
    pub target_filter: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, uniffi::Record)]
pub struct McpServerStatus {
    pub port: u16,
    pub endpoint: String,
    pub is_running: bool,
    pub app_version: String,
    pub enable_write_tools: bool,
    pub logged_in: bool,
    pub user_id: Option<String>,
    pub user_name: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, uniffi::Record)]
pub struct McpSessionUserInfo {
    pub id: String,
    pub name: String,
    pub account: String,
}
