use parking_lot::RwLock;
use pixeval_download::DownloadProgressCallback;
use pixeval_mako::{MakoClient, MakoConfigurationDto, MakoSessionCallback};
use std::sync::{Arc, LazyLock};
pub struct AppSessionCallback {
    event_sender: super::events::EventHub<crate::app::api::events::SessionEvent>,
}

impl MakoSessionCallback for AppSessionCallback {
    fn on_auth_invalidated(&self, reason: String) {
        if let Ok(storage) = APP_STATE.get_or_init_storage() {
            let _ = storage.set_mobile_refresh_token(None);
        }
        let _ = self
            .event_sender
            .send(crate::app::api::events::SessionEvent::AuthInvalidated { reason });
    }

    fn on_rate_limit_encountered(&self, retry_after_seconds: u64) {
        let _ = self
            .event_sender
            .send(crate::app::api::events::SessionEvent::RateLimited {
                retry_after_seconds,
            });
    }
}

pub struct AppDownloadCallback {
    pub sender: super::events::EventHub<crate::app::api::download::DownloadProgressEvent>,
}

impl DownloadProgressCallback for AppDownloadCallback {
    fn on_progress(
        &self,
        key: pixeval_download::DownloadTaskKey,
        progress_percentage: f64,
        downloaded_bytes: u64,
        total_bytes: u64,
    ) {
        let _ = self.sender.send(
            crate::app::api::download::DownloadProgressEvent::ProgressUpdated {
                destination: key.destination,
                progress_percentage,
                downloaded_bytes,
                total_bytes,
            },
        );
    }

    fn on_state_changed(
        &self,
        key: pixeval_download::DownloadTaskKey,
        state: pixeval_download::DownloadState,
        error_message: Option<String>,
    ) {
        let state_str = format!("{state:?}");
        let _ = self.sender.send(
            crate::app::api::download::DownloadProgressEvent::StateChanged {
                destination: key.destination,
                state: state_str,
                error_message,
            },
        );
    }

    fn on_completed(&self, _key: pixeval_download::DownloadTaskKey, destination: String) {
        let _ = self
            .sender
            .send(crate::app::api::download::DownloadProgressEvent::Completed { destination });
    }
}

pub struct AppState {
    pub client: Arc<MakoClient>,
    pub current_config: Arc<RwLock<MakoConfigurationDto>>,
    pub event_broadcaster: super::events::EventHub<crate::app::api::events::SessionEvent>,
    pub image_cache: Arc<RwLock<Option<Arc<pixeval_cache::ImageCache>>>>,
    pub download_manager: Arc<RwLock<Option<Arc<pixeval_download::DownloadManager>>>>,
    pub download_event_sender:
        super::events::EventHub<crate::app::api::download::DownloadProgressEvent>,
    pub storage_engine: Arc<RwLock<Option<Arc<pixeval_storage::StorageEngine>>>>,
}

impl AppState {
    fn new() -> Self {
        let config = MakoConfigurationDto::default();
        let client = MakoClient::new(config.clone()).expect("Failed to create default MakoClient");
        let tx = super::events::EventHub::new(256);
        let dtx = super::events::EventHub::new(1024);

        let cb = Box::new(AppSessionCallback {
            event_sender: tx.clone(),
        });
        client.set_session_callback(Some(cb));
        client.set_token_persistence(Some(Arc::new(|response| {
            let storage = APP_STATE.get_or_init_storage()?;
            if storage
                .get_mobile_refresh_token()
                .map_err(|e| e.to_string())?
                .as_deref()
                != Some(response.refresh_token.as_str())
            {
                storage
                    .set_mobile_refresh_token(Some(response.refresh_token.clone()))
                    .map_err(|e| format!("Failed to persist refreshed session: {e}"))?;
            }
            Ok(())
        })));

        Self {
            client,
            current_config: Arc::new(RwLock::new(config)),
            event_broadcaster: tx,
            image_cache: Arc::new(RwLock::new(None)),
            download_manager: Arc::new(RwLock::new(None)),
            download_event_sender: dtx,
            storage_engine: Arc::new(RwLock::new(None)),
        }
    }

    pub fn get_or_init_cache(&self) -> Result<Arc<pixeval_cache::ImageCache>, String> {
        let read = self.image_cache.read();
        if let Some(cache) = read.as_ref() {
            return Ok(cache.clone());
        }
        drop(read);

        let mut write = self.image_cache.write();
        if let Some(cache) = write.as_ref() {
            return Ok(cache.clone());
        }

        let default_dir = std::env::temp_dir()
            .join("pixeval_flutter_cache")
            .to_string_lossy()
            .to_string();

        let cache = pixeval_cache::ImageCache::new(default_dir, Some(1024 * 1024 * 1024))
            .map_err(|e| format!("Failed to create default ImageCache: {e}"))?;

        let config = self.current_config.read().clone();
        cache.update_network_options(
            config.domain_fronting_enabled,
            config.split_delay_ms,
            config.host_ips.clone(),
            config.proxy_url,
        );

        *write = Some(cache.clone());
        Ok(cache)
    }

    pub fn init_download_manager_with_concurrency(
        &self,
        concurrency: u32,
    ) -> Result<Arc<pixeval_download::DownloadManager>, String> {
        let cb = Box::new(AppDownloadCallback {
            sender: self.download_event_sender.clone(),
        });

        let config = self.current_config.read().clone();
        let net_opts = Some(pixeval_download::DownloadNetworkOptions {
            proxy_url: config.proxy_url,
            static_domain_ips: config.host_ips.clone(),
        });

        let dm = pixeval_download::DownloadManager::new(concurrency, Some(cb), net_opts);
        *self.download_manager.write() = Some(dm.clone());
        Ok(dm)
    }

    pub fn get_or_init_download_manager(
        &self,
    ) -> Result<Arc<pixeval_download::DownloadManager>, String> {
        let read = self.download_manager.read();
        if let Some(dm) = read.as_ref() {
            return Ok(dm.clone());
        }
        drop(read);

        self.init_download_manager_with_concurrency(3)
    }

    pub fn init_storage_with_path(&self, db_path: String) -> Result<(), String> {
        let engine = pixeval_storage::StorageEngine::new(db_path)
            .map_err(|e| format!("Failed to open SQLite database: {e}"))?;
        *self.storage_engine.write() = Some(Arc::new(engine));
        Ok(())
    }

    pub fn get_or_init_storage(&self) -> Result<Arc<pixeval_storage::StorageEngine>, String> {
        let read = self.storage_engine.read();
        if let Some(s) = read.as_ref() {
            return Ok(s.clone());
        }
        drop(read);

        let mut write = self.storage_engine.write();
        if let Some(s) = write.as_ref() {
            return Ok(s.clone());
        }

        let default_db = std::env::temp_dir()
            .join("pixeval_flutter_data.db")
            .to_string_lossy()
            .to_string();

        let engine = pixeval_storage::StorageEngine::new(default_db)
            .map_err(|e| format!("Failed to open default SQLite storage: {e}"))?;
        let arc = Arc::new(engine);
        *write = Some(arc.clone());
        Ok(arc)
    }
}

pub static APP_STATE: LazyLock<AppState> = LazyLock::new(AppState::new);
