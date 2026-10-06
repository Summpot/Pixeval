// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::models::McpSessionUserInfo;

#[uniffi::export(callback_interface)]
pub trait McpSessionBridge: Send + Sync {
    fn get_current_user(&self) -> Option<McpSessionUserInfo>;
    fn get_help_document(&self, topic: Option<String>) -> String;
    fn on_download_macro_changed(&self, macro_text: String);
    fn log_event(&self, level: String, message: String);
}
