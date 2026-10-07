uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod error;
pub mod models;
pub mod protocol;
pub mod resources;
pub mod server;
pub mod session;
pub mod tools;

pub use error::*;
pub use models::*;
pub use protocol::*;
pub use resources::*;
pub use server::*;
pub use session::*;
pub use tools::*;

#[cfg(test)]
mod tests {
    use super::*;
    use pixeval_cache::CacheEngine;
    use pixeval_download::DownloadManager;
    use pixeval_mako::{MakoClient, MakoConfigurationDto};
    use pixeval_plugin::PluginHostEngine;
    use pixeval_storage::StorageEngine;
    use pixeval_subscription::SubscriptionSyncEngine;
    use std::sync::Arc;

    #[derive(Clone)]
    struct MockSessionBridge {
        user: Option<McpSessionUserInfo>,
    }

    impl McpSessionBridge for MockSessionBridge {
        fn get_current_user(&self) -> Option<McpSessionUserInfo> {
            self.user.clone()
        }

        fn get_help_document(&self, topic: Option<String>) -> String {
            format!("Help doc for: {:?}", topic)
        }

        fn on_download_macro_changed(&self, _macro_text: String) {}

        fn log_event(&self, _level: String, _message: String) {}
    }

    fn create_test_components() -> (
        Arc<MakoClient>,
        Arc<StorageEngine>,
        Arc<DownloadManager>,
        Arc<CacheEngine>,
        Arc<SubscriptionSyncEngine>,
        Option<Arc<PluginHostEngine>>,
    ) {
        let repo_tmp = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .parent()
            .and_then(|p| p.parent())
            .map(|p| p.join("target").join("tmp"))
            .unwrap_or_else(std::env::temp_dir);
        let _ = std::fs::create_dir_all(&repo_tmp);
        let temp_dir = tempfile::Builder::new()
            .prefix("test_mcp_")
            .tempdir_in(&repo_tmp)
            .unwrap_or_else(|_| {
                tempfile::Builder::new()
                    .prefix("test_mcp_")
                    .tempdir()
                    .unwrap()
            });
        let db_path = temp_dir.path().join("test.sqlite");
        let storage = Arc::new(StorageEngine::new(db_path.to_str().unwrap().to_string()).unwrap());

        let mako_config = MakoConfigurationDto::default();
        let mako = MakoClient::new(mako_config).unwrap();

        let download = DownloadManager::new(3, None, None);

        let cache = CacheEngine::new(
            temp_dir.path().join("cache").to_str().unwrap().to_string(),
            1024 * 1024,
            100,
        )
        .unwrap();

        let sub_engine = Arc::new(SubscriptionSyncEngine::new(Some(5), None));
        let plugin_engine = PluginHostEngine::new("5.0.0".to_string());

        (
            mako,
            storage,
            download,
            cache,
            sub_engine,
            Some(plugin_engine),
        )
    }

    #[test]
    fn test_tools_list_generation() {
        let (mako, storage, download, cache, sub_engine, plugin_engine) = create_test_components();
        let config = McpServerConfig {
            port: 52163,
            enable_write_tools: true,
            max_binary_resource_megabytes: 10,
            app_version: "5.0.13".to_string(),
            target_filter: "Safe".to_string(),
        };

        let session = Arc::new(MockSessionBridge {
            user: Some(McpSessionUserInfo {
                id: "123".to_string(),
                name: "TestUser".to_string(),
                account: "test_acc".to_string(),
            }),
        });

        let registry = McpToolRegistry::new(
            config,
            session,
            mako,
            storage,
            download,
            cache,
            sub_engine,
            plugin_engine,
        );

        let tools = registry.list_tools();
        assert!(tools.len() >= 15);
        assert!(tools.iter().any(|t| t.name == "status"));
        assert!(tools.iter().any(|t| t.name == "capabilities"));
        assert!(tools.iter().any(|t| t.name == "set_download_macro"));
    }

    #[tokio::test]
    async fn test_mcp_server_http_lifecycle() {
        let (mako, storage, download, cache, sub_engine, plugin_engine) = create_test_components();
        // Use port 52199 for testing
        let config = McpServerConfig {
            port: 52199,
            enable_write_tools: false,
            max_binary_resource_megabytes: 5,
            app_version: "5.0.13".to_string(),
            target_filter: "Safe".to_string(),
        };

        let session = Box::new(MockSessionBridge {
            user: Some(McpSessionUserInfo {
                id: "999".to_string(),
                name: "Alice".to_string(),
                account: "alice999".to_string(),
            }),
        });

        let server = McpServer::new(
            config,
            session,
            mako,
            storage,
            download,
            cache,
            sub_engine,
            plugin_engine,
        )
        .unwrap();

        server.start().await.unwrap();
        assert!(server.is_running());

        let client = pixeval_maho::MahoHttpClient::new(
            std::sync::Arc::new(pixeval_maho::MahoConfig::default()),
            None,
        );
        let endpoint = server.endpoint();

        // 1. GET /mcp
        let res = client.get(&endpoint).send().await.unwrap();
        assert_eq!(res.status().as_u16(), 200);

        // 2. POST /mcp with initialize
        let init_body = serde_json::json!({
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {}
        });
        let res = client
            .post(&endpoint)
            .json(&init_body)
            .unwrap()
            .send()
            .await
            .unwrap();
        assert_eq!(res.status().as_u16(), 200);
        let val: serde_json::Value = res.json().await.unwrap();
        assert_eq!(val["result"]["serverInfo"]["name"], "Pixeval MCP");

        // 3. POST /mcp with tools/list
        let list_body = serde_json::json!({
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/list",
            "params": {}
        });
        let res = client
            .post(&endpoint)
            .json(&list_body)
            .unwrap()
            .send()
            .await
            .unwrap();
        assert_eq!(res.status().as_u16(), 200);
        let val: serde_json::Value = res.json().await.unwrap();
        let tools = val["result"]["tools"].as_array().unwrap();
        assert!(!tools.is_empty());

        // 4. POST /mcp with tools/call for "status"
        let call_body = serde_json::json!({
            "jsonrpc": "2.0",
            "id": 3,
            "method": "tools/call",
            "params": {
                "name": "status",
                "arguments": {}
            }
        });
        let res = client
            .post(&endpoint)
            .json(&call_body)
            .unwrap()
            .send()
            .await
            .unwrap();
        assert_eq!(res.status().as_u16(), 200);
        let val: serde_json::Value = res.json().await.unwrap();
        assert_eq!(val["id"], 3);

        // Stop server
        server.stop().await.unwrap();
        assert!(!server.is_running());
    }
}
