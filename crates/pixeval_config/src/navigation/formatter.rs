// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::error::ConfigError;
use crate::navigation::models::NavigationYamlSettings;

pub fn format_navigation_yaml(settings: &NavigationYamlSettings) -> Result<String, ConfigError> {
    let yaml = serde_yaml::to_string(settings)?;
    
    // Ensure clean trailing newline
    let trimmed = yaml.trim_end();
    if trimmed.is_empty() {
        Ok(String::new())
    } else {
        Ok(format!("{trimmed}\n"))
    }
}
