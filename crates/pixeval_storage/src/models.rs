// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

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
