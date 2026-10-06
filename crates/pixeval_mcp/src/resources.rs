// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::sync::Arc;

use serde_json::json;

use crate::models::McpServerConfig;
use crate::protocol::{McpResourceDefinition, McpResourceTemplate};
use crate::session::McpSessionBridge;

pub struct McpResourceRegistry {
    config: McpServerConfig,
    session: Arc<dyn McpSessionBridge>,
}

impl McpResourceRegistry {
    pub fn new(config: McpServerConfig, session: Arc<dyn McpSessionBridge>) -> Self {
        Self { config, session }
    }

    pub fn list_resources(&self) -> Vec<McpResourceDefinition> {
        vec![McpResourceDefinition {
            uri: "pixeval://me".to_string(),
            name: "me".to_string(),
            description: Some("Current Pixeval account and session state".to_string()),
            mime_type: Some("application/json".to_string()),
        }]
    }

    pub fn list_resource_templates(&self) -> Vec<McpResourceTemplate> {
        vec![
            McpResourceTemplate {
                uri_template: "pixeval://illust/{id}".to_string(),
                name: "illustration".to_string(),
                description: Some("Pixiv illustration details".to_string()),
                mime_type: Some("application/json".to_string()),
            },
            McpResourceTemplate {
                uri_template: "pixeval://illust/{id}/thumbnail/{size}".to_string(),
                name: "illustration_thumbnail".to_string(),
                description: Some("Pixiv illustration thumbnail".to_string()),
                mime_type: Some("application/octet-stream".to_string()),
            },
            McpResourceTemplate {
                uri_template: "pixeval://novel/{id}/thumbnail/{size}".to_string(),
                name: "novel_thumbnail".to_string(),
                description: Some("Pixiv novel thumbnail".to_string()),
                mime_type: Some("application/octet-stream".to_string()),
            },
        ]
    }

    pub fn read_resource(&self, uri: &str) -> Result<serde_json::Value, String> {
        if uri == "pixeval://me" {
            let user = self.session.get_current_user();
            let result = json!({
                "appVersion": self.config.app_version,
                "targetFilter": self.config.target_filter,
                "loggedIn": user.is_some(),
                "currentUser": user.map(|u| json!({
                    "id": u.id,
                    "name": u.name,
                    "account": u.account
                })),
                "port": self.config.port,
                "enableWriteTools": self.config.enable_write_tools
            });

            Ok(json!({
                "contents": [{
                    "uri": uri,
                    "mimeType": "application/json",
                    "text": serde_json::to_string_pretty(&result).unwrap_or_default()
                }]
            }))
        } else {
            Err(format!("Resource '{uri}' not found or direct read not supported."))
        }
    }
}
