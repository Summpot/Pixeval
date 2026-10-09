// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Copy, Clone, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum SubscriptionStatus {
    Idle,
    Fetching,
    Completed,
    Failed,
    Cancelled,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct SubscriptionFetchState {
    pub subscription_id: i64,
    pub page: u32,
    pub total_fetched: u32,
    pub status: SubscriptionStatus,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct SubscriptionDownloadItem {
    pub artwork_id: String,
    pub destination: String,
    pub work_subscription_id: i64,
    pub title: String,
    pub payload_json: String,
    pub is_novel: bool,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct SubscriptionSyncConfig {
    pub download_path_macro: String,
    pub base_download_dir: String,
    pub overwrite: bool,
    pub duplicate_stop_threshold: u32,
    pub my_user_id: Option<i64>,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum SyncRequestKind {
    All,
    Single { subscription_id: i64 },
}

#[derive(Clone, Copy, Debug, PartialEq, uniffi::Record)]
pub struct FolderTaskItemState {
    pub state: u32,
    pub progress_percentage: f64,
    pub active_count: u32,
    pub completed_count: u32,
    pub error_count: u32,
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct SubscriptionFolderSnapshot {
    pub subscription_id: i64,
    pub total_count: u32,
    pub active_count: u32,
    pub completed_count: u32,
    pub error_count: u32,
    pub progress_percentage: f64,
    pub current_state: u32,
    pub is_fetching: bool,
    pub fetched_count: u32,
    pub retry_at_timestamp: Option<i64>,
}

#[uniffi::export(callback_interface)]
pub trait SubscriptionProgressCallback: Send + Sync {
    fn on_fetch_state_changed(&self, state: SubscriptionFetchState);
    fn on_subscription_updated(&self, subscription_id: i64, name: String, account: String, avatar_url: String);
    fn on_item_fetched(&self, item: SubscriptionDownloadItem);
    fn on_duplicate_stopped(&self, subscription_id: i64, duplicate_count: u32);
    fn on_sync_finished(&self);
}

