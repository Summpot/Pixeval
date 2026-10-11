// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::sync::{Arc, LazyLock};
use parking_lot::Mutex;
use tokio_util::sync::CancellationToken;

use pixeval_download::DownloadManager;
use pixeval_mako::MakoClient;
use pixeval_storage::{StorageEngine, WorkSubscriptionRecord};

use crate::models::*;
use crate::queue::SubscriptionSyncQueue;

static SUBSCRIPTION_RUNTIME: LazyLock<tokio::runtime::Runtime> = LazyLock::new(|| {
    tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .thread_name("pixeval-subscription-worker")
        .build()
        .expect("Failed to build Tokio runtime for subscription sync engine")
});

fn subscription_type_name(subscription_type: u32) -> String {
    match subscription_type {
        0 => "Bookmarks".to_string(),
        1 => "Posts".to_string(),
        _ => "Series".to_string(),
    }
}

struct EngineInner {
    queue: SubscriptionSyncQueue,
    active_request: Option<SyncRequestKind>,
    duplicate_counts: HashMap<i64, u32>,
    fetch_states: HashMap<i64, SubscriptionFetchState>,
    callback: Option<Arc<dyn SubscriptionProgressCallback>>,
    duplicate_stop_threshold: u32,
    storage: Option<Arc<StorageEngine>>,
    mako: Option<Arc<MakoClient>>,
    download: Option<Arc<DownloadManager>>,
    config: Option<SubscriptionSyncConfig>,
    is_running: bool,
    global_cancel: CancellationToken,
    active_sub_cancel: Option<CancellationToken>,
    daemon_cancel: Option<CancellationToken>,
    daemon_interval_secs: u64,
    is_daemon_running: bool,
}

#[derive(Clone, uniffi::Object)]
pub struct SubscriptionSyncEngine {
    inner: Arc<Mutex<EngineInner>>,
}

#[uniffi::export]
impl SubscriptionSyncEngine {
    #[uniffi::constructor]
    pub fn new(
        duplicate_stop_threshold: Option<u32>,
        callback: Option<Box<dyn SubscriptionProgressCallback>>,
    ) -> Self {
        Self {
            inner: Arc::new(Mutex::new(EngineInner {
                queue: SubscriptionSyncQueue::new(),
                active_request: None,
                duplicate_counts: HashMap::new(),
                fetch_states: HashMap::new(),
                callback: callback.map(Arc::from),
                duplicate_stop_threshold: duplicate_stop_threshold.unwrap_or(5),
                storage: None,
                mako: None,
                download: None,
                config: None,
                is_running: false,
                global_cancel: CancellationToken::new(),
                active_sub_cancel: None,
                daemon_cancel: None,
                daemon_interval_secs: 1800,
                is_daemon_running: false,
            })),
        }
    }

    #[uniffi::constructor]
    pub fn new_with_services(
        storage: Arc<StorageEngine>,
        mako: Arc<MakoClient>,
        download: Arc<DownloadManager>,
        config: SubscriptionSyncConfig,
        callback: Option<Box<dyn SubscriptionProgressCallback>>,
    ) -> Self {
        let threshold = config.duplicate_stop_threshold;
        Self {
            inner: Arc::new(Mutex::new(EngineInner {
                queue: SubscriptionSyncQueue::new(),
                active_request: None,
                duplicate_counts: HashMap::new(),
                fetch_states: HashMap::new(),
                callback: callback.map(Arc::from),
                duplicate_stop_threshold: threshold,
                storage: Some(storage),
                mako: Some(mako),
                download: Some(download),
                config: Some(config),
                is_running: false,
                global_cancel: CancellationToken::new(),
                active_sub_cancel: None,
                daemon_cancel: None,
                daemon_interval_secs: 1800,
                is_daemon_running: false,
            })),
        }
    }

    pub fn update_config(&self, config: SubscriptionSyncConfig) {
        let mut inner = self.inner.lock();
        inner.duplicate_stop_threshold = config.duplicate_stop_threshold;
        inner.config = Some(config);
    }

    pub fn queue_sync_all(&self) -> bool {
        let mut inner = self.inner.lock();
        let active = inner.active_request.clone();
        let enqueued = inner.queue.try_enqueue(SyncRequestKind::All, active.as_ref());
        if enqueued {
            drop(inner);
            Self::maybe_start_worker(&self.inner);
            true
        } else {
            false
        }
    }

    pub fn queue_sync_subscription(&self, subscription_id: i64) -> bool {
        let mut inner = self.inner.lock();
        let active = inner.active_request.clone();
        let enqueued = inner.queue.try_enqueue(
            SyncRequestKind::Single { subscription_id },
            active.as_ref(),
        );
        if enqueued {
            drop(inner);
            Self::maybe_start_worker(&self.inner);
            true
        } else {
            false
        }
    }

    pub fn remove_pending_subscription(&self, subscription_id: i64) {
        let mut inner = self.inner.lock();
        inner.queue.remove_subscription(subscription_id);
        if let Some(SyncRequestKind::Single { subscription_id: active_id }) = inner.active_request {
            if active_id == subscription_id {
                if let Some(ref token) = inner.active_sub_cancel {
                    token.cancel();
                }
            }
        }
    }

    pub fn cancel_active(&self) {
        let inner = self.inner.lock();
        if let Some(ref token) = inner.active_sub_cancel {
            token.cancel();
        }
    }

    pub fn cancel_all(&self) {
        let mut inner = self.inner.lock();
        inner.global_cancel.cancel();
        if let Some(ref token) = inner.active_sub_cancel {
            token.cancel();
        }
        inner.queue.clear();
        inner.active_request = None;
    }

    pub fn is_sync_in_progress(&self) -> bool {
        let inner = self.inner.lock();
        inner.is_running
    }

    pub fn try_dequeue_sync_request(&self) -> Option<SyncRequestKind> {
        let mut inner = self.inner.lock();
        if let Some(req) = inner.queue.try_dequeue() {
            inner.active_request = Some(req.clone());
            Some(req)
        } else {
            None
        }
    }

    pub fn complete_active_sync_request(&self, _request: SyncRequestKind) {
        let mut inner = self.inner.lock();
        inner.active_request = None;
    }

    pub fn record_work_processed(&self, subscription_id: i64, is_duplicate: bool) -> bool {
        let mut inner = self.inner.lock();
        Self::record_work_processed_internal(&mut inner, subscription_id, is_duplicate)
    }

    pub fn reset_duplicate_counter(&self, subscription_id: i64) {
        let mut inner = self.inner.lock();
        inner.duplicate_counts.remove(&subscription_id);
    }

    pub fn set_fetch_state(
        &self,
        subscription_id: i64,
        page: u32,
        total_fetched: u32,
        fetch_status: SubscriptionStatus,
    ) {
        let mut inner = self.inner.lock();
        let state = SubscriptionFetchState {
            subscription_id,
            page,
            total_fetched,
            status: fetch_status,
        };
        inner.fetch_states.insert(subscription_id, state.clone());
        if let Some(ref cb) = inner.callback {
            cb.on_fetch_state_changed(state);
        }
    }

    pub fn get_fetch_state(&self, subscription_id: i64) -> Option<SubscriptionFetchState> {
        let inner = self.inner.lock();
        inner.fetch_states.get(&subscription_id).cloned()
    }

    pub fn notify_item_fetched(&self, item: SubscriptionDownloadItem) {
        let inner = self.inner.lock();
        if let Some(ref cb) = inner.callback {
            cb.on_item_fetched(item);
        }
    }

    pub fn get_pending_count(&self) -> u32 {
        let inner = self.inner.lock();
        inner.queue.count() as u32
    }

    pub fn clear_queue(&self) {
        let mut inner = self.inner.lock();
        inner.queue.clear();
    }

    pub fn start_daemon(&self, interval_secs: u64) {
        let mut inner = self.inner.lock();
        if let Some(ref cancel) = inner.daemon_cancel {
            cancel.cancel();
        }
        let cancel = CancellationToken::new();
        inner.daemon_cancel = Some(cancel.clone());
        inner.daemon_interval_secs = interval_secs.max(1);
        inner.is_daemon_running = true;
        if let Some(ref cb) = inner.callback {
            cb.on_daemon_state_changed(true);
        }
        drop(inner);

        let clone = self.clone();
        SUBSCRIPTION_RUNTIME.spawn(async move {
            clone.queue_sync_all();

            loop {
                let interval = {
                    let inner = clone.inner.lock();
                    if !inner.is_daemon_running {
                        break;
                    }
                    inner.daemon_interval_secs
                };

                tokio::select! {
                    _ = cancel.cancelled() => {
                        break;
                    }
                    _ = tokio::time::sleep(tokio::time::Duration::from_secs(interval)) => {
                        let should_run = {
                            let inner = clone.inner.lock();
                            inner.is_daemon_running && !cancel.is_cancelled()
                        };
                        if should_run {
                            clone.queue_sync_all();
                        }
                    }
                }
            }

            let mut inner = clone.inner.lock();
            if inner.is_daemon_running {
                inner.is_daemon_running = false;
                if let Some(ref cb) = inner.callback {
                    cb.on_daemon_state_changed(false);
                }
            }
        });
    }

    pub fn stop_daemon(&self) {
        let mut inner = self.inner.lock();
        if let Some(ref cancel) = inner.daemon_cancel {
            cancel.cancel();
        }
        inner.daemon_cancel = None;
        if inner.is_daemon_running {
            inner.is_daemon_running = false;
            if let Some(ref cb) = inner.callback {
                cb.on_daemon_state_changed(false);
            }
        }
    }

    pub fn is_daemon_running(&self) -> bool {
        let inner = self.inner.lock();
        inner.is_daemon_running
    }

    pub fn set_daemon_interval(&self, interval_secs: u64) {
        let mut inner = self.inner.lock();
        inner.daemon_interval_secs = interval_secs.max(1);
    }

    pub fn get_daemon_interval(&self) -> u64 {
        let inner = self.inner.lock();
        inner.daemon_interval_secs
    }
}

impl SubscriptionSyncEngine {
    fn record_work_processed_internal(
        inner: &mut EngineInner,
        subscription_id: i64,
        is_duplicate: bool,
    ) -> bool {
        let threshold = inner.duplicate_stop_threshold;
        let count = inner.duplicate_counts.entry(subscription_id).or_insert(0);

        if is_duplicate {
            *count += 1;
            if *count >= threshold {
                let duplicate_count = *count;
                if let Some(ref cb) = inner.callback {
                    cb.on_duplicate_stopped(subscription_id, duplicate_count);
                }
                return true;
            }
            false
        } else {
            *count = 0;
            false
        }
    }

    fn maybe_start_worker(inner_arc: &Arc<Mutex<EngineInner>>) {
        let mut inner = inner_arc.lock();
        if inner.storage.is_none() || inner.mako.is_none() || inner.download.is_none() {
            return;
        }

        if !inner.is_running {
            inner.is_running = true;
            inner.global_cancel = CancellationToken::new();
            let clone = inner_arc.clone();
            SUBSCRIPTION_RUNTIME.spawn(async move {
                Self::run_worker(clone).await;
            });
        }
    }

    async fn run_worker(inner_arc: Arc<Mutex<EngineInner>>) {
        loop {
            let (req, sub_cancel, storage, mako, download, config, callback) = {
                let mut inner = inner_arc.lock();
                if inner.global_cancel.is_cancelled() {
                    inner.is_running = false;
                    inner.active_request = None;
                    inner.active_sub_cancel = None;
                    if let Some(ref cb) = inner.callback {
                        cb.on_sync_finished();
                    }
                    break;
                }
                match inner.queue.try_dequeue() {
                    Some(req) => {
                        inner.active_request = Some(req.clone());
                        let token = CancellationToken::new();
                        inner.active_sub_cancel = Some(token.clone());
                        let storage = inner.storage.clone();
                        let mako = inner.mako.clone();
                        let download = inner.download.clone();
                        let config = inner.config.clone();
                        let callback = inner.callback.clone();
                        (req, token, storage, mako, download, config, callback)
                    }
                    None => {
                        inner.is_running = false;
                        inner.active_request = None;
                        inner.active_sub_cancel = None;
                        if let Some(ref cb) = inner.callback {
                            cb.on_sync_finished();
                        }
                        break;
                    }
                }
            };

            if let (Some(storage), Some(mako), Some(download), Some(config)) =
                (storage, mako, download, config)
            {
                match req {
                    SyncRequestKind::All => {
                        Self::process_sync_all(
                            &inner_arc,
                            storage,
                            mako,
                            download,
                            config,
                            callback,
                            sub_cancel,
                        )
                        .await;
                    }
                    SyncRequestKind::Single { subscription_id } => {
                        Self::process_sync_single(
                            &inner_arc,
                            subscription_id,
                            storage,
                            mako,
                            download,
                            config,
                            callback,
                            sub_cancel,
                        )
                        .await;
                    }
                }
            }

            {
                let mut inner = inner_arc.lock();
                inner.active_request = None;
                inner.active_sub_cancel = None;
            }
        }
    }

    async fn process_sync_all(
        inner: &Arc<Mutex<EngineInner>>,
        storage: Arc<StorageEngine>,
        mako: Arc<MakoClient>,
        download: Arc<DownloadManager>,
        config: SubscriptionSyncConfig,
        callback: Option<Arc<dyn SubscriptionProgressCallback>>,
        cancel_token: CancellationToken,
    ) {
        let subscriptions = match storage.get_all_subscriptions() {
            Ok(subs) => subs,
            Err(_) => return,
        };

        let mut total_new_works = 0u32;
        for sub in subscriptions {
            if cancel_token.is_cancelled() {
                break;
            }
            let sub_cancel = {
                let mut g = inner.lock();
                if g.global_cancel.is_cancelled() {
                    break;
                }
                let token = CancellationToken::new();
                g.active_sub_cancel = Some(token.clone());
                token
            };
            let count = Self::sync_subscription_internal(
                inner,
                sub,
                storage.clone(),
                mako.clone(),
                download.clone(),
                &config,
                callback.as_deref(),
                sub_cancel,
            )
            .await;
            total_new_works += count;
        }

        if total_new_works > 0 {
            if let Some(ref cb) = callback {
                cb.on_new_works_ingested(total_new_works);
            }
        }
    }

    async fn process_sync_single(
        inner: &Arc<Mutex<EngineInner>>,
        subscription_id: i64,
        storage: Arc<StorageEngine>,
        mako: Arc<MakoClient>,
        download: Arc<DownloadManager>,
        config: SubscriptionSyncConfig,
        callback: Option<Arc<dyn SubscriptionProgressCallback>>,
        cancel_token: CancellationToken,
    ) {
        let sub = match storage.get_subscription_by_history_id(subscription_id) {
            Ok(Some(s)) => s,
            _ => return,
        };

        let count = Self::sync_subscription_internal(
            inner,
            sub,
            storage,
            mako,
            download,
            &config,
            callback.as_deref(),
            cancel_token,
        )
        .await;

        if count > 0 {
            if let Some(ref cb) = callback {
                cb.on_new_works_ingested(count);
            }
        }
    }

    async fn sync_subscription_internal(
        inner: &Arc<Mutex<EngineInner>>,
        sub: WorkSubscriptionRecord,
        storage: Arc<StorageEngine>,
        mako: Arc<MakoClient>,
        download: Arc<DownloadManager>,
        config: &SubscriptionSyncConfig,
        callback: Option<&dyn SubscriptionProgressCallback>,
        cancel_token: CancellationToken,
    ) -> u32 {
        let history_id = sub.history_entry_id;
        download.set_overwrite(config.overwrite);
        let subscription_type = subscription_type_name(sub.subscription_type);

        // 1. Initial Fetch State
        {
            let mut g = inner.lock();
            g.duplicate_counts.insert(history_id, 0);
            let state = SubscriptionFetchState {
                subscription_id: history_id,
                page: 1,
                total_fetched: 0,
                status: SubscriptionStatus::Fetching,
            };
            g.fetch_states.insert(history_id, state.clone());
            if let Some(cb) = callback {
                cb.on_fetch_state_changed(state);
            }
        }

        // 2. Metadata refresh if empty
        if sub.title.is_empty() || sub.avatar.is_empty() {
            if sub.subscription_type == 0 || sub.subscription_type == 1 {
                if let Ok(user_resp) = mako.get_user_detail(sub.id).await {
                    let name = user_resp.user.name;
                    let account = user_resp.user.account;
                    let avatar = user_resp
                        .user
                        .profile_image_urls
                        .medium
                        .or(user_resp.user.profile_image_urls.px_170x170)
                        .unwrap_or_default();
                    if storage
                        .upsert_subscription(
                            sub.id,
                            sub.subscription_type,
                            sub.work_kind,
                            name.clone(),
                            account.clone(),
                            avatar.clone(),
                            String::new(),
                            None,
                        )
                        .is_ok()
                    {
                        if let Some(cb) = callback {
                            cb.on_subscription_updated(history_id, name, account, avatar);
                        }
                    }
                }
            }
        }

        let mut total_fetched = 0u32;

        // 3. Fetch stream execution
        if sub.work_kind == 2 {
            // Novel
            let mut engines = Vec::new();
            match sub.subscription_type {
                0 => {
                    engines.push(mako.novel_bookmarks(sub.id, "public".into(), None));
                    if config.my_user_id == Some(sub.id) {
                        engines.push(mako.novel_bookmarks(sub.id, "private".into(), None));
                    }
                }
                1 => engines.push(mako.novel_posted(sub.id)),
                2 => engines.push(mako.novel_series(sub.id)),
                _ => {}
            }

            'outer_novel: for engine in engines {
                while let Some(novel) = engine.next().await {
                    if cancel_token.is_cancelled() {
                        break 'outer_novel;
                    }

                    let payload_json = serde_json::to_string(&novel).unwrap_or_default();
                    let probe = download.plan_pixiv_probe(
                        payload_json.clone(),
                        true,
                        -1,
                        config.download_path_macro.clone(),
                        config.base_download_dir.clone(),
                        history_id,
                        subscription_type.clone(),
                    );
                    let is_real_file_present = probe
                        .probe_paths
                        .iter()
                        .any(|path| std::path::Path::new(path).is_file());

                    let is_dup = storage
                        .contains_subscription_download_identity(
                            history_id,
                            novel.id.to_string(),
                            probe.destination.clone(),
                        )
                        .unwrap_or(false)
                        || is_real_file_present;

                    let is_fused = {
                        let mut g = inner.lock();
                        Self::record_work_processed_internal(&mut g, history_id, is_dup)
                    };

                    if is_dup {
                        if is_fused {
                            break 'outer_novel;
                        }
                        continue;
                    }

                    if storage.get_subscription_by_history_id(history_id).unwrap_or(None).is_none() {
                        break 'outer_novel;
                    }

                    let destination = download.enqueue_pixiv(
                        payload_json.clone(),
                        true,
                        -1,
                        config.download_path_macro.clone(),
                        config.base_download_dir.clone(),
                        history_id,
                        subscription_type.clone(),
                    );
                    if destination.is_empty() {
                        continue;
                    }

                    total_fetched += 1;
                    let item = SubscriptionDownloadItem {
                        artwork_id: novel.id.to_string(),
                        destination,
                        work_subscription_id: history_id,
                        title: novel.title.clone(),
                        payload_json,
                        is_novel: true,
                    };

                    {
                        let mut g = inner.lock();
                        let page = (total_fetched / 30) + 1;
                        let state = SubscriptionFetchState {
                            subscription_id: history_id,
                            page,
                            total_fetched,
                            status: SubscriptionStatus::Fetching,
                        };
                        g.fetch_states.insert(history_id, state.clone());
                        if let Some(cb) = callback {
                            cb.on_item_fetched(item);
                            cb.on_fetch_state_changed(state);
                        }
                    }
                }
            }
        } else {
            // Illustration / Manga
            let mut engines = Vec::new();
            match sub.subscription_type {
                0 => {
                    engines.push(mako.work_bookmarks(sub.id, "public".into(), None));
                    if config.my_user_id == Some(sub.id) {
                        engines.push(mako.work_bookmarks(sub.id, "private".into(), None));
                    }
                }
                1 => engines.push(mako.work_posted(
                    sub.id,
                    if sub.work_kind == 1 {
                        "manga".into()
                    } else {
                        "illust".into()
                    },
                )),
                2 => engines.push(mako.work_series(sub.id)),
                _ => {}
            }

            'outer_illust: for engine in engines {
                while let Some(illust) = engine.next().await {
                    if cancel_token.is_cancelled() {
                        break 'outer_illust;
                    }

                    let payload_json = serde_json::to_string(&illust).unwrap_or_default();
                    let probe = download.plan_pixiv_probe(
                        payload_json.clone(),
                        false,
                        -1,
                        config.download_path_macro.clone(),
                        config.base_download_dir.clone(),
                        history_id,
                        subscription_type.clone(),
                    );
                    let is_real_file_present = probe
                        .probe_paths
                        .iter()
                        .any(|path| std::path::Path::new(path).is_file());

                    let is_dup = storage
                        .contains_subscription_download_identity(
                            history_id,
                            illust.id.to_string(),
                            probe.destination.clone(),
                        )
                        .unwrap_or(false)
                        || is_real_file_present;

                    let is_fused = {
                        let mut g = inner.lock();
                        Self::record_work_processed_internal(&mut g, history_id, is_dup)
                    };

                    if is_dup {
                        if is_fused {
                            break 'outer_illust;
                        }
                        continue;
                    }

                    if storage.get_subscription_by_history_id(history_id).unwrap_or(None).is_none() {
                        break 'outer_illust;
                    }

                    let destination = download.enqueue_pixiv(
                        payload_json.clone(),
                        false,
                        -1,
                        config.download_path_macro.clone(),
                        config.base_download_dir.clone(),
                        history_id,
                        subscription_type.clone(),
                    );
                    if destination.is_empty() {
                        continue;
                    }

                    total_fetched += 1;
                    let item = SubscriptionDownloadItem {
                        artwork_id: illust.id.to_string(),
                        destination,
                        work_subscription_id: history_id,
                        title: illust.title.clone(),
                        payload_json,
                        is_novel: false,
                    };

                    {
                        let mut g = inner.lock();
                        let page = (total_fetched / 30) + 1;
                        let state = SubscriptionFetchState {
                            subscription_id: history_id,
                            page,
                            total_fetched,
                            status: SubscriptionStatus::Fetching,
                        };
                        g.fetch_states.insert(history_id, state.clone());
                        if let Some(cb) = callback {
                            cb.on_item_fetched(item);
                            cb.on_fetch_state_changed(state);
                        }
                    }
                }
            }
        }

        // 4. Final state update
        {
            let mut g = inner.lock();
            let page = (total_fetched / 30) + 1;
            let final_status = if cancel_token.is_cancelled() {
                SubscriptionStatus::Cancelled
            } else {
                SubscriptionStatus::Completed
            };
            let state = SubscriptionFetchState {
                subscription_id: history_id,
                page,
                total_fetched,
                status: final_status,
            };
            g.fetch_states.insert(history_id, state.clone());
            if let Some(cb) = callback {
                cb.on_fetch_state_changed(state);
            }
        }

        total_fetched
    }
}
