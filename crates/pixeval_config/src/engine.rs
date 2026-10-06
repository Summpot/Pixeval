// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::fs;
use std::path::Path;

use crate::error::ConfigError;
use crate::migration::migrate_yaml_str;

#[derive(Clone, uniffi::Object)]
pub struct ConfigEngine;

#[uniffi::export]
impl ConfigEngine {
    #[uniffi::constructor]
    pub fn new() -> Self {
        Self
    }

    pub fn migrate_yaml(&self, legacy_yaml: String) -> Result<String, ConfigError> {
        migrate_yaml_str(&legacy_yaml)
    }

    pub fn validate_and_normalize_yaml(&self, yaml: String) -> Result<String, ConfigError> {
        let val: serde_yaml::Value = serde_yaml::from_str(&yaml)?;
        let out = serde_yaml::to_string(&val)?;
        Ok(out)
    }

    pub fn load_from_file(&self, path: String) -> Result<String, ConfigError> {
        let raw = fs::read_to_string(&path)?;
        migrate_yaml_str(&raw)
    }

    pub fn save_to_file(&self, path: String, content: String) -> Result<(), ConfigError> {
        let target_path = Path::new(&path);
        if let Some(parent) = target_path.parent() {
            if !parent.as_os_str().is_empty() {
                fs::create_dir_all(parent)?;
            }
        }

        let tmp_path = format!("{path}.tmp");
        fs::write(&tmp_path, content)?;
        fs::rename(&tmp_path, target_path)?;
        Ok(())
    }
}
