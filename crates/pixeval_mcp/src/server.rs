// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::net::SocketAddr;
use std::sync::Arc;

use axum::extract::State;
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::Json;
use parking_lot::RwLock;
use serde_json::json;
use tokio::sync::watch;

use pixeval_cache::CacheEngine;
use pixeval_download::DownloadManager;
use pixeval_mako::MakoClient;
use pixeval_plugin::PluginHostEngine;
use pixeval_storage::StorageEngine;
use pixeval_subscription::SubscriptionSyncEngine;

use crate::error::McpError;
use crate::models::{McpServerConfig, McpServerStatus};
use crate::protocol::{JsonRpcRequest, JsonRpcResponse};
use crate::resources::McpResourceRegistry;
use crate::session::McpSessionBridge;
use crate::tools::McpToolRegistry;

struct ServerState {
    config: McpServerConfig,
    tools: Arc<McpToolRegistry>,
    resources: Arc<McpResourceRegistry>,
}

#[derive(uniffi::Object)]
pub struct McpServer {
    config: McpServerConfig,
    tools: Arc<McpToolRegistry>,
    resources: Arc<McpResourceRegistry>,
    shutdown_tx: Arc<RwLock<Option<watch::Sender<bool>>>>,
    is_running: Arc<RwLock<bool>>,
}

struct SessionBridgeWrapper(Box<dyn McpSessionBridge>);

impl McpSessionBridge for SessionBridgeWrapper {
    fn get_current_user(&self) -> Option<crate::models::McpSessionUserInfo> {
        self.0.get_current_user()
    }
    fn get_help_document(&self, topic: Option<String>) -> String {
        self.0.get_help_document(topic)
    }
    fn on_download_macro_changed(&self, macro_text: String) {
        self.0.on_download_macro_changed(macro_text)
    }
    fn log_event(&self, level: String, message: String) {
        self.0.log_event(level, message)
    }
}

#[uniffi::export(async_runtime = "tokio")]
impl McpServer {
    #[uniffi::constructor]
    pub fn new(
        config: McpServerConfig,
        session: Box<dyn McpSessionBridge>,
        mako: Arc<MakoClient>,
        storage: Arc<StorageEngine>,
        download: Arc<DownloadManager>,
        cache: Arc<CacheEngine>,
        subscription: Arc<SubscriptionSyncEngine>,
        plugin: Option<Arc<PluginHostEngine>>,
    ) -> Result<Arc<Self>, McpError> {
        let session: Arc<dyn McpSessionBridge> = Arc::new(SessionBridgeWrapper(session));
        let tools = Arc::new(McpToolRegistry::new(
            config.clone(),
            session.clone(),
            mako,
            storage,
            download,
            cache,
            subscription,
            plugin,
        ));

        let resources = Arc::new(McpResourceRegistry::new(
            config.clone(),
            session,
        ));

        Ok(Arc::new(Self {
            config,
            tools,
            resources,
            shutdown_tx: Arc::new(RwLock::new(None)),
            is_running: Arc::new(RwLock::new(false)),
        }))
    }

    pub async fn start(&self) -> Result<(), McpError> {
        if *self.is_running.read() {
            return Ok(());
        }

        let port = self.config.port;
        let addr = SocketAddr::from(([127, 0, 0, 1], port));
        let listener = tokio::net::TcpListener::bind(addr).await.map_err(|e| {
            McpError::Bind {
                message: format!("Failed to bind to {addr}: {e}"),
            }
        })?;

        let (tx, mut rx) = watch::channel(false);
        *self.shutdown_tx.write() = Some(tx);
        *self.is_running.write() = true;

        let state = Arc::new(ServerState {
            config: self.config.clone(),
            tools: self.tools.clone(),
            resources: self.resources.clone(),
        });

        let app = axum::Router::new()
            .route("/mcp", post(handle_post_mcp))
            .route("/mcp", get(handle_get_mcp))
            .with_state(state);

        let is_running_clone = self.is_running.clone();

        tokio::spawn(async move {
            axum::serve(listener, app)
                .with_graceful_shutdown(async move {
                    let _ = rx.changed().await;
                })
                .await
                .ok();

            *is_running_clone.write() = false;
        });

        Ok(())
    }

    pub async fn stop(&self) -> Result<(), McpError> {
        if let Some(tx) = self.shutdown_tx.write().take() {
            let _ = tx.send(true);
        }
        *self.is_running.write() = false;
        Ok(())
    }

    pub fn is_running(&self) -> bool {
        *self.is_running.read()
    }

    pub fn port(&self) -> u16 {
        self.config.port
    }

    pub fn endpoint(&self) -> String {
        format!("http://127.0.0.1:{}/mcp", self.config.port)
    }

    pub fn get_status(&self) -> McpServerStatus {
        let user = self.tools.session.get_current_user();
        McpServerStatus {
            port: self.config.port,
            endpoint: self.endpoint(),
            is_running: self.is_running(),
            app_version: self.config.app_version.clone(),
            enable_write_tools: self.config.enable_write_tools,
            logged_in: user.is_some(),
            user_id: user.as_ref().map(|u| u.id.clone()),
            user_name: user.as_ref().map(|u| u.name.clone()),
        }
    }
}

async fn handle_get_mcp(State(state): State<Arc<ServerState>>) -> impl IntoResponse {
    let user = state.tools.session.get_current_user();
    let body = json!({
        "status": "online",
        "name": "Pixeval MCP",
        "version": state.config.app_version,
        "loggedIn": user.is_some()
    });
    Json(body)
}

async fn handle_post_mcp(
    State(state): State<Arc<ServerState>>,
    Json(req): Json<JsonRpcRequest>,
) -> Response {
    let id = req.id.clone().unwrap_or(serde_json::Value::Null);

    // Notifications (no ID required)
    if req.method == "notifications/initialized" {
        return StatusCode::NO_CONTENT.into_response();
    }

    let response = match req.method.as_str() {
        "initialize" => {
            let res = json!({
                "protocolVersion": "2024-11-05",
                "capabilities": {
                    "tools": { "listChanged": false },
                    "resources": { "subscribe": false, "listChanged": false }
                },
                "serverInfo": {
                    "name": "Pixeval MCP",
                    "version": state.config.app_version
                },
                "instructions": "Expose the running Pixeval desktop session as local MCP tools. Tools follow Pixeval's current account, content filter, network settings, and MCP permission settings."
            });
            JsonRpcResponse::success(id, res)
        }
        "ping" => JsonRpcResponse::success(id, json!({})),
        "tools/list" => {
            let tools = state.tools.list_tools();
            JsonRpcResponse::success(id, json!({ "tools": tools }))
        }
        "tools/call" => {
            let params = req.params.unwrap_or(json!({}));
            let name = params
                .get("name")
                .and_then(|v| v.as_str())
                .unwrap_or_default();
            let args = params.get("arguments").cloned().unwrap_or(json!({}));

            let result = state.tools.call_tool(name, args).await;
            JsonRpcResponse::success(id, serde_json::to_value(&result).unwrap_or(json!({})))
        }
        "resources/list" => {
            let resources = state.resources.list_resources();
            JsonRpcResponse::success(id, json!({ "resources": resources }))
        }
        "resources/templates/list" => {
            let templates = state.resources.list_resource_templates();
            JsonRpcResponse::success(id, json!({ "resourceTemplates": templates }))
        }
        "resources/read" => {
            let params = req.params.unwrap_or(json!({}));
            let uri = params
                .get("uri")
                .and_then(|v| v.as_str())
                .unwrap_or_default();

            match state.resources.read_resource(uri) {
                Ok(res) => JsonRpcResponse::success(id, res),
                Err(err) => JsonRpcResponse::error(id, -32602, err),
            }
        }
        other => JsonRpcResponse::error(id, -32601, format!("Method '{other}' not found.")),
    };

    Json(response).into_response()
}
