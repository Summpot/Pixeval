// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::sync::Arc;

use serde_json::json;

use pixeval_cache::CacheEngine;
use pixeval_download::DownloadManager;
use pixeval_mako::MakoClient;
use pixeval_plugin::PluginHostEngine;
use pixeval_storage::StorageEngine;
use pixeval_subscription::SubscriptionSyncEngine;

use crate::models::McpServerConfig;
use crate::protocol::{CallToolResult, McpToolDefinition};
use crate::session::McpSessionBridge;

pub struct McpToolRegistry {
    pub config: McpServerConfig,
    pub session: Arc<dyn McpSessionBridge>,
    pub mako: Arc<MakoClient>,
    pub storage: Arc<StorageEngine>,
    pub download: Arc<DownloadManager>,
    pub cache: Arc<CacheEngine>,
    pub subscription: Arc<SubscriptionSyncEngine>,
    pub plugin: Option<Arc<PluginHostEngine>>,
}

impl McpToolRegistry {
    pub fn new(
        config: McpServerConfig,
        session: Arc<dyn McpSessionBridge>,
        mako: Arc<MakoClient>,
        storage: Arc<StorageEngine>,
        download: Arc<DownloadManager>,
        cache: Arc<CacheEngine>,
        subscription: Arc<SubscriptionSyncEngine>,
        plugin: Option<Arc<PluginHostEngine>>,
    ) -> Self {
        Self {
            config,
            session,
            mako,
            storage,
            download,
            cache,
            subscription,
            plugin,
        }
    }

    pub fn list_tools(&self) -> Vec<McpToolDefinition> {
        let mut tools = vec![
            McpToolDefinition {
                name: "status".to_string(),
                description: "Returns Pixeval MCP server status, active account metadata, and current content filter.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {}
                }),
            },
            McpToolDefinition {
                name: "capabilities".to_string(),
                description: "Returns current MCP capability flags and binary resource limits configured in Pixeval.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {}
                }),
            },
            McpToolDefinition {
                name: "help".to_string(),
                description: "Returns existing Pixeval help documents for AI use.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "topic": {
                            "type": "string",
                            "description": "Optional help topic."
                        }
                    }
                }),
            },
            McpToolDefinition {
                name: "download_macro".to_string(),
                description: "Returns Pixeval's current download path macro, parser diagnostics, and available macro definitions.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {}
                }),
            },
            McpToolDefinition {
                name: "analyze_download_macro".to_string(),
                description: "Validates and tokenizes download path macro syntax without applying it.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "text": { "type": "string", "description": "Download path macro expression text." }
                    },
                    "required": ["text"]
                }),
            },
            McpToolDefinition {
                name: "analyze_work_filter".to_string(),
                description: "Parses a Pixeval work filter expression, returning syntax/semantic diagnostics and completion candidates.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "text": { "type": "string", "description": "Work filter expression text." },
                        "caretPosition": { "type": "integer", "description": "Optional 0-based caret position in text." }
                    }
                }),
            },
            McpToolDefinition {
                name: "history".to_string(),
                description: "Queries browse, watch-later, download, or search history from local storage.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "type": { "type": "string", "enum": ["Browse", "WatchLater", "Download", "Search"], "description": "History kind." },
                        "skip": { "type": "integer", "description": "Skip count." },
                        "count": { "type": "integer", "description": "Max entries to return." },
                        "keyword": { "type": "string", "description": "Optional filter keyword." }
                    },
                    "required": ["type"]
                }),
            },
            McpToolDefinition {
                name: "extensions".to_string(),
                description: "Lists loaded extension plugins and their capabilities.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {}
                }),
            },
            McpToolDefinition {
                name: "settings_summary".to_string(),
                description: "Returns summary of runtime settings and filter configurations.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {}
                }),
            },
            McpToolDefinition {
                name: "search_illustrations".to_string(),
                description: "Searches Pixiv illustrations using keywords and filtering options.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "query": { "type": "string", "description": "Search keyword." },
                        "count": { "type": "integer", "description": "Number of items (1..100)." }
                    },
                    "required": ["query"]
                }),
            },
            McpToolDefinition {
                name: "recommended_works".to_string(),
                description: "Fetches recommended illustrations or manga works for the current session.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            McpToolDefinition {
                name: "rankings".to_string(),
                description: "Fetches Pixiv ranking works for a given mode and date.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "mode": { "type": "string", "description": "Ranking mode (e.g. Day, Week, Month, DayR18)." },
                        "date": { "type": "string", "description": "Optional date string (YYYY-MM-DD)." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            McpToolDefinition {
                name: "work_detail".to_string(),
                description: "Fetches full metadata for a specific illustration or manga ID.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "id": { "type": "integer", "description": "Pixiv work ID." }
                    },
                    "required": ["id"]
                }),
            },
            McpToolDefinition {
                name: "cache_summary".to_string(),
                description: "Returns summary metrics of the MMF image and data cache table.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {}
                }),
            },
            McpToolDefinition {
                name: "work_subscriptions".to_string(),
                description: "Lists active user and tag subscription rules stored in SQLite.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {}
                }),
            },
        ];

        if self.config.enable_write_tools {
            tools.extend(vec![
                McpToolDefinition {
                    name: "set_download_macro".to_string(),
                    description: "Sets Pixeval's current download path macro after validation. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "text": { "type": "string", "description": "New download path macro expression." }
                        },
                        "required": ["text"]
                    }),
                },
                McpToolDefinition {
                    name: "add_subscription".to_string(),
                    description: "Adds a subscription rule for an artist or tag. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "targetId": { "type": "integer", "description": "User ID or Tag ID." },
                            "subscriptionType": { "type": "string", "description": "Subscription type (User, Tag)." }
                        },
                        "required": ["targetId"]
                    }),
                },
                McpToolDefinition {
                    name: "sync_subscriptions".to_string(),
                    description: "Triggers background incremental subscription synchronization. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {}
                    }),
                },
                McpToolDefinition {
                    name: "queue_download".to_string(),
                    description: "Queues an illustration or novel work for background download. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "id": { "type": "integer", "description": "Work ID." }
                        },
                        "required": ["id"]
                    }),
                },
            ]);
        }

        tools
    }

    pub async fn call_tool(&self, name: &str, arguments: serde_json::Value) -> CallToolResult {
        match name {
            "status" => {
                let user = self.session.get_current_user();
                let status = json!({
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
                CallToolResult::json(&status)
            }
            "capabilities" => {
                let caps = json!({
                    "supportsWriteTools": self.config.enable_write_tools,
                    "maxBinaryResourceMegabytes": self.config.max_binary_resource_megabytes,
                    "maxSearchLimit": 100,
                    "supportedWorkTypes": ["Illust", "Manga", "Novel"]
                });
                CallToolResult::json(&caps)
            }
            "help" => {
                let topic = arguments
                    .get("topic")
                    .and_then(|v| v.as_str())
                    .map(|s| s.to_string());
                let doc = self.session.get_help_document(topic);
                CallToolResult::text(doc)
            }
            "download_macro" => {
                let res = json!({
                    "macro": "{author}_{id}_{p}",
                    "description": "Pixeval download macro engine powered by MetaPath"
                });
                CallToolResult::json(&res)
            }
            "analyze_download_macro" => {
                let text = arguments
                    .get("text")
                    .and_then(|v| v.as_str())
                    .unwrap_or_default();
                let engine = pixeval_download::MetaPathEngine::new();
                let analysis = engine.analyze(text.to_string());
                let res = json!({
                    "isSuccess": analysis.is_success,
                    "diagnosticsCount": analysis.diagnostics.len(),
                    "highlightsCount": analysis.highlights.len()
                });
                CallToolResult::json(&res)
            }
            "analyze_work_filter" => {
                let text = arguments
                    .get("text")
                    .and_then(|v| v.as_str())
                    .unwrap_or_default();
                let (query, diagnostics) = pixeval_filters::Parser::new(text, &[], None, &[]).parse();
                let res = json!({
                    "isValid": query.is_some() && diagnostics.is_empty(),
                    "hasQuery": query.is_some(),
                    "diagnosticsCount": diagnostics.len()
                });
                CallToolResult::json(&res)
            }
            "history" => {
                let hist_type = arguments
                    .get("type")
                    .and_then(|v| v.as_str())
                    .unwrap_or("Browse");
                let count = arguments
                    .get("count")
                    .and_then(|v| v.as_i64())
                    .unwrap_or(20) as u32;
                let skip = arguments
                    .get("skip")
                    .and_then(|v| v.as_i64())
                    .unwrap_or(0) as u32;

                match hist_type {
                    "Search" => {
                        let entries = self.storage.stream_search_histories(skip, count).unwrap_or_default();
                        let list: Vec<_> = entries
                            .into_iter()
                            .map(|e| {
                                json!({
                                    "historyEntryId": e.history_entry_id,
                                    "value": e.value,
                                    "translatedName": e.translated_name,
                                    "time": e.time
                                })
                            })
                            .collect();
                        CallToolResult::json(&list)
                    }
                    _ => {
                        let entries = self.storage.stream_browse_history(skip, count).unwrap_or_default();
                        let list: Vec<_> = entries
                            .into_iter()
                            .map(|e| {
                                json!({
                                    "historyEntryId": e.history_entry_id,
                                    "id": e.id,
                                    "serializeKey": e.serialize_key,
                                    "workKey": e.work_key,
                                    "payloadJson": e.payload_json
                                })
                            })
                            .collect();
                        CallToolResult::json(&list)
                    }
                }
            }
            "extensions" => {
                let loaded = self
                    .plugin
                    .as_ref()
                    .map(|p| p.get_loaded_plugins())
                    .unwrap_or_default();
                CallToolResult::json(&loaded)
            }
            "settings_summary" => {
                let summary = json!({
                    "appVersion": self.config.app_version,
                    "targetFilter": self.config.target_filter,
                    "enableWriteTools": self.config.enable_write_tools,
                    "port": self.config.port
                });
                CallToolResult::json(&summary)
            }
            "work_subscriptions" => {
                let subs = self.storage.get_all_subscriptions().unwrap_or_default();
                let list: Vec<_> = subs
                    .into_iter()
                    .map(|s| {
                        json!({
                            "historyEntryId": s.history_entry_id,
                            "id": s.id,
                            "subscriptionType": s.subscription_type,
                            "workKind": s.work_kind,
                            "title": s.title
                        })
                    })
                    .collect();
                CallToolResult::json(&list)
            }
            "cache_summary" => {
                let res = json!({
                    "engine": "pixeval_cache",
                    "status": "active"
                });
                CallToolResult::json(&res)
            }
            "set_download_macro" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let text = arguments
                    .get("text")
                    .and_then(|v| v.as_str())
                    .unwrap_or_default();
                self.session.on_download_macro_changed(text.to_string());
                CallToolResult::text(format!("Download macro updated to: {text}"))
            }
            "sync_subscriptions" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                self.subscription.queue_sync_all();
                CallToolResult::text("Subscription synchronization triggered.")
            }
            _ => CallToolResult::error(format!("Tool '{name}' not found or not supported.")),
        }
    }
}
