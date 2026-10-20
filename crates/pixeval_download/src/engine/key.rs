// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use serde::{Deserialize, Serialize};

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub struct DownloadTaskKey {
    pub destination: String,
    pub work_subscription_id: i32,
    pub artwork_id: Option<String>,
}

impl DownloadTaskKey {
    pub fn new_ordinary(destination: impl Into<String>) -> Self {
        Self {
            destination: destination.into(),
            work_subscription_id: 0,
            artwork_id: None,
        }
    }
}
