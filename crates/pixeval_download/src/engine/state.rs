// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use serde::{Deserialize, Serialize};

#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum DownloadState {
    Queued,
    Running,
    Paused,
    Cancelled,
    Error,
    Pending,
    Completed,
}

impl DownloadState {
    pub fn is_active(&self) -> bool {
        matches!(self, DownloadState::Queued | DownloadState::Running)
    }

    pub fn is_terminal(&self) -> bool {
        matches!(
            self,
            DownloadState::Cancelled | DownloadState::Error | DownloadState::Completed
        )
    }
}
