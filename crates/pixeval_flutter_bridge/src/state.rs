use parking_lot::RwLock;
use pixeval_download::DownloadProgressCallback;
use pixeval_mako::stream::IllustrationFeed;
use pixeval_mako::{MakoClient, MakoConfigurationDto, MakoSessionCallback};
use std::sync::{Arc, LazyLock};

type EventTaskRegistry =
    parking_lot::Mutex<std::collections::HashMap<String, Option<(u64, tokio::task::AbortHandle)>>>;
static NEXT_EVENT_TASK: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(1);
pub(crate) static SESSION_EVENT_TASKS: LazyLock<EventTaskRegistry> =
    LazyLock::new(Default::default);
pub(crate) static DOWNLOAD_EVENT_TASKS: LazyLock<EventTaskRegistry> =
    LazyLock::new(Default::default);

struct EventTaskCleanup {
    registry: &'static EventTaskRegistry,
    id: String,
    generation: u64,
}
impl Drop for EventTaskCleanup {
    fn drop(&mut self) {
        let mut tasks = self.registry.lock();
        if tasks
            .get(&self.id)
            .and_then(|task| task.as_ref())
            .is_some_and(|(generation, _)| *generation == self.generation)
        {
            tasks.remove(&self.id);
        }
    }
}

pub(crate) fn spawn_event_task(
    registry: &'static EventTaskRegistry,
    id: String,
    future: impl std::future::Future<Output = ()> + Send + 'static,
) {
    let mut tasks = registry.lock();
    if let Some(previous) = tasks.remove(&id) {
        match previous {
            Some((_, task)) => task.abort(),
            // Cancellation can arrive before the normal FRB worker starts.
            None => return,
        }
    }
    let generation = NEXT_EVENT_TASK.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
    let cleanup = EventTaskCleanup {
        registry,
        id: id.clone(),
        generation,
    };
    let task = BRIDGE_TOKIO_RT.spawn(async move {
        let _cleanup = cleanup;
        future.await;
    });
    tasks.insert(id, Some((generation, task.abort_handle())));
}

pub(crate) fn cancel_event_task(registry: &'static EventTaskRegistry, id: String) {
    let mut tasks = registry.lock();
    match tasks.remove(&id) {
        Some(Some((_, task))) => task.abort(),
        _ => {
            tasks.insert(id, None);
        }
    }
}

pub struct FlutterSessionCallback {
    event_sender: tokio::sync::broadcast::Sender<crate::api::events::SessionEvent>,
}

impl MakoSessionCallback for FlutterSessionCallback {
    fn on_auth_invalidated(&self, reason: String) {
        if let Ok(storage) = APP_STATE.get_or_init_storage() {
            let _ = storage.set_mobile_refresh_token(None);
        }
        let _ = self
            .event_sender
            .send(crate::api::events::SessionEvent::AuthInvalidated { reason });
    }

    fn on_rate_limit_encountered(&self, retry_after_seconds: u64) {
        let _ = self
            .event_sender
            .send(crate::api::events::SessionEvent::RateLimited {
                retry_after_seconds,
            });
    }
}

pub struct FlutterDownloadCallback {
    pub sender: tokio::sync::broadcast::Sender<crate::api::download::DownloadProgressEvent>,
}

impl DownloadProgressCallback for FlutterDownloadCallback {
    fn on_progress(
        &self,
        key: pixeval_download::DownloadTaskKey,
        progress_percentage: f64,
        downloaded_bytes: u64,
        total_bytes: u64,
    ) {
        let _ = self.sender.send(
            crate::api::download::DownloadProgressEvent::ProgressUpdated {
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
        let _ = self
            .sender
            .send(crate::api::download::DownloadProgressEvent::StateChanged {
                destination: key.destination,
                state: state_str,
                error_message,
            });
    }

    fn on_completed(&self, _key: pixeval_download::DownloadTaskKey, destination: String) {
        let _ = self
            .sender
            .send(crate::api::download::DownloadProgressEvent::Completed { destination });
    }
}

pub struct AppState {
    pub client: Arc<MakoClient>,
    pub current_config: Arc<RwLock<MakoConfigurationDto>>,
    pub event_broadcaster: tokio::sync::broadcast::Sender<crate::api::events::SessionEvent>,
    pub active_recommended_feed: Arc<RwLock<Option<Arc<IllustrationFeed>>>>,
    pub active_ranking_feed: Arc<RwLock<Option<Arc<IllustrationFeed>>>>,
    pub active_follow_feed: Arc<RwLock<Option<Arc<IllustrationFeed>>>>,
    pub(crate) search_feeds:
        RwLock<std::collections::HashMap<String, crate::api::search::SearchFeedState>>,
    pub image_cache: Arc<RwLock<Option<Arc<pixeval_cache::ImageCache>>>>,
    pub download_manager: Arc<RwLock<Option<Arc<pixeval_download::DownloadManager>>>>,
    pub download_event_sender:
        tokio::sync::broadcast::Sender<crate::api::download::DownloadProgressEvent>,
    pub storage_engine: Arc<RwLock<Option<Arc<pixeval_storage::StorageEngine>>>>,
}

impl AppState {
    fn new() -> Self {
        let config = MakoConfigurationDto::default();
        let client = MakoClient::new(config.clone()).expect("Failed to create default MakoClient");
        let (tx, _rx) = tokio::sync::broadcast::channel(64);
        let (dtx, _drx) = tokio::sync::broadcast::channel(128);

        let cb = Box::new(FlutterSessionCallback {
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
            active_recommended_feed: Arc::new(RwLock::new(None)),
            active_ranking_feed: Arc::new(RwLock::new(None)),
            active_follow_feed: Arc::new(RwLock::new(None)),
            search_feeds: RwLock::new(std::collections::HashMap::new()),
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
        let cb = Box::new(FlutterDownloadCallback {
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

pub static BRIDGE_TOKIO_RT: LazyLock<tokio::runtime::Runtime> = LazyLock::new(|| {
    tokio::runtime::Builder::new_multi_thread()
        .worker_threads(2)
        .enable_all()
        .thread_name("pixeval-bridge-worker")
        .build()
        .expect("Failed to initialize bridge tokio runtime")
});

pub static APP_STATE: LazyLock<AppState> = LazyLock::new(AppState::new);

#[cfg(test)]
mod event_task_tests {
    use super::*;
    static TASKS: LazyLock<EventTaskRegistry> = LazyLock::new(Default::default);

    #[tokio::test]
    async fn cancellation_releases_an_idle_receiver_without_another_event() {
        let (sender, mut receiver) = tokio::sync::broadcast::channel::<u8>(1);
        spawn_event_task(&TASKS, "idle".into(), async move {
            let _ = receiver.recv().await;
        });
        assert_eq!(sender.receiver_count(), 1);
        cancel_event_task(&TASKS, "idle".into());
        tokio::time::timeout(std::time::Duration::from_secs(2), async {
            while sender.receiver_count() != 0 {
                tokio::task::yield_now().await;
            }
        })
        .await
        .unwrap();
        assert!(!TASKS.lock().contains_key("idle"));
    }

    #[test]
    fn cancellation_can_arrive_before_the_frb_worker_creates_a_stream() {
        let (sender, mut receiver) = tokio::sync::broadcast::channel::<u8>(1);
        cancel_event_task(&TASKS, "early".into());
        spawn_event_task(&TASKS, "early".into(), async move {
            let _ = receiver.recv().await;
        });
        assert_eq!(sender.receiver_count(), 0);
        assert!(!TASKS.lock().contains_key("early"));
    }
}
