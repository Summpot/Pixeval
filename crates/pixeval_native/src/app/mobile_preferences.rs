use pixeval_mako::MakoConfigurationDto;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

#[derive(Clone, Debug, Serialize, Deserialize, uniffi::Record)]
#[serde(default)]
pub struct MobilePreferences {
    pub language: String,
    pub dark_theme: bool,
    pub domain_fronting_enabled: bool,
    pub split_delay_ms: u64,
    pub proxy_url: Option<String>,
    pub mirror_host: Option<String>,
    pub host_ips: HashMap<String, Vec<String>>,
    pub download_path_macro: String,
    pub download_concurrency: u32,
    pub overwrite: bool,
    pub illustration_format: String,
    pub ugoira_format: String,
    pub novel_format: String,
    pub subscription_interval_seconds: u64,
    pub mcp_port: u16,
    pub mcp_write_tools: bool,
}
impl Default for MobilePreferences {
    fn default() -> Self {
        let config = MakoConfigurationDto::default();
        Self {
            language: String::new(),
            dark_theme: false,
            domain_fronting_enabled: config.domain_fronting_enabled,
            split_delay_ms: config.split_delay_ms,
            proxy_url: config.proxy_url,
            mirror_host: config.mirror_host,
            host_ips: config.host_ips,
            download_path_macro: "@{id}@{is_pic_set?p@{pic_set_index}:}.@{ext}".into(),
            download_concurrency: 3,
            overwrite: false,
            illustration_format: "Original".into(),
            ugoira_format: "Original".into(),
            novel_format: "OriginalTxt".into(),
            subscription_interval_seconds: 1800,
            mcp_port: 17821,
            mcp_write_tools: false,
        }
    }
}
impl MobilePreferences {
    pub(super) fn configuration(&self) -> MakoConfigurationDto {
        MakoConfigurationDto {
            domain_fronting_enabled: self.domain_fronting_enabled,
            split_delay_ms: self.split_delay_ms,
            proxy_url: self.proxy_url.clone(),
            mirror_host: self.mirror_host.clone(),
            host_ips: self.host_ips.clone(),
            ..Default::default()
        }
    }
    pub(super) fn validate(&self) -> Result<(), String> {
        if self.download_concurrency == 0 || self.download_concurrency > 64 {
            return Err("Download concurrency must be between 1 and 64".into());
        }
        if !pixeval_download::analyze(&self.download_path_macro).is_success {
            return Err("Invalid download path macro".into());
        }
        if self.subscription_interval_seconds < 60 {
            return Err("Subscription interval must be at least 60 seconds".into());
        }
        if self.mcp_port == 0 {
            return Err("MCP requires a nonzero port".into());
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn defaults_and_persisted_settings_are_valid() {
        let prefs = MobilePreferences::default();
        prefs.validate().unwrap();
        let serialized = serde_json::to_string(&prefs).unwrap();
        let restored: MobilePreferences = serde_json::from_str(&serialized).unwrap();
        assert_eq!(restored.download_path_macro, prefs.download_path_macro);
        assert!(!restored.mcp_write_tools);
    }
    #[test]
    fn missing_fields_use_current_defaults() {
        let prefs: MobilePreferences = serde_json::from_str(r#"{"language":"en-US"}"#).unwrap();
        assert_eq!(prefs.language, "en-US");
        assert_eq!(prefs.download_concurrency, 3);
    }
}
