// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::net::SocketAddr;
use std::sync::Arc;
use std::time::Instant;

use axum::extract::{Query, State};
use axum::http::{HeaderMap, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::Json;
use parking_lot::RwLock;
use serde::Deserialize;
use serde_json::json;
use tokio::sync::watch;
use uuid::Uuid;

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

#[derive(Clone, Debug)]
pub struct McpSessionRecord {
    pub id: String,
    pub created_at: Instant,
    pub protocol_version: Option<String>,
}

struct ServerState {
    config: McpServerConfig,
    tools: Arc<McpToolRegistry>,
    resources: Arc<McpResourceRegistry>,
    sessions: Arc<RwLock<HashMap<String, McpSessionRecord>>>,
}

#[derive(uniffi::Object)]
pub struct McpServer {
    config: McpServerConfig,
    tools: Arc<McpToolRegistry>,
    resources: Arc<McpResourceRegistry>,
    sessions: Arc<RwLock<HashMap<String, McpSessionRecord>>>,
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
            sessions: Arc::new(RwLock::new(HashMap::new())),
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
            sessions: self.sessions.clone(),
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

#[derive(Deserialize, Default)]
struct McpQueryParams {
    #[serde(rename = "sessionId")]
    session_id: Option<String>,
}

async fn handle_get_mcp(
    State(state): State<Arc<ServerState>>,
    headers: HeaderMap,
) -> Response {
    let accept = headers
        .get("accept")
        .and_then(|v| v.to_str().ok())
        .unwrap_or_default();

    if accept.contains("text/event-stream") {
        // SSE flow: create a new session
        let session_id = Uuid::new_v4().to_string();
        state.sessions.write().insert(
            session_id.clone(),
            McpSessionRecord {
                id: session_id.clone(),
                created_at: Instant::now(),
                protocol_version: None,
            },
        );

        let endpoint_data = format!("event: endpoint\ndata: /mcp?sessionId={session_id}\n\n");
        return Response::builder()
            .status(StatusCode::OK)
            .header("content-type", "text/event-stream")
            .header("cache-control", "no-cache")
            .header("connection", "keep-alive")
            .body(axum::body::Body::from(endpoint_data))
            .unwrap_or_else(|_| StatusCode::INTERNAL_SERVER_ERROR.into_response());
    }

    let user = state.tools.session.get_current_user();
    let body = json!({
        "status": "online",
        "name": "Pixeval MCP",
        "version": state.config.app_version,
        "loggedIn": user.is_some()
    });
    Json(body).into_response()
}

async fn handle_post_mcp(
    State(state): State<Arc<ServerState>>,
    headers: HeaderMap,
    Query(query): Query<McpQueryParams>,
    Json(req): Json<JsonRpcRequest>,
) -> Response {
    let id = req.id.clone().unwrap_or(serde_json::Value::Null);

    // Track or update session if sessionId provided in query or header
    let session_id = query
        .session_id
        .or_else(|| headers.get("mcp-session-id").and_then(|v| v.to_str().ok().map(|s| s.to_string())));

    // Notifications (no ID required)
    if req.method == "notifications/initialized" {
        return StatusCode::NO_CONTENT.into_response();
    }

    let response = match req.method.as_str() {
        "initialize" => {
            // Echo the requested protocol version back to the client
            let client_protocol_version = req
                .params
                .as_ref()
                .and_then(|p| p.get("protocolVersion"))
                .and_then(|v| v.as_str())
                .unwrap_or("2024-11-05");

            if let Some(ref sid) = session_id {
                let mut sessions = state.sessions.write();
                if let Some(record) = sessions.get_mut(sid) {
                    record.protocol_version = Some(client_protocol_version.to_string());
                } else {
                    sessions.insert(
                        sid.clone(),
                        McpSessionRecord {
                            id: sid.clone(),
                            created_at: Instant::now(),
                            protocol_version: Some(client_protocol_version.to_string()),
                        },
                    );
                }
            }

            let res = json!({
                "protocolVersion": client_protocol_version,
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

    let accept = headers
        .get("accept")
        .and_then(|v| v.to_str().ok())
        .unwrap_or_default();

    if accept.contains("text/event-stream") {
        let json_text = serde_json::to_string(&response).unwrap_or_default();
        let sse_chunk = format!("event: message\ndata: {json_text}\n\n");
        Response::builder()
            .status(StatusCode::OK)
            .header("content-type", "text/event-stream")
            .header("cache-control", "no-cache")
            .body(axum::body::Body::from(sse_chunk))
            .unwrap_or_else(|_| StatusCode::INTERNAL_SERVER_ERROR.into_response())
    } else {
        Json(response).into_response()
    }
}
