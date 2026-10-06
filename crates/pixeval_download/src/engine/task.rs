// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::engine::key::DownloadTaskKey;
use crate::engine::state::DownloadState;

#[derive(Clone, Debug)]
pub struct DownloadTaskItem {
    pub key: DownloadTaskKey,
    pub url: String,
    pub destination: String,
    pub temp_destination: String,
    pub overwrite: bool,
    pub state: DownloadState,
    pub progress_percentage: f64,
    pub downloaded_bytes: u64,
    pub total_bytes: u64,
    pub error_message: Option<String>,
}

impl DownloadTaskItem {
    pub fn new(key: DownloadTaskKey, url: String, destination: String, overwrite: bool) -> Self {
        let temp_destination = format!("{destination}.pixevaldownloading");
        Self {
            key,
            url,
            destination,
            temp_destination,
            overwrite,
            state: DownloadState::Queued,
            progress_percentage: 0.0,
            downloaded_bytes: 0,
            total_bytes: 0,
            error_message: None,
        }
    }

    pub fn to_progress_info(&self) -> DownloadProgressInfo {
        DownloadProgressInfo {
            key: self.key.clone(),
            state: self.state,
            progress_percentage: self.progress_percentage,
            downloaded_bytes: self.downloaded_bytes,
            total_bytes: self.total_bytes,
            error_message: self.error_message.clone(),
        }
    }
}

#[derive(uniffi::Record, Clone, Debug, PartialEq)]
pub struct DownloadProgressInfo {
    pub key: DownloadTaskKey,
    pub state: DownloadState,
    pub progress_percentage: f64,
    pub downloaded_bytes: u64,
    pub total_bytes: u64,
    pub error_message: Option<String>,
}
