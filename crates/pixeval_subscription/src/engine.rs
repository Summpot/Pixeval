// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::sync::{Arc, LazyLock};
use parking_lot::Mutex;
use tokio_util::sync::CancellationToken;

use pixeval_download::metapath::MacroContext;
use pixeval_download::{DownloadManager, DownloadTaskKey};
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

fn resolve_tokens(path: &str, ext: &str, set_index: i32) -> String {
    let mut resolved = path
        .replace("<ext>", ext)
        .replace("<ext:l>", &ext.to_lowercase())
        .replace("<ext:u>", &ext.to_uppercase());
    if set_index >= 0 {
        resolved = resolved
            .replace("<pic_set_index>", &set_index.to_string())
            .replace("<pic_set_index:00>", &format!("{:02}", set_index))
            .replace("<pic_set_index:000>", &format!("{:03}", set_index))
            .replace("<pic_set_index:0000>", &format!("{:04}", set_index));
    }
    resolved
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
        inner.is_running = false;
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
            Self::sync_subscription_internal(
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

        Self::sync_subscription_internal(
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
    ) {
        let history_id = sub.history_entry_id;

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

                    let mut macro_ctx = MacroContext::default();
                    macro_ctx.artwork_id = novel.id.to_string();
                    macro_ctx.title = novel.title.clone();
                    macro_ctx.author_ids = vec![novel.user.id.to_string()];
                    macro_ctx.author_names = vec![novel.user.name.clone()];
                    macro_ctx.create_date = novel.create_date.clone();
                    macro_ctx.image_type = "Other".to_string();
                    macro_ctx.is_novel = true;
                    macro_ctx.is_ai = novel.novel_ai_type == 2;
                    macro_ctx.is_r18 = novel.x_restrict == 1;
                    macro_ctx.is_r18g = novel.x_restrict == 2;
                    macro_ctx.work_subscription_id = Some(history_id);
                    macro_ctx.work_subscription_type = Some(match sub.subscription_type {
                        0 => "Bookmarks".to_string(),
                        1 => "Posts".to_string(),
                        _ => "Series".to_string(),
                    });

                    let rel_path = pixeval_download::metapath::reduce(
                        &config.download_path_macro,
                        &macro_ctx,
                    )
                    .unwrap_or_else(|_| format!("{}.txt", novel.id));
                    let resolved_rel = resolve_tokens(&rel_path, "txt", -1);
                    let base_dir = config.base_download_dir.trim_end_matches(['/', '\\']);
                    let clean_rel = resolved_rel.trim_start_matches(['/', '\\']);
                    let destination = if base_dir.is_empty() {
                        clean_rel.to_string()
                    } else {
                        format!("{base_dir}/{clean_rel}")
                    };

                    let is_dup = storage
                        .contains_subscription_download_identity(
                            history_id,
                            novel.id.to_string(),
                            destination.clone(),
                        )
                        .unwrap_or(false)
                        || std::path::Path::new(&destination).exists();

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

                    let download_url = novel
                        .image_urls
                        .large
                        .clone()
                        .or(novel.image_urls.medium.clone())
                        .unwrap_or_default();
                    let payload_json = serde_json::to_string(&novel).unwrap_or_default();
                    let _ = storage.add_or_replace_subscription_download_history(
                        novel.id.to_string(),
                        Some(format!("Novel:{}", novel.id)),
                        destination.clone(),
                        0,
                        None,
                        None,
                        history_id,
                        novel.id.to_string(),
                        payload_json.clone(),
                    );

                    let key = DownloadTaskKey::new_subscription(
                        &destination,
                        history_id as i32,
                        novel.id.to_string(),
                    );
                    if callback.is_none() {
                        download.enqueue_task(key, download_url, destination.clone(), config.overwrite);
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

                    let mut macro_ctx = MacroContext::default();
                    macro_ctx.artwork_id = illust.id.to_string();
                    macro_ctx.title = illust.title.clone();
                    macro_ctx.author_ids = vec![illust.user.id.to_string()];
                    macro_ctx.author_names = vec![illust.user.name.clone()];
                    macro_ctx.create_date = illust.create_date.clone();
                    macro_ctx.image_type = if illust.page_count > 1 {
                        "ImageSet".to_string()
                    } else if illust.illust_type == "ugoira" {
                        "SingleAnimatedImage".to_string()
                    } else {
                        "SingleImage".to_string()
                    };
                    macro_ctx.is_ai = illust.illust_ai_type == 2;
                    macro_ctx.is_r18 = illust.x_restrict == 1;
                    macro_ctx.is_r18g = illust.x_restrict == 2;
                    macro_ctx.work_subscription_id = Some(history_id);
                    macro_ctx.work_subscription_type = Some(match sub.subscription_type {
                        0 => "Bookmarks".to_string(),
                        1 => "Posts".to_string(),
                        _ => "Series".to_string(),
                    });

                    let download_url = if illust.page_count <= 1 {
                        illust
                            .meta_single_page
                            .original_image_url
                            .clone()
                            .or(illust.image_urls.large.clone())
                            .or(illust.image_urls.medium.clone())
                            .unwrap_or_default()
                    } else if let Some(first_page) = illust.meta_pages.first() {
                        first_page
                            .image_urls
                            .original
                            .clone()
                            .or(first_page.image_urls.large.clone())
                            .or(first_page.image_urls.medium.clone())
                            .unwrap_or_default()
                    } else {
                        illust
                            .image_urls
                            .large
                            .clone()
                            .or(illust.image_urls.medium.clone())
                            .unwrap_or_default()
                    };

                    let ext = if download_url.contains(".png") {
                        "png"
                    } else if download_url.contains(".gif") {
                        "gif"
                    } else if download_url.contains(".zip") {
                        "zip"
                    } else {
                        "jpg"
                    };

                    let rel_path = pixeval_download::metapath::reduce(
                        &config.download_path_macro,
                        &macro_ctx,
                    )
                    .unwrap_or_else(|_| format!("{}.jpg", illust.id));
                    let resolved_rel = resolve_tokens(&rel_path, ext, 0);
                    let base_dir = config.base_download_dir.trim_end_matches(['/', '\\']);
                    let clean_rel = resolved_rel.trim_start_matches(['/', '\\']);
                    let destination = if base_dir.is_empty() {
                        clean_rel.to_string()
                    } else {
                        format!("{base_dir}/{clean_rel}")
                    };

                    let is_dup = storage
                        .contains_subscription_download_identity(
                            history_id,
                            illust.id.to_string(),
                            destination.clone(),
                        )
                        .unwrap_or(false)
                        || std::path::Path::new(&destination).exists();

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

                    let payload_json = serde_json::to_string(&illust).unwrap_or_default();
                    let _ = storage.add_or_replace_subscription_download_history(
                        illust.id.to_string(),
                        Some(format!("Illustration:{}", illust.id)),
                        destination.clone(),
                        0,
                        None,
                        None,
                        history_id,
                        illust.id.to_string(),
                        payload_json.clone(),
                    );

                    let key = DownloadTaskKey::new_subscription(
                        &destination,
                        history_id as i32,
                        illust.id.to_string(),
                    );
                    if callback.is_none() {
                        download.enqueue_task(key, download_url, destination.clone(), config.overwrite);
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
    }
}
