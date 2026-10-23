//! An explicitly owned mobile session. It never opens the legacy APP_STATE's
//! temporary database or shares the Avalonia desktop profile.
pub use super::mobile_preferences::MobilePreferences;
use parking_lot::Mutex;
use pixeval_cache::ImageCache;
use pixeval_download::{DownloadManager, DownloadNetworkOptions};
use pixeval_mako::{MakoClient, MakoSessionCallback, WorkEntry};
use pixeval_storage::StorageEngine;
use pixeval_subscription::{SubscriptionSync, SubscriptionSyncConfig};
use std::path::PathBuf;
use std::sync::{Arc, LazyLock, Weak};

use super::api::events::SessionEvent;
use super::events::{EventHub, SessionEventReceiver};

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct MobileDirectories {
    pub data: String,
    pub cache: String,
    pub downloads: String,
}

#[derive(Clone, Debug, uniffi::Record)]
pub struct MobileCapabilities {
    pub dynamic_dotnet_plugins: bool,
    pub desktop_updater: bool,
    pub mp4_export: bool,
}

struct SessionCallback {
    storage: Arc<StorageEngine>,
    events: EventHub<SessionEvent>,
}
impl MakoSessionCallback for SessionCallback {
    fn on_auth_invalidated(&self, reason: String) {
        let _ = self.storage.set_mobile_refresh_token(None);
        let _ = self.events.send(SessionEvent::AuthInvalidated { reason });
    }
    fn on_rate_limit_encountered(&self, retry_after_seconds: u64) {
        let _ = self.events.send(SessionEvent::RateLimited {
            retry_after_seconds,
        });
    }
}

static ACTIVE: LazyLock<Mutex<Weak<MobileRuntime>>> = LazyLock::new(Default::default);

#[derive(uniffi::Object)]
pub struct MobileRuntime {
    directories: MobileDirectories,
    client: Arc<MakoClient>,
    storage: Arc<StorageEngine>,
    cache: Arc<ImageCache>,
    downloads: Arc<DownloadManager>,
    events: EventHub<SessionEvent>,
    preferences: Mutex<MobilePreferences>,
    subscriptions: Arc<SubscriptionSync>,
}

#[uniffi::export]
impl MobileRuntime {
    /// Call on a worker thread. An existing runtime may only be reopened for
    /// exactly the same profile; live native owners cannot be replaced.
    #[uniffi::constructor]
    pub fn open(directories: MobileDirectories) -> Result<Arc<Self>, String> {
        let directories = normalize_directories(directories)?;
        let mut active = ACTIVE.lock();
        if let Some(runtime) = active.upgrade() {
            return if runtime.directories == directories {
                Ok(runtime)
            } else {
                Err("A mobile runtime is already open for another profile".into())
            };
        }
        let storage = Arc::new(
            StorageEngine::new(
                PathBuf::from(&directories.data)
                    .join("pixeval.db")
                    .to_string_lossy()
                    .into_owned(),
            )
            .map_err(|e| e.to_string())?,
        );
        let preferences_path = PathBuf::from(&directories.data).join("mobile.json");
        let preferences: MobilePreferences = if preferences_path.exists() {
            serde_json::from_slice(&std::fs::read(&preferences_path).map_err(|e| e.to_string())?)
                .map_err(|e| format!("Invalid mobile preferences: {e}"))?
        } else {
            MobilePreferences::default()
        };
        preferences.validate()?;
        let config = preferences.configuration();
        let client = MakoClient::new(config.clone()).map_err(|e| e.to_string())?;
        let events = EventHub::new(256);
        client.set_session_callback(Some(Box::new(SessionCallback {
            storage: storage.clone(),
            events: events.clone(),
        })));
        let token_storage = storage.clone();
        client.set_token_persistence(Some(Arc::new(move |response| {
            token_storage
                .set_mobile_refresh_token(Some(response.refresh_token.clone()))
                .map_err(|e| e.to_string())
        })));
        let cache = ImageCache::new(directories.cache.clone(), Some(1024 * 1024 * 1024))
            .map_err(|e| e.to_string())?;
        cache.update_network_options(
            config.domain_fronting_enabled,
            config.split_delay_ms,
            config.host_ips.clone(),
            config.proxy_url.clone(),
        );
        let downloads = DownloadManager::new(
            preferences.download_concurrency,
            None,
            Some(DownloadNetworkOptions {
                proxy_url: config.proxy_url,
                static_domain_ips: config.host_ips,
            }),
        );
        downloads.bind_storage(storage.clone());
        downloads.bind_mako(client.clone());
        downloads.set_download_policy(pixeval_download::plan::DownloadPolicy {
            overwrite: preferences.overwrite,
            illustration_format: "Original".into(),
            ugoira_format: preferences.ugoira_format.clone(),
            novel_format: preferences.novel_format.clone(),
        });
        downloads.restore_histories();
        let subscriptions = Arc::new(SubscriptionSync::new_with_services(
            storage.clone(),
            client.clone(),
            downloads.clone(),
            SubscriptionSyncConfig {
                download_path_macro: preferences.download_path_macro.clone(),
                base_download_dir: directories.downloads.clone(),
                overwrite: preferences.overwrite,
                duplicate_stop_threshold: 5,
                my_user_id: None,
            },
            None,
        ));
        let runtime = Arc::new(Self {
            directories,
            client,
            storage,
            cache,
            downloads,
            events,
            preferences: Mutex::new(preferences),
            subscriptions,
        });
        *active = Arc::downgrade(&runtime);
        Ok(runtime)
    }

    pub fn directories(&self) -> MobileDirectories {
        self.directories.clone()
    }
    pub fn client(&self) -> Arc<MakoClient> {
        self.client.clone()
    }
    pub fn storage(&self) -> Arc<StorageEngine> {
        self.storage.clone()
    }
    pub fn cache(&self) -> Arc<ImageCache> {
        self.cache.clone()
    }
    pub fn downloads(&self) -> Arc<DownloadManager> {
        self.downloads.clone()
    }
    pub fn subscriptions(&self) -> Arc<SubscriptionSync> {
        self.subscriptions.clone()
    }
    pub fn preferences(&self) -> MobilePreferences {
        self.preferences.lock().clone()
    }
    pub fn update_preferences(&self, preferences: MobilePreferences) -> Result<(), String> {
        preferences.validate()?;
        let mut previous = self.preferences.lock();
        self.client
            .update_configuration(preferences.configuration())
            .map_err(|e| e.to_string())?;
        let target = PathBuf::from(&self.directories.data).join("mobile.json");
        let temp = target.with_extension("json.tmp");
        let result = serde_json::to_vec_pretty(&preferences)
            .map_err(|e| e.to_string())
            .and_then(|data| std::fs::write(&temp, data).map_err(|e| e.to_string()))
            .and_then(|_| std::fs::rename(&temp, &target).map_err(|e| e.to_string()));
        if let Err(error) = result {
            let _ = self.client.update_configuration(previous.configuration());
            return Err(error);
        }
        self.cache.update_network_options(
            preferences.domain_fronting_enabled,
            preferences.split_delay_ms,
            preferences.host_ips.clone(),
            preferences.proxy_url.clone(),
        );
        self.downloads
            .set_concurrency(preferences.download_concurrency);
        self.downloads
            .update_network_options(Some(DownloadNetworkOptions {
                proxy_url: preferences.proxy_url.clone(),
                static_domain_ips: preferences.host_ips.clone(),
            }));
        self.downloads
            .set_download_policy(pixeval_download::plan::DownloadPolicy {
                overwrite: preferences.overwrite,
                illustration_format: "Original".into(),
                ugoira_format: preferences.ugoira_format.clone(),
                novel_format: preferences.novel_format.clone(),
            });
        self.subscriptions.update_config(SubscriptionSyncConfig {
            download_path_macro: preferences.download_path_macro.clone(),
            base_download_dir: self.directories.downloads.clone(),
            overwrite: preferences.overwrite,
            duplicate_stop_threshold: 5,
            my_user_id: self.client.get_user().and_then(|u| u.id.parse().ok()),
        });
        self.subscriptions
            .set_daemon_interval(preferences.subscription_interval_seconds);
        *previous = preferences;
        Ok(())
    }
    pub fn record_browse(&self, work: WorkEntry) -> Result<(), String> {
        let payload = pixeval_storage::payload::ArtworkPayload::Work { entry: work };
        let (key, json) =
            pixeval_storage::payload::serialize(&payload).map_err(|e| e.to_string())?;
        self.storage
            .history()
            .add_or_replace_browse_history(
                pixeval_storage::payload::artwork_id(&payload),
                Some(key),
                pixeval_storage::payload::work_key(&payload),
                json,
            )
            .map_err(|e| e.to_string())?;
        Ok(())
    }
    pub fn help_document(&self, topic: Option<String>) -> String {
        let english = self.preferences.lock().language.starts_with("en");
        let document = match (english, topic.as_deref().unwrap_or("filters")) {
            (true, "download" | "macros") => {
                include_str!("../../../../src/Pixeval/Assets/Help/en-US/DownloadMacroHelp.md")
            }
            (true, "subscriptions") => {
                include_str!("../../../../src/Pixeval/Assets/Help/en-US/WorkSubscriptionsHelp.md")
            }
            (true, "mcp") => include_str!("../../../../src/Pixeval/Assets/Help/en-US/McpHelp.md"),
            (true, _) => {
                include_str!("../../../../src/Pixeval/Assets/Help/en-US/QueryFilterHelp.md")
            }
            (false, "download" | "macros") => {
                include_str!("../../../../src/Pixeval/Assets/Help/zh-Hans/DownloadMacroHelp.md")
            }
            (false, "subscriptions") => {
                include_str!("../../../../src/Pixeval/Assets/Help/zh-Hans/WorkSubscriptionsHelp.md")
            }
            (false, "mcp") => {
                include_str!("../../../../src/Pixeval/Assets/Help/zh-Hans/McpHelp.md")
            }
            (false, _) => {
                include_str!("../../../../src/Pixeval/Assets/Help/zh-Hans/QueryFilterHelp.md")
            }
        };
        document.into()
    }
    pub fn session_events(&self) -> SessionEventReceiver {
        SessionEventReceiver::from_hub(&self.events)
    }
    pub fn capabilities(&self) -> MobileCapabilities {
        MobileCapabilities {
            dynamic_dotnet_plugins: false,
            desktop_updater: false,
            mp4_export: pixeval_media::ugoira::is_mp4_supported(),
        }
    }

    /// Checkpoint work before suspension. Owners must still cancel their feeds
    /// and join callback collectors before releasing their generated handles.
    pub fn suspend(&self) {
        self.subscriptions.stop_daemon();
        self.subscriptions.cancel_all();
        for task in self.downloads.current_page_snapshot().ordinary_items {
            self.downloads.pause_work(task.key);
        }
        for folder in self.downloads.current_page_snapshot().folders {
            for task in folder.items {
                self.downloads.pause_work(task.key);
            }
        }
    }
}

impl Drop for MobileRuntime {
    fn drop(&mut self) {
        self.suspend();
        self.client.set_session_callback(None);
        self.client.set_token_persistence(None);
        self.downloads.set_page_callback(None);
        self.downloads.set_format_encoder(None);
        self.cache.close();
    }
}

fn normalize_directories(directories: MobileDirectories) -> Result<MobileDirectories, String> {
    fn normalize(value: String) -> Result<String, String> {
        let path = PathBuf::from(&value);
        if value.trim().is_empty() || !path.is_absolute() {
            return Err("Mobile directories must be nonempty absolute paths".into());
        }
        std::fs::create_dir_all(&path).map_err(|e| format!("Cannot create {value}: {e}"))?;
        Ok(path
            .canonicalize()
            .map_err(|e| e.to_string())?
            .to_string_lossy()
            .into_owned())
    }
    Ok(MobileDirectories {
        data: normalize(directories.data)?,
        cache: normalize(directories.cache)?,
        downloads: normalize(directories.downloads)?,
    })
}

#[uniffi::export]
pub fn mobile_oauth_code(url: String) -> Option<String> {
    let uri = url::Url::parse(&url).ok()?;
    let valid =
        (uri.scheme() == "pixiv" && uri.host_str() == Some("account") && uri.path() == "/login")
            || (uri.scheme() == "https"
                && uri.host_str() == Some("app-api.pixiv.net")
                && uri.path() == "/web/v1/users/auth/pixiv/callback");
    if !valid {
        return None;
    }
    uri.query_pairs()
        .find(|(key, value)| key == "code" && !value.is_empty())
        .map(|(_, value)| value.into_owned())
}

/// A deterministic FFI diagnostic using the actual application callback types.
/// Nothing is registered globally and every worker is joined before returning.
#[derive(Debug, uniffi::Record)]
pub struct MobileCallbackReport {
    pub tag_names: Vec<String>,
    pub author_names: Vec<String>,
    pub encoder_result: String,
    pub completed_workers: u32,
}

#[uniffi::export(async_runtime = "tokio")]
pub async fn verify_mobile_callbacks(
    storage: Box<dyn pixeval_filters::completions::FilterStorageProvider>,
    encoder: Box<dyn pixeval_download::plan::DownloadFormatEncoder>,
) -> Result<MobileCallbackReport, String> {
    tokio::task::spawn_blocking(move || {
        let storage: Arc<dyn pixeval_filters::completions::FilterStorageProvider> = storage.into();
        let workers: Vec<_> = (0..4)
            .map(|_| {
                let storage = storage.clone();
                std::thread::spawn(move || {
                    (
                        storage.query_search_history_tags("probe".into(), 8),
                        storage.query_subscription_authors("probe".into(), 8),
                    )
                })
            })
            .collect();
        let mut tags = Vec::new();
        let mut authors = Vec::new();
        let mut worker_failed = false;
        for worker in workers {
            match worker.join() {
                Ok((tag_results, author_results)) => {
                    tags.extend(tag_results.into_iter().map(|tag| tag.name));
                    authors.extend(author_results.into_iter().map(|author| author.name));
                }
                Err(_) => worker_failed = true,
            }
        }
        if worker_failed {
            return Err("Callback worker panicked".into());
        }
        let encoder_result =
            encoder.encode_static_image("source".into(), "destination".into(), "probe".into());
        Ok(MobileCallbackReport {
            tag_names: tags,
            author_names: authors,
            encoder_result,
            completed_workers: 4,
        })
    })
    .await
    .map_err(|e| e.to_string())?
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn rejects_relative_or_empty_directories() {
        for value in ["", "relative/profile"] {
            assert!(
                normalize_directories(MobileDirectories {
                    data: value.into(),
                    cache: value.into(),
                    downloads: value.into(),
                })
                .is_err()
            );
        }
    }
    #[test]
    fn oauth_codes_only_come_from_known_callback_routes() {
        assert_eq!(
            mobile_oauth_code("pixiv://account/login?code=abc%2Bdef".into()).as_deref(),
            Some("abc+def")
        );
        assert!(mobile_oauth_code("https://example.com/?code=secret".into()).is_none());
        assert!(mobile_oauth_code("pixiv://account/login?code=".into()).is_none());
    }
}
