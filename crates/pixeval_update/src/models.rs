// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use serde::Deserialize;

use crate::version::UpdateState;

/// 单个发布工件资产
#[derive(Debug, Clone, uniffi::Record)]
pub struct ReleaseAsset {
    pub name: String,
    pub download_url: String,
    pub size: u64,
    pub content_type: String,
    pub sha256: Option<String>,
}

/// 客户端发布版本信息
#[derive(Debug, Clone, uniffi::Record)]
pub struct AppRelease {
    pub version: String,
    pub tag_name: String,
    pub title: String,
    pub release_notes: String,
    pub published_at: Option<String>,
    pub html_url: String,
    pub is_prerelease: bool,
    pub assets: Vec<ReleaseAsset>,
}

/// 检查更新的结果
#[derive(Debug, Clone, uniffi::Record)]
pub struct UpdateCheckResult {
    pub current_version: String,
    pub update_state: UpdateState,
    pub latest_release: Option<AppRelease>,
    pub all_releases: Vec<AppRelease>,
}

/// 更新网络配置选项
#[derive(Debug, Clone, uniffi::Record)]
pub struct UpdateNetworkOptions {
    pub enable_domain_fronting: bool,
    pub proxy_url: Option<String>,
    pub custom_dns_mappings: HashMap<String, Vec<String>>,
}

// ================= Internal GitHub API Models =================

#[derive(Debug, Deserialize)]
pub(crate) struct RawGitHubRelease {
    pub tag_name: String,
    pub name: Option<String>,
    pub body: Option<String>,
    #[serde(default)]
    pub draft: bool,
    #[serde(default)]
    pub prerelease: bool,
    pub published_at: Option<String>,
    pub html_url: String,
    #[serde(default)]
    pub assets: Vec<RawGitHubAsset>,
}

#[derive(Debug, Deserialize)]
pub(crate) struct RawGitHubAsset {
    pub name: String,
    pub browser_download_url: String,
    pub size: u64,
    pub content_type: Option<String>,
}
