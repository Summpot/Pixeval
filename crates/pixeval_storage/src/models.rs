use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize, uniffi::Record)]
pub struct WorkMetadata {
    pub id: String,
    pub title: String,
    pub work_type: String,
    pub author_id: String,
    pub author_name: String,
    pub author_account: String,
    pub author_avatar: String,
    pub thumb_url: String,
    pub original_url: Option<String>,
    pub total_bookmarks: i64,
    pub total_views: i64,
    pub create_date: String,
    pub page_count: i32,
    pub x_restrict: u32,
    pub is_ai: bool,
    pub tags: Vec<String>,
    pub extra_json: Option<String>,
}

#[uniffi::export(callback_interface)]
pub trait StorageObserver: Send + Sync {
    fn on_browse_history_changed(&self);
    fn on_watch_later_changed(&self);
    fn on_download_history_changed(&self);
    fn on_search_history_changed(&self);
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct SearchHistoryRecord {
    pub history_entry_id: i64,
    pub value: String,
    pub translated_name: Option<String>,
    pub time: String,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct BrowseHistoryRecord {
    pub history_entry_id: i64,
    pub id: String,
    pub serialize_key: Option<String>,
    pub work_key: String,
    pub payload_json: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct WatchLaterRecord {
    pub history_entry_id: i64,
    pub id: String,
    pub serialize_key: Option<String>,
    pub work_key: String,
    pub payload_json: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct DownloadHistoryRecord {
    pub history_entry_id: i64,
    pub id: String,
    pub serialize_key: Option<String>,
    pub destination: String,
    pub state: u32,
    pub format_token: Option<String>,
    pub error_message: Option<String>,
    pub payload_json: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct SubscriptionDownloadHistoryRecord {
    pub history_entry_id: i64,
    pub id: String,
    pub serialize_key: Option<String>,
    pub destination: String,
    pub state: u32,
    pub format_token: Option<String>,
    pub error_message: Option<String>,
    pub work_subscription_id: i64,
    pub artwork_id: String,
    pub payload_json: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct WorkSubscriptionRecord {
    pub history_entry_id: i64,
    pub id: i64,
    pub subscription_type: u32,
    pub work_kind: u32,
    pub title: String,
    pub author: String,
    pub avatar: String,
    pub last_check_time: String,
    pub last_work_id: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct BlockedUserRecord {
    pub history_entry_id: i64,
    pub id: i64,
    pub user_name: String,
    pub avatar_url: String,
    pub account: String,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct LoginUserRecord {
    pub history_entry_id: i64,
    pub user_id: i64,
    pub name: String,
    pub account: String,
    pub mail_address: String,
    pub is_premium: bool,
    pub x_restrict: u32,
    pub is_mail_authorized: bool,
    pub require_policy_agreement: bool,
    pub avatar16_url: String,
    pub avatar50_url: String,
    pub avatar170_url: String,
    pub refresh_token: String,
}
