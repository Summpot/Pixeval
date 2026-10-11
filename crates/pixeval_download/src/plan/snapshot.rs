// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::engine::state::DownloadState;

#[derive(uniffi::Record, Clone, Debug, PartialEq)]
pub struct DownloadItemSnapshot {
    pub key: crate::engine::key::DownloadTaskKey,
    pub title: String,
    pub author: String,
    pub thumbnail_url: String,
    pub website_uri: String,
    pub app_uri: String,
    pub state: DownloadState,
    pub progress_percentage: f64,
    pub active_count: u32,
    pub completed_count: u32,
    pub error_count: u32,
    pub error_message: Option<String>,
    pub open_destination: String,
    pub is_processing: bool,
    pub artwork_id: String,
    pub work_subscription_id: i64,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq)]
pub struct DownloadFolderMeta {
    pub subscription_id: i64,
    pub display_name: String,
    pub avatar_url: String,
    pub subscription_type: u32,
    pub work_kind: u32,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq)]
pub struct DownloadFolderSnapshot {
    pub subscription_id: i64,
    pub display_name: String,
    pub avatar_url: String,
    pub subscription_type: u32,
    pub work_kind: u32,
    pub items: Vec<DownloadItemSnapshot>,
    pub total_count: u32,
    pub active_count: u32,
    pub completed_count: u32,
    pub error_count: u32,
    pub progress_percentage: f64,
    pub current_state: DownloadState,
    pub is_fetching: bool,
    pub fetched_count: u32,
    pub retry_at_timestamp: Option<i64>,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq, Default)]
pub struct DownloadPageSnapshot {
    pub ordinary_items: Vec<DownloadItemSnapshot>,
    pub folders: Vec<DownloadFolderSnapshot>,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq)]
pub struct DownloadPolicy {
    pub overwrite: bool,
    pub illustration_format: String,
    pub ugoira_format: String,
    pub novel_format: String,
}

impl Default for DownloadPolicy {
    fn default() -> Self {
        Self {
            overwrite: true,
            illustration_format: "Original".to_string(),
            ugoira_format: "Original".to_string(),
            novel_format: "OriginalTxt".to_string(),
        }
    }
}

#[derive(uniffi::Record, Clone, Debug, PartialEq)]
pub struct ExternalImageRequest {
    pub artwork_id: String,
    pub title: String,
    pub author: String,
    pub thumbnail_url: String,
    pub website_uri: String,
    pub app_uri: String,
    pub original_url: String,
    pub path_macro: String,
    pub base_dir: String,
    pub payload_json: String,
    pub serialize_key: String,
    pub work_subscription_id: i64,
    pub subscription_type: String,
    pub is_r18: bool,
    pub is_r18g: bool,
    pub create_date: String,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq, Default)]
pub struct PlannedDownloadProbe {
    pub destination: String,
    pub probe_paths: Vec<String>,
}

#[derive(Clone, Debug, Default)]
pub struct FolderFetchState {
    pub is_fetching: bool,
    pub fetched_count: u32,
    pub retry_at_timestamp: Option<i64>,
}

pub fn state_code(state: DownloadState) -> u32 {
    match state {
        DownloadState::Queued => 1,
        DownloadState::Running => 2,
        DownloadState::Paused => 3,
        DownloadState::Cancelled => 4,
        DownloadState::Error => 5,
        DownloadState::Pending => 6,
        DownloadState::Completed => 7,
    }
}

pub fn state_from_code(code: u32) -> DownloadState {
    match code {
        2 => DownloadState::Running,
        3 => DownloadState::Paused,
        4 => DownloadState::Cancelled,
        5 => DownloadState::Error,
        6 => DownloadState::Pending,
        7 => DownloadState::Completed,
        _ => DownloadState::Queued,
    }
}

pub fn aggregate_items(items: &[DownloadItemSnapshot]) -> (DownloadState, f64, u32, u32, u32) {
    if items.is_empty() {
        return (DownloadState::Completed, 100.0, 0, 0, 0);
    }

    let mut active_count = 0u32;
    let mut completed_count = 0u32;
    let mut error_count = 0u32;
    let mut sum_progress = 0.0f64;
    let mut has_error = false;
    let mut has_cancelled = false;
    let mut has_paused = false;
    let mut has_running = false;
    let mut has_queued = false;
    let mut has_pending = false;

    for item in items {
        active_count += item.active_count;
        completed_count += item.completed_count;
        error_count += item.error_count;
        sum_progress += item.progress_percentage;
        match item.state {
            DownloadState::Error => has_error = true,
            DownloadState::Cancelled => has_cancelled = true,
            DownloadState::Paused => has_paused = true,
            DownloadState::Running => has_running = true,
            DownloadState::Queued => has_queued = true,
            DownloadState::Pending => has_pending = true,
            DownloadState::Completed => {}
        }
    }

    let state = if has_error {
        DownloadState::Error
    } else if has_cancelled {
        DownloadState::Cancelled
    } else if has_paused {
        DownloadState::Paused
    } else if has_running {
        DownloadState::Running
    } else if has_queued {
        DownloadState::Queued
    } else if has_pending {
        DownloadState::Pending
    } else {
        DownloadState::Completed
    };

    (
        state,
        sum_progress / items.len() as f64,
        active_count,
        completed_count,
        error_count,
    )
}

#[uniffi::export(callback_interface)]
pub trait DownloadPageCallback: Send + Sync {
    fn on_page_snapshot(&self, snapshot: DownloadPageSnapshot);
}

#[uniffi::export(callback_interface)]
pub trait DownloadFormatEncoder: Send + Sync {
    /// Returns an error message. An empty string means success.
    fn encode_static_image(&self, source_path: String, destination_path: String, extension: String) -> String;
    fn encode_animated(&self, frame_paths: Vec<String>, delays_ms: Vec<u32>, destination_path: String, extension: String) -> String;
    fn encode_novel(&self, text: String, image_paths: Vec<String>, destination_path: String, extension: String) -> String;
}
