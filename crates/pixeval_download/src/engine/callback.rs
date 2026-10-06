// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::engine::key::DownloadTaskKey;
use crate::engine::state::DownloadState;

#[uniffi::export(callback_interface)]
pub trait DownloadProgressCallback: Send + Sync {
    fn on_progress(
        &self,
        key: DownloadTaskKey,
        progress_percentage: f64,
        downloaded_bytes: u64,
        total_bytes: u64,
    );
    fn on_state_changed(
        &self,
        key: DownloadTaskKey,
        state: DownloadState,
        error_message: Option<String>,
    );
    fn on_completed(&self, key: DownloadTaskKey, destination: String);
}
