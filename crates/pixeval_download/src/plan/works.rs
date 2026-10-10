// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::path::Path;
use std::sync::Arc;
use std::sync::atomic::Ordering;

use pixeval_mako::{Illustration, Novel, NovelContent};
use pixeval_media::{ImageCodecFormat, UgoiraFormat};
use pixeval_novel::{NovelEngine, NovelEpubMetadataDto, NovelIllustRenderDto, NovelImageRenderDto};
use pixeval_storage::{DownloadHistoryRecord, StorageEngine, SubscriptionDownloadHistoryRecord};
use tokio_util::sync::CancellationToken;

use crate::engine::key::DownloadTaskKey;
use crate::engine::manager::DownloadManager;
use crate::engine::state::DownloadState;
use crate::metapath::MacroContext;

use super::book::{Control, NovelAssets, WorkItem, children_from_blueprint};
use super::kinds::{
    AfterDownload, NovelBuiltIn, WorkBlueprint, build_external, build_external_from_json, codec_of,
    fill_novel_images, fill_ugoira_frames, flex_deserialize, original_ugoira_url, plan_pixiv, probe_paths,
    replan_stored, ugoira_native_format,
};
use super::snapshot::{
    state_code, state_from_code, DownloadFolderMeta, DownloadFormatEncoder, DownloadPageCallback, DownloadPageSnapshot,
    DownloadPolicy, ExternalImageRequest, FolderFetchState, PlannedDownloadProbe,
};
use super::transfer::{TransferOutcome, commit_file, download_file, remove_path};

#[uniffi::export]
impl DownloadManager {
    pub fn bind_storage(&self, storage: Arc<StorageEngine>) {
        *self.storage.write() = Some(storage);
    }

    pub fn bind_mako(&self, mako: Arc<pixeval_mako::MakoClient>) {
        *self.mako.write() = Some(mako);
    }

    pub fn set_page_callback(&self, callback: Option<Box<dyn DownloadPageCallback>>) {
        *self.page_callback.write() = callback.map(Arc::from);
    }

    pub fn set_format_encoder(&self, encoder: Option<Box<dyn DownloadFormatEncoder>>) {
        *self.encoder.write() = encoder.map(Arc::from);
    }

    pub fn set_download_policy(&self, policy: DownloadPolicy) {
        *self.policy.write() = policy;
    }

    pub fn set_overwrite(&self, overwrite: bool) {
        self.policy.write().overwrite = overwrite;
    }

    pub fn current_page_snapshot(&self) -> DownloadPageSnapshot {
        self.book.read().snapshot()
    }

    pub fn set_subscriptions(&self, folders: Vec<DownloadFolderMeta>) {
        self.book.write().folders = folders;
        self.publish(true);
    }

    pub fn set_folder_fetch(
        &self,
        subscription_id: i64,
        is_fetching: bool,
        fetched_count: u32,
        retry_at_timestamp: Option<i64>,
    ) {
        self.book.write().fetches.insert(
            subscription_id,
            FolderFetchState {
                is_fetching,
                fetched_count,
                retry_at_timestamp,
            },
        );
        self.publish(true);
    }

    pub fn enqueue_pixiv(
        &self,
        payload_json: String,
        is_novel: bool,
        page_index: i32,
        path_macro: String,
        base_dir: String,
        work_subscription_id: i64,
        subscription_type: String,
    ) -> String {
        let policy = self.policy.read().clone();
        let Ok(blueprint) = plan_pixiv(
            &payload_json,
            is_novel,
            page_index,
            &path_macro,
            &base_dir,
            work_subscription_id,
            &subscription_type,
            &policy,
        ) else {
            return String::new();
        };
        self.install(blueprint, work_subscription_id, true)
    }

    pub fn enqueue_external_image(&self, request: ExternalImageRequest) -> String {
        let policy = self.policy.read().clone();
        let Ok(blueprint) = plan_external(&request, &policy) else {
            return String::new();
        };
        self.install(blueprint, request.work_subscription_id, true)
    }

    pub fn plan_pixiv_probe(
        &self,
        payload_json: String,
        is_novel: bool,
        page_index: i32,
        path_macro: String,
        base_dir: String,
        work_subscription_id: i64,
        subscription_type: String,
    ) -> PlannedDownloadProbe {
        let policy = self.policy.read().clone();
        match plan_pixiv(
            &payload_json,
            is_novel,
            page_index,
            &path_macro,
            &base_dir,
            work_subscription_id,
            &subscription_type,
            &policy,
        ) {
            Ok(plan) => PlannedDownloadProbe {
                destination: plan.destination.clone(),
                probe_paths: probe_paths(&plan),
            },
            Err(_) => PlannedDownloadProbe::default(),
        }
    }

    pub fn restore_histories(&self) {
        let Some(storage) = self.storage.read().clone() else {
            return;
        };
        for record in load_history(&storage) {
            self.restore_record(record, 0, String::new());
        }
        for record in load_subscription_history(&storage) {
            self.restore_record(
                HistoryRow::from_subscription(record.0),
                record.1,
                record.2,
            );
        }
        self.publish(true);
    }

    pub fn pause_work(&self, key: DownloadTaskKey) -> bool {
        let persist = {
            let mut book = self.book.write();
            let Some(work) = book.works.get_mut(&key) else {
                return false;
            };
            let (state, _, _, _, _, _) = work.presentation();
            if !matches!(state, DownloadState::Queued | DownloadState::Running) {
                return false;
            }
            work.control = Control::Pause;
            work.token.cancel();
            for child in &mut work.children {
                if matches!(child.state, DownloadState::Queued | DownloadState::Running) {
                    child.state = DownloadState::Paused;
                }
            }
            work.state = DownloadState::Paused;
            work.pending = false;
            work.is_processing = false;
            work.initialized = true;
            Persist::from_work(work)
        };
        self.persist_state(&persist);
        self.publish(true);
        true
    }

    pub fn resume_work(&self, key: DownloadTaskKey) -> bool {
        let generation = {
            let mut book = self.book.write();
            let Some(work) = book.works.get_mut(&key) else {
                return false;
            };
            let (state, _, _, _, _, _) = work.presentation();
            if state != DownloadState::Paused && work.state != DownloadState::Paused {
                return false;
            }
            work.control = Control::Run;
            work.token = CancellationToken::new();
            work.generation += 1;
            work.pending = false;
            work.error = None;
            for child in &mut work.children {
                if child.state == DownloadState::Paused {
                    child.state = DownloadState::Queued;
                    child.error = None;
                }
            }
            work.state = DownloadState::Queued;
            work.initialized = !work.children.is_empty() || !work.blueprint.defer_ugoira && !work.blueprint.defer_novel;
            work.generation
        };
        self.spawn_work(key, generation);
        self.publish(true);
        true
    }

    pub fn cancel_work(&self, key: DownloadTaskKey) -> bool {
        let (persist, temps) = {
            let mut book = self.book.write();
            let Some(work) = book.works.get_mut(&key) else {
                return false;
            };
            let (state, _, _, _, _, _) = work.presentation();
            if !matches!(
                state,
                DownloadState::Queued | DownloadState::Running | DownloadState::Paused | DownloadState::Pending
            ) {
                return false;
            }
            work.control = Control::Cancel;
            work.token.cancel();
            let mut temps = Vec::new();
            for child in &mut work.children {
                if child.state != DownloadState::Completed {
                    temps.push(child.destination.clone());
                    child.state = DownloadState::Cancelled;
                }
            }
            work.state = DownloadState::Cancelled;
            work.pending = false;
            work.is_processing = false;
            work.initialized = true;
            (Persist::from_work(work), temps)
        };
        Self::spawn_worker(async move {
            for path in temps {
                remove_path(&format!("{path}.pixevaldownloading"));
            }
        });
        self.persist_state(&persist);
        self.publish(true);
        true
    }

    pub fn reset_work(&self, key: DownloadTaskKey) -> bool {
        let generation = {
            let mut book = self.book.write();
            let Some(work) = book.works.get_mut(&key) else {
                return false;
            };
            let (state, _, _, _, _, _) = work.presentation();
            if !matches!(state, DownloadState::Completed | DownloadState::Error | DownloadState::Cancelled) {
                return false;
            }
            let only_errors = state == DownloadState::Error;
            delete_final_output(work);
            work.control = Control::Run;
            work.token = CancellationToken::new();
            work.generation += 1;
            work.pending = false;
            work.error = None;
            work.is_processing = false;
            if work.blueprint.defer_ugoira || work.blueprint.defer_novel {
                work.children.clear();
                work.initialized = false;
            }
            for child in &mut work.children {
                let redo = !only_errors || child.state == DownloadState::Error || child.error.is_some();
                if redo {
                    remove_path(&child.destination);
                    child.state = DownloadState::Queued;
                    child.progress = 0.0;
                    child.error = None;
                    child.skipped = false;
                }
            }
            work.state = DownloadState::Queued;
            let generation = work.generation;
            let persist = Persist::from_work(work);
            drop(book);
            self.persist_state(&persist);
            generation
        };
        self.spawn_work(key, generation);
        self.publish(true);
        true
    }

    pub fn remove_work(&self, key: DownloadTaskKey, delete_local_files: bool) -> bool {
        let removed = {
            let mut book = self.book.write();
            let Some(work) = book.works.remove(&key) else {
                return false;
            };
            work.token.cancel();
            book.order.retain(|existing| existing != &key);
            if delete_local_files {
                delete_local_work(&work);
            }
            Persist::from_work(&work)
        };
        self.delete_history(&removed);
        self.publish(true);
        true
    }

    pub fn remove_subscription(&self, subscription_id: i64) {
        let mut removed = Vec::new();
        {
            let mut book = self.book.write();
            book.folders.retain(|folder| folder.subscription_id != subscription_id);
            book.fetches.remove(&subscription_id);
            let keys: Vec<_> = book
                .works
                .iter()
                .filter(|(_, work)| work.subscription_id == subscription_id)
                .map(|(key, _)| key.clone())
                .collect();
            for key in keys {
                if let Some(work) = book.works.remove(&key) {
                    work.token.cancel();
                    removed.push(Persist::from_work(&work));
                }
                book.order.retain(|existing| existing != &key);
            }
        }
        if let Some(storage) = self.storage.read().clone() {
            let _ = storage.delete_subscription_downloads_by_work_subscription_id(subscription_id);
        }
        let _ = removed;
        self.publish(true);
    }
}

impl DownloadManager {
    fn install(&self, blueprint: WorkBlueprint, subscription_id: i64, start: bool) -> String {
        let destination = blueprint.destination.clone();
        let key = work_key(&destination, &blueprint.artwork_id, subscription_id);
        let generation = {
            let mut book = self.book.write();
            let next_generation = book.works.get(&key).map(|work| work.generation.saturating_add(1)).unwrap_or(1);
            if let Some(old) = book.works.get(&key) {
                old.token.cancel();
            }
            let mut item = WorkItem::fresh(key.clone(), blueprint, subscription_id);
            item.generation = next_generation;
            let persist = Persist::from_work(&item);
            book.insert_front(key.clone());
            book.works.insert(key.clone(), item);
            drop(book);
            self.write_history_full(&persist);
            next_generation
        };
        if start {
            self.spawn_work(key, generation);
        }
        self.publish(true);
        destination
    }

    fn restore_record(&self, row: HistoryRow, subscription_id: i64, artwork_fallback: String) {
        let payload = row.payload_json.clone().unwrap_or_default();
        let format_token = row.format_token.clone().unwrap_or_else(|| "Original".to_string());
        let mut blueprint = replan_stored(&payload, &row.destination, &format_token, row.serialize_key.as_deref())
            .unwrap_or_else(|_| {
                build_external_from_json(&payload, &row.destination, &format_token, row.serialize_key.as_deref().unwrap_or(""))
            });
        if blueprint.artwork_id.is_empty() {
            blueprint.artwork_id = if artwork_fallback.is_empty() {
                row.id.clone()
            } else {
                artwork_fallback
            };
        }
        if blueprint.title.is_empty() {
            blueprint.title = blueprint.artwork_id.clone();
        }
        let key = work_key(&blueprint.destination, &blueprint.artwork_id, subscription_id);
        let state = state_from_code(row.state);
        let mut item = WorkItem::fresh(key.clone(), blueprint, subscription_id);
        item.state = state;
        item.error = row.error_message.clone();
        item.control = if state == DownloadState::Paused {
            Control::Pause
        } else {
            Control::Run
        };
        if state == DownloadState::Completed && !item.blueprint.defer_ugoira && !item.blueprint.defer_novel {
            item.initialized = true;
            for child in &mut item.children {
                child.state = DownloadState::Completed;
                child.progress = 100.0;
                child.skipped = true;
            }
        }
        let start = state == DownloadState::Queued;
        let generation = item.generation;
        {
            let mut book = self.book.write();
            book.insert_back(key.clone());
            book.works.insert(key.clone(), item);
        }
        if start {
            self.spawn_work(key, generation);
        }
    }

    fn spawn_work(&self, key: DownloadTaskKey, generation: u64) {
        let weak = self.me.clone();
        Self::spawn_worker(async move {
            run_work(weak, key, generation).await;
        });
    }

    fn publish(&self, immediate: bool) {
        if immediate {
            self.emit_snapshot();
            return;
        }
        if self.snapshot_emit_scheduled.swap(true, Ordering::AcqRel) {
            return;
        }
        let weak = self.me.clone();
        Self::spawn_worker(async move {
            tokio::time::sleep(std::time::Duration::from_millis(100)).await;
            let Some(manager) = weak.upgrade() else {
                return;
            };
            manager.snapshot_emit_scheduled.store(false, Ordering::Release);
            manager.emit_snapshot();
        });
    }

    fn emit_snapshot(&self) {
        let snapshot = self.book.read().snapshot();
        let callback = self.page_callback.read().clone();
        if let Some(callback) = callback {
            callback.on_page_snapshot(snapshot);
        }
    }

    fn overwrite(&self) -> bool {
        self.policy.read().overwrite
    }

    fn is_running(&self, key: &DownloadTaskKey, generation: u64) -> bool {
        let book = self.book.read();
        let Some(work) = book.works.get(key) else {
            return false;
        };
        work.generation == generation && work.control == Control::Run
    }

    fn write_history_full(&self, persist: &Persist) {
        let Some(storage) = self.storage.read().clone() else {
            return;
        };
        let state = state_code(persist.state);
        if persist.subscription_id == 0 {
            let _ = storage.add_or_replace_download_history(
                persist.artwork_id.clone(),
                Some(persist.serialize_key.clone()),
                persist.destination.clone(),
                state,
                Some(persist.format_token.clone()),
                persist.error.clone(),
                persist.payload_json.clone(),
            );
        } else {
            let _ = storage.add_or_replace_subscription_download_history(
                persist.artwork_id.clone(),
                Some(persist.serialize_key.clone()),
                persist.destination.clone(),
                state,
                Some(persist.format_token.clone()),
                persist.error.clone(),
                persist.subscription_id,
                persist.artwork_id.clone(),
                persist.payload_json.clone(),
            );
        }
    }

    fn persist_state(&self, persist: &Persist) {
        let Some(storage) = self.storage.read().clone() else {
            return;
        };
        let state = state_code(persist.state);
        if persist.subscription_id == 0 {
            let _ = storage.update_download_history_state(persist.destination.clone(), state, persist.error.clone());
        } else {
            let _ = storage.update_subscription_download_history_state(
                persist.subscription_id,
                persist.artwork_id.clone(),
                persist.destination.clone(),
                state,
                persist.error.clone(),
            );
        }
    }

    fn delete_history(&self, persist: &Persist) {
        let Some(storage) = self.storage.read().clone() else {
            return;
        };
        if persist.subscription_id == 0 {
            let _ = storage.try_delete_download_history_by_destination(persist.destination.clone());
        } else {
            let _ = storage.try_delete_subscription_download_by_identity(
                persist.subscription_id,
                persist.artwork_id.clone(),
                persist.destination.clone(),
            );
        }
    }
}

struct Persist {
    subscription_id: i64,
    artwork_id: String,
    destination: String,
    state: DownloadState,
    error: Option<String>,
    serialize_key: String,
    format_token: String,
    payload_json: String,
}

impl Persist {
    fn from_work(work: &WorkItem) -> Self {
        Self {
            subscription_id: work.subscription_id,
            artwork_id: work.blueprint.artwork_id.clone(),
            destination: work.blueprint.destination.clone(),
            state: work.state,
            error: work.error.clone(),
            serialize_key: work.blueprint.serialize_key.clone(),
            format_token: work.blueprint.format_token.clone(),
            payload_json: work.blueprint.payload_json.clone(),
        }
    }
}

struct HistoryRow {
    id: String,
    serialize_key: Option<String>,
    destination: String,
    state: u32,
    format_token: Option<String>,
    error_message: Option<String>,
    payload_json: Option<String>,
}

impl HistoryRow {
    fn from_download(record: DownloadHistoryRecord) -> Self {
        Self {
            id: record.id,
            serialize_key: record.serialize_key,
            destination: record.destination,
            state: record.state,
            format_token: record.format_token,
            error_message: record.error_message,
            payload_json: record.payload_json,
        }
    }

    fn from_subscription(record: SubscriptionDownloadHistoryRecord) -> Self {
        Self {
            id: record.artwork_id.clone(),
            serialize_key: record.serialize_key,
            destination: record.destination,
            state: record.state,
            format_token: record.format_token,
            error_message: record.error_message,
            payload_json: record.payload_json,
        }
    }
}

fn load_history(storage: &StorageEngine) -> Vec<HistoryRow> {
    let mut rows = Vec::new();
    let mut cursor = None;
    loop {
        let Ok(page) = storage.stream_download_history_cursor(cursor, 200) else {
            break;
        };
        if page.is_empty() {
            break;
        }
        cursor = page.last().map(|record| record.history_entry_id);
        let count = page.len();
        rows.extend(page.into_iter().map(HistoryRow::from_download));
        if count < 200 {
            break;
        }
    }
    rows
}

fn load_subscription_history(storage: &StorageEngine) -> Vec<(SubscriptionDownloadHistoryRecord, i64, String)> {
    let mut rows = Vec::new();
    let mut cursor = None;
    loop {
        let Ok(page) = storage.stream_subscription_download_history_cursor(cursor, 200) else {
            break;
        };
        if page.is_empty() {
            break;
        }
        cursor = page.last().map(|record| record.history_entry_id);
        let count = page.len();
        rows.extend(page.into_iter().map(|record| {
            let subscription_id = record.work_subscription_id;
            let artwork_id = record.artwork_id.clone();
            (record, subscription_id, artwork_id)
        }));
        if count < 200 {
            break;
        }
    }
    rows
}

fn work_key(destination: &str, artwork_id: &str, subscription_id: i64) -> DownloadTaskKey {
    DownloadTaskKey {
        destination: destination.to_string(),
        work_subscription_id: if subscription_id == 0 { 0 } else { subscription_id as i32 },
        artwork_id: Some(artwork_id.to_string()),
    }
}

fn plan_external(request: &ExternalImageRequest, policy: &DownloadPolicy) -> Result<WorkBlueprint, String> {
    let mut context = MacroContext::default();
    context.artwork_id = request.artwork_id.clone();
    context.title = request.title.clone();
    context.author_names = vec![request.author.clone()];
    context.create_date = request.create_date.clone();
    context.image_type = "SingleImage".to_string();
    context.is_r18 = request.is_r18;
    context.is_r18g = request.is_r18g;
    context.set_index = -1;
    if request.work_subscription_id != 0 {
        context.work_subscription_id = Some(request.work_subscription_id);
    }
    if !request.subscription_type.is_empty() {
        context.work_subscription_type = Some(request.subscription_type.clone());
    }
    let destination = super::kinds::reduce_destination(&request.path_macro, &request.base_dir, &context)?;
    Ok(build_external(
        request.artwork_id.clone(),
        request.title.clone(),
        request.author.clone(),
        request.thumbnail_url.clone(),
        request.website_uri.clone(),
        request.app_uri.clone(),
        request.original_url.clone(),
        request.payload_json.clone(),
        request.serialize_key.clone(),
        destination,
        policy.illustration_format.clone(),
    ))
}

async fn run_work(weak: std::sync::Weak<DownloadManager>, key: DownloadTaskKey, generation: u64) {
    let Some(manager) = weak.upgrade() else {
        return;
    };
    if !manager.is_running(&key, generation) {
        return;
    }
    if let Err(err) = prepare(&manager, &key, generation).await {
        if manager.is_running(&key, generation) {
            fail_work(&manager, &key, generation, err);
        }
        return;
    }
    if !manager.is_running(&key, generation) {
        return;
    }
    if skip_final_output(&manager, &key, generation) {
        return;
    }
    let jobs = {
        let book = manager.book.read();
        let Some(work) = book.works.get(&key) else {
            return;
        };
        work.children
            .iter()
            .enumerate()
            .filter(|(_, child)| child.state == DownloadState::Queued)
            .map(|(index, child)| (index, child.url.clone(), child.destination.clone()))
            .collect::<Vec<_>>()
    };
    let mut joins = Vec::new();
    for (index, url, destination) in jobs {
        let weak = weak.clone();
        let key = key.clone();
        joins.push(tokio::spawn(async move {
            download_child(weak, key, generation, index, url, destination).await;
        }));
    }
    for join in joins {
        let _ = join.await;
    }
    let Some(manager) = weak.upgrade() else {
        return;
    };
    if !manager.is_running(&key, generation) {
        return;
    }
    if !all_children_completed(&manager, &key) {
        settle_incomplete(&manager, &key, generation);
        return;
    }
    mark_pending(&manager, &key, generation);
    if !manager.is_running(&key, generation) {
        return;
    }
    if let Err(err) = post_process(&manager, &key, generation).await {
        if manager.is_running(&key, generation) {
            fail_work(&manager, &key, generation, err);
        }
        return;
    }
    if manager.is_running(&key, generation) {
        complete_work(&manager, &key, generation);
    }
}

async fn prepare(manager: &DownloadManager, key: &DownloadTaskKey, generation: u64) -> Result<(), String> {
    let (defer_ugoira, defer_novel, artwork_id, payload) = {
        let book = manager.book.read();
        let Some(work) = book.works.get(key) else {
            return Ok(());
        };
        if work.generation != generation {
            return Ok(());
        }
        (
            work.blueprint.defer_ugoira,
            work.blueprint.defer_novel,
            work.blueprint.artwork_id.clone(),
            work.blueprint.payload_json.clone(),
        )
    };

    if defer_ugoira {
        let mako = manager.mako.read().clone().ok_or_else(|| "Pixiv client is not available".to_string())?;
        let id = artwork_id.parse::<i64>().map_err(|_| "Invalid illustration id".to_string())?;
        let metadata = mako.get_ugoira_metadata(id).await.map_err(|err| err.to_string())?;
        let illust: Illustration = flex_deserialize(&payload)?;
        let original = original_ugoira_url(&illust);
        let mut book = manager.book.write();
        let Some(work) = book.works.get_mut(key) else {
            return Ok(());
        };
        if work.generation != generation {
            return Ok(());
        }
        fill_ugoira_frames(&mut work.blueprint, &original, &metadata);
        work.children = children_from_blueprint(&work.blueprint);
    }

    if defer_novel {
        let mako = manager.mako.read().clone().ok_or_else(|| "Pixiv client is not available".to_string())?;
        let novel: Novel = flex_deserialize(&payload)?;
        let mut content = mako.get_novel_content_structured(novel.id).await.map_err(|err| err.to_string())?;
        if content.title.trim().is_empty() {
            content.title = novel.title.clone();
        }
        if content.cover_url.trim().is_empty() {
            content.cover_url = novel
                .image_urls
                .square_medium
                .clone()
                .or(novel.image_urls.medium.clone())
                .unwrap_or_default();
        }
        if content.user_id == 0 {
            content.user_id = novel.user.id;
        }
        let mut book = manager.book.write();
        let Some(work) = book.works.get_mut(key) else {
            return Ok(());
        };
        if work.generation != generation {
            return Ok(());
        }
        fill_novel_images(&mut work.blueprint, &content);
        work.children = children_from_blueprint(&work.blueprint);
        work.novel = NovelAssets {
            text: content.text.clone(),
            title: if content.title.is_empty() { novel.title.clone() } else { content.title.clone() },
            author: novel.user.name.clone(),
            language: if content.language.is_empty() { "ja".to_string() } else { content.language.clone() },
            caption: content.caption.clone(),
            content: Some(content),
        };
        if work.blueprint.title.is_empty() {
            work.blueprint.title = work.novel.title.clone();
        }
    }

    {
        let mut book = manager.book.write();
        let Some(work) = book.works.get_mut(key) else {
            return Ok(());
        };
        if work.generation != generation {
            return Ok(());
        }
        if work.children.is_empty() {
            work.children = children_from_blueprint(&work.blueprint);
        }
        work.initialized = true;
        if work.control == Control::Run {
            work.state = if work.children.is_empty() {
                DownloadState::Pending
            } else {
                DownloadState::Running
            };
        }
    }
    manager.publish(true);
    Ok(())
}

fn skip_final_output(manager: &DownloadManager, key: &DownloadTaskKey, generation: u64) -> bool {
    let overwrite = manager.overwrite();
    let mut book = manager.book.write();
    let Some(work) = book.works.get_mut(key) else {
        return false;
    };
    if work.generation != generation || !work.blueprint.skip_when_exists || overwrite {
        return false;
    }
    if !final_file_exists(&work.blueprint) {
        return false;
    }
    for child in &mut work.children {
        child.state = DownloadState::Completed;
        child.progress = 100.0;
        child.skipped = true;
    }
    work.initialized = true;
    work.pending = false;
    work.state = DownloadState::Completed;
    work.error = None;
    let persist = Persist::from_work(work);
    drop(book);
    manager.persist_state(&persist);
    manager.publish(true);
    true
}

fn final_file_exists(blueprint: &WorkBlueprint) -> bool {
    match &blueprint.after {
        AfterDownload::WriteNovel(output) => Path::new(&output.novel_file).is_file(),
        AfterDownload::SynthesizeUgoira { destination, .. } | AfterDownload::EncodeUgoira { destination, .. } => {
            Path::new(destination).is_file()
        }
        _ => false,
    }
}

async fn download_child(
    weak: std::sync::Weak<DownloadManager>,
    key: DownloadTaskKey,
    generation: u64,
    index: usize,
    url: String,
    destination: String,
) {
    let Some(manager) = weak.upgrade() else {
        return;
    };
    if !manager.is_running(&key, generation) {
        return;
    }
    let client = manager.client.read().clone();
    let overwrite = manager.overwrite();
    let token = {
        let book = manager.book.read();
        let Some(work) = book.works.get(&key) else {
            return;
        };
        work.token.clone()
    };
    let weak_progress = weak.clone();
    let key_progress = key.clone();
    let outcome = download_file(
        &client,
        &manager.semaphore,
        &token,
        &url,
        &destination,
        overwrite,
        |progress| {
            let Some(manager) = weak_progress.upgrade() else {
                return;
            };
            set_child_progress(&manager, &key_progress, generation, index, progress);
        },
    )
    .await;
    let Some(manager) = weak.upgrade() else {
        return;
    };
    apply_child_outcome(&manager, &key, generation, index, outcome);
}

fn set_child_progress(manager: &DownloadManager, key: &DownloadTaskKey, generation: u64, index: usize, progress: f64) {
    let immediate = {
        let mut book = manager.book.write();
        let Some(work) = book.works.get_mut(key) else {
            return;
        };
        if work.generation != generation || work.control != Control::Run {
            return;
        }
        let Some(child) = work.children.get_mut(index) else {
            return;
        };
        if child.state == DownloadState::Completed || (progress - child.progress).abs() < 1.0 && progress < 100.0 {
            return;
        }
        let started = child.state == DownloadState::Queued;
        child.progress = progress;
        if started {
            child.state = DownloadState::Running;
            work.state = DownloadState::Running;
        }
        started
    };
    manager.publish(immediate);
}

fn apply_child_outcome(
    manager: &DownloadManager,
    key: &DownloadTaskKey,
    generation: u64,
    index: usize,
    outcome: TransferOutcome,
) {
    let mut book = manager.book.write();
    let Some(work) = book.works.get_mut(key) else {
        return;
    };
    if work.generation != generation {
        return;
    }
    let Some(child) = work.children.get_mut(index) else {
        return;
    };
    match work.control {
        Control::Pause => {
            if matches!(outcome, TransferOutcome::Completed { .. }) {
                child.state = DownloadState::Completed;
                child.progress = 100.0;
                child.skipped = matches!(outcome, TransferOutcome::Completed { skipped: true });
            } else if child.state != DownloadState::Completed {
                child.state = DownloadState::Paused;
            }
        }
        Control::Cancel => {
            if child.state != DownloadState::Completed {
                child.state = DownloadState::Cancelled;
            }
        }
        Control::Run => match outcome {
            TransferOutcome::Completed { skipped } => {
                child.state = DownloadState::Completed;
                child.progress = 100.0;
                child.skipped = skipped;
                child.error = None;
            }
            TransferOutcome::Failed(message) => {
                child.state = DownloadState::Error;
                child.error = Some(message);
            }
            TransferOutcome::Cancelled => {
                if child.state != DownloadState::Completed {
                    child.state = DownloadState::Paused;
                }
            }
        },
    }
    if work.control == Control::Run {
        if work.error.is_none() {
            work.error = work.children.iter().find_map(|child| child.error.clone());
        }
        let (state, _, _, _, _, _) = work.presentation();
        if !work.pending {
            work.state = state;
        }
    }
    drop(book);
    manager.publish(false);
}

fn all_children_completed(manager: &DownloadManager, key: &DownloadTaskKey) -> bool {
    let book = manager.book.read();
    let Some(work) = book.works.get(key) else {
        return false;
    };
    work.children.iter().all(|child| child.state == DownloadState::Completed)
}

fn settle_incomplete(manager: &DownloadManager, key: &DownloadTaskKey, generation: u64) {
    let persist = {
        let mut book = manager.book.write();
        let Some(work) = book.works.get_mut(key) else {
            return;
        };
        if work.generation != generation || work.control != Control::Run {
            return;
        }
        let (state, _, _, _, _, _) = work.presentation();
        work.state = state;
        work.pending = false;
        work.is_processing = false;
        if work.error.is_none() {
            work.error = work.children.iter().find_map(|child| child.error.clone());
        }
        Persist::from_work(work)
    };
    if matches!(persist.state, DownloadState::Error | DownloadState::Cancelled | DownloadState::Paused | DownloadState::Completed) {
        manager.persist_state(&persist);
    }
    manager.publish(true);
}

fn mark_pending(manager: &DownloadManager, key: &DownloadTaskKey, generation: u64) {
    {
        let mut book = manager.book.write();
        let Some(work) = book.works.get_mut(key) else {
            return;
        };
        if work.generation != generation || work.control != Control::Run {
            return;
        }
        work.pending = true;
        work.is_processing = true;
        work.state = DownloadState::Pending;
    }
    manager.publish(true);
}

fn complete_work(manager: &DownloadManager, key: &DownloadTaskKey, generation: u64) {
    let persist = {
        let mut book = manager.book.write();
        let Some(work) = book.works.get_mut(key) else {
            return;
        };
        if work.generation != generation || work.control != Control::Run {
            return;
        }
        work.pending = false;
        work.is_processing = false;
        work.error = None;
        work.state = DownloadState::Completed;
        for child in &mut work.children {
            child.state = DownloadState::Completed;
            child.progress = 100.0;
        }
        Persist::from_work(work)
    };
    manager.persist_state(&persist);
    manager.publish(true);
}

fn fail_work(manager: &DownloadManager, key: &DownloadTaskKey, generation: u64, message: String) {
    let persist = {
        let mut book = manager.book.write();
        let Some(work) = book.works.get_mut(key) else {
            return;
        };
        if work.generation != generation || work.control != Control::Run {
            return;
        }
        work.pending = false;
        work.is_processing = false;
        work.error = Some(message);
        work.state = DownloadState::Error;
        Persist::from_work(work)
    };
    manager.persist_state(&persist);
    manager.publish(true);
}

async fn post_process(manager: &DownloadManager, key: &DownloadTaskKey, generation: u64) -> Result<(), String> {
    let job = {
        let book = manager.book.read();
        let Some(work) = book.works.get(key) else {
            return Ok(());
        };
        if work.generation != generation || work.control != Control::Run {
            return Ok(());
        }
        AfterJob::from_work(work)
    };
    if job.token.is_cancelled() {
        return Ok(());
    }
    let overwrite = manager.overwrite();
    let encoder = manager.encoder.read().clone();
    tokio::task::spawn_blocking(move || execute_after(job, overwrite, encoder))
        .await
        .map_err(|err| err.to_string())?
}

struct AfterJob {
    after: AfterDownload,
    token: CancellationToken,
    children: Vec<(String, bool)>,
    delays_ms: Vec<u32>,
    novel: NovelAssets,
    novel_id: i64,
}

impl AfterJob {
    fn from_work(work: &WorkItem) -> Self {
        Self {
            after: work.blueprint.after.clone(),
            token: work.token.clone(),
            children: work
                .children
                .iter()
                .map(|child| (child.destination.clone(), child.skipped))
                .collect(),
            delays_ms: work.blueprint.delays_ms.clone(),
            novel: work.novel.clone(),
            novel_id: work.blueprint.novel_id,
        }
    }
}

fn execute_after(
    job: AfterJob,
    overwrite: bool,
    encoder: Option<Arc<dyn DownloadFormatEncoder>>,
) -> Result<(), String> {
    if job.token.is_cancelled() {
        return Ok(());
    }
    match &job.after {
        AfterDownload::None => Ok(()),
        AfterDownload::EncodeEach { extension } => encode_each(&job, extension, overwrite, encoder.as_deref()),
        AfterDownload::PackManga { format, archive_path } => {
            let paths = job.children.iter().map(|(path, _)| path.clone()).collect();
            pixeval_media::manga::pack_manga(paths, archive_path, *format).map_err(|err| err.to_string())
        }
        AfterDownload::SynthesizeUgoira { format, destination, folder } => {
            synthesize(&job, *format, destination, folder, overwrite)
        }
        AfterDownload::WriteDelayCsv { path } => write_delay_csv(&job.delays_ms, path),
        AfterDownload::EncodeUgoira { extension, destination, folder } => {
            encode_ugoira(&job, extension, destination, folder, overwrite, encoder.as_deref())
        }
        AfterDownload::WriteNovel(output) => write_novel(&job, output, overwrite, encoder.as_deref()),
    }
}

fn encode_each(
    job: &AfterJob,
    extension: &str,
    overwrite: bool,
    encoder: Option<&dyn DownloadFormatEncoder>,
) -> Result<(), String> {
    let _ = overwrite;
    for (path, skipped) in &job.children {
        if job.token.is_cancelled() {
            return Ok(());
        }
        if *skipped || !Path::new(path).is_file() {
            continue;
        }
        if let Some(codec) = codec_of(extension) {
            transcode_in_place(path, codec)?;
            continue;
        }
        let Some(encoder) = encoder else {
            return Err(format!("No encoder for .{extension}"));
        };
        let source = format!("{path}.source");
        if Path::new(&source).exists() {
            let _ = std::fs::remove_file(&source);
        }
        std::fs::rename(path, &source).map_err(|err| format!("Rename error: {err}"))?;
        let message = encoder.encode_static_image(source.clone(), path.clone(), extension.to_string());
        let _ = std::fs::remove_file(&source);
        if !message.is_empty() {
            return Err(message);
        }
    }
    Ok(())
}

fn transcode_in_place(path: &str, codec: ImageCodecFormat) -> Result<(), String> {
    let source = format!("{path}.source");
    if Path::new(&source).exists() {
        let _ = std::fs::remove_file(&source);
    }
    std::fs::rename(path, &source).map_err(|err| format!("Rename error: {err}"))?;
    let result = pixeval_media::transcode::transcode_file(&source, path, codec, None).map_err(|err| err.to_string());
    let _ = std::fs::remove_file(&source);
    result
}

fn synthesize(job: &AfterJob, format: UgoiraFormat, destination: &str, folder: &str, overwrite: bool) -> Result<(), String> {
    if Path::new(destination).is_file() && !overwrite {
        cleanup_frames(job, folder);
        return Ok(());
    }
    let paths = job.children.iter().map(|(path, _)| path.clone()).collect();
    let delays = job.delays_ms.iter().copied().map(|delay| delay.max(1)).collect();
    pixeval_media::ugoira::synthesize_ugoira_from_frames(paths, destination, format, delays)
        .map_err(|err| err.to_string())?;
    cleanup_frames(job, folder);
    Ok(())
}

fn encode_ugoira(
    job: &AfterJob,
    extension: &str,
    destination: &str,
    folder: &str,
    overwrite: bool,
    encoder: Option<&dyn DownloadFormatEncoder>,
) -> Result<(), String> {
    if Path::new(destination).is_file() && !overwrite {
        cleanup_frames(job, folder);
        return Ok(());
    }
    let Some(encoder) = encoder else {
        return Err(format!("No encoder for .{extension}"));
    };
    if ugoira_native_format(extension).is_some() {
        return Err(format!("Native ugoira format .{extension} was not synthesized"));
    }
    let paths = job.children.iter().map(|(path, _)| path.clone()).collect();
    let delays = job.delays_ms.iter().copied().map(|delay| delay.max(1)).collect();
    let message = encoder.encode_animated(paths, delays, destination.to_string(), extension.to_string());
    if !message.is_empty() {
        return Err(message);
    }
    cleanup_frames(job, folder);
    Ok(())
}

fn cleanup_frames(job: &AfterJob, folder: &str) {
    for (path, _) in &job.children {
        remove_path(path);
    }
    let _ = std::fs::remove_dir(folder);
}

fn write_delay_csv(delays: &[u32], path: &str) -> Result<(), String> {
    if let Some(parent) = Path::new(path).parent() {
        std::fs::create_dir_all(parent).map_err(|err| format!("Create directory error: {err}"))?;
    }
    let body = delays.iter().map(|delay| delay.to_string()).collect::<Vec<_>>().join(",");
    std::fs::write(path, body).map_err(|err| format!("Write error: {err}"))
}

fn write_novel(
    job: &AfterJob,
    output: &super::kinds::NovelOutput,
    overwrite: bool,
    encoder: Option<&dyn DownloadFormatEncoder>,
) -> Result<(), String> {
    if Path::new(&output.novel_file).is_file() && !overwrite {
        if output.delete_images_after {
            cleanup_frames(job, &output.image_folder);
        }
        return Ok(());
    }
    let content = job.novel.content.clone().ok_or_else(|| "Novel content is missing".to_string())?;
    if let Some(extension) = &output.extension {
        let Some(encoder) = encoder else {
            return Err(format!("No encoder for .{extension}"));
        };
        let paths = job.children.iter().map(|(path, _)| path.clone()).collect();
        let message = encoder.encode_novel(job.novel.text.clone(), paths, output.novel_file.clone(), extension.clone());
        if output.delete_images_after {
            cleanup_frames(job, &output.image_folder);
        }
        return if message.is_empty() { Ok(()) } else { Err(message) };
    }

    let built_in = output.built_in.unwrap_or(NovelBuiltIn::Txt);
    let body = match built_in {
        NovelBuiltIn::Txt => job.novel.text.clone(),
        NovelBuiltIn::Html => render_novel_html(job, &content),
        NovelBuiltIn::Markdown => render_novel_markdown(job, &content),
        NovelBuiltIn::Epub => {
            let bytes = render_novel_epub(job, &content)?;
            return commit_bytes(&bytes, &output.novel_file, overwrite, output.delete_images_after, job, &output.image_folder);
        }
    };
    let temp = format!("{}.pixevaldownloading", output.novel_file);
    if let Some(parent) = Path::new(&temp).parent() {
        std::fs::create_dir_all(parent).map_err(|err| format!("Create directory error: {err}"))?;
    }
    std::fs::write(&temp, body).map_err(|err| format!("Write error: {err}"))?;
    commit_file(&temp, &output.novel_file, overwrite)?;
    if output.delete_images_after {
        cleanup_frames(job, &output.image_folder);
    }
    Ok(())
}

fn commit_bytes(
    bytes: &[u8],
    destination: &str,
    overwrite: bool,
    delete_images: bool,
    job: &AfterJob,
    folder: &str,
) -> Result<(), String> {
    let temp = format!("{destination}.pixevaldownloading");
    if let Some(parent) = Path::new(&temp).parent() {
        std::fs::create_dir_all(parent).map_err(|err| format!("Create directory error: {err}"))?;
    }
    std::fs::write(&temp, bytes).map_err(|err| format!("Write error: {err}"))?;
    commit_file(&temp, destination, overwrite)?;
    if delete_images {
        cleanup_frames(job, folder);
    }
    Ok(())
}

fn render_novel_html(job: &AfterJob, content: &NovelContent) -> String {
    let (images, illusts, cover) = novel_names(job, content);
    let engine = NovelEngine::new();
    engine.render_export_html_from_text(content.text.clone(), job.novel.title.clone(), cover, images, illusts)
}

fn render_novel_markdown(job: &AfterJob, content: &NovelContent) -> String {
    let (images, illusts, cover) = novel_names(job, content);
    let engine = NovelEngine::new();
    engine.render_export_markdown_from_text(content.text.clone(), cover, images, illusts)
}

fn render_novel_epub(job: &AfterJob, content: &NovelContent) -> Result<Vec<u8>, String> {
    let (images, illusts, cover) = novel_names(job, content);
    let mut bytes = HashMap::new();
    for (path, _) in &job.children {
        if Path::new(path).is_file() {
            if let Ok(data) = std::fs::read(path) {
                bytes.insert(file_name(path), data);
            }
        }
    }
    let engine = NovelEngine::new();
    let metadata = NovelEpubMetadataDto {
        id: job.novel_id,
        title: job.novel.title.clone(),
        author: job.novel.author.clone(),
        description: job.novel.caption.clone(),
        language: job.novel.language.clone(),
        cover_filename: cover,
    };
    engine
        .build_epub_from_text(metadata, content.text.clone(), bytes)
        .map_err(|err| err.to_string())
        .map(|data| {
            let _ = (images, illusts);
            data
        })
}

fn novel_names(
    job: &AfterJob,
    content: &NovelContent,
) -> (Vec<NovelImageRenderDto>, Vec<NovelIllustRenderDto>, Option<String>) {
    let mut names = job.children.iter().map(|(path, _)| file_name(path));
    let cover = names.next();
    let mut images = Vec::new();
    for image in &content.images {
        let filename = names.next().unwrap_or_default();
        images.push(NovelImageRenderDto {
            image_id: image.novel_image_id,
            url: filename.clone(),
            filename,
        });
    }
    let mut illusts = Vec::new();
    for illust in &content.illusts {
        let filename = names.next().unwrap_or_default();
        let web = format!("https://www.pixiv.net/artworks/{}", illust.id);
        illusts.push(NovelIllustRenderDto {
            illust_id: illust.id,
            page: illust.page,
            url: filename.clone(),
            app_uri: format!("pixeval://illust/{}", illust.id),
            web_uri: web,
            filename,
        });
    }
    (images, illusts, cover)
}

fn file_name(path: &str) -> String {
    Path::new(path)
        .file_name()
        .map(|name| name.to_string_lossy().to_string())
        .unwrap_or_else(|| path.to_string())
}

fn delete_final_output(work: &WorkItem) {
    match &work.blueprint.after {
        AfterDownload::WriteNovel(output) => remove_path(&output.novel_file),
        AfterDownload::PackManga { archive_path, .. } => remove_path(archive_path),
        AfterDownload::SynthesizeUgoira { destination, .. } | AfterDownload::EncodeUgoira { destination, .. } => {
            remove_path(destination)
        }
        AfterDownload::WriteDelayCsv { path } => remove_path(path),
        AfterDownload::None | AfterDownload::EncodeEach { .. } => {}
    }
}

fn delete_local_work(work: &WorkItem) {
    for child in &work.children {
        remove_path(&child.destination);
    }
    delete_final_output(work);
    if let AfterDownload::WriteNovel(output) = &work.blueprint.after {
        remove_path(&output.image_folder);
        let _ = std::fs::remove_dir(&output.image_folder);
    }
    if let AfterDownload::SynthesizeUgoira { folder, .. } | AfterDownload::EncodeUgoira { folder, .. } = &work.blueprint.after {
        remove_path(folder);
    }
    if Path::new(&work.blueprint.open_destination).is_file() {
        remove_path(&work.blueprint.open_destination);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::plan::DownloadPolicy;

    fn request(path: &str, title: &str, artwork_id: &str, subscription_id: i64) -> ExternalImageRequest {
        ExternalImageRequest {
            artwork_id: artwork_id.to_string(),
            title: title.to_string(),
            author: "Author".to_string(),
            thumbnail_url: String::new(),
            website_uri: String::new(),
            app_uri: String::new(),
            original_url: "http://127.0.0.1:1/missing.jpg".to_string(),
            path_macro: path.to_string(),
            base_dir: String::new(),
            payload_json: format!(r#"{{"id":"{artwork_id}","title":"{title}"}}"#),
            serialize_key: "test.image".to_string(),
            work_subscription_id: subscription_id,
            subscription_type: if subscription_id == 0 { String::new() } else { "Bookmarks".to_string() },
            is_r18: false,
            is_r18g: false,
            create_date: "2020-01-01T00:00:00+00:00".to_string(),
        }
    }

    fn macro_file(dir: &std::path::Path, stem: &str) -> String {
        format!(
            "{}/{stem}.@{{ext}}",
            dir.to_string_lossy().replace('\\', "/")
        )
    }

    fn wait_completed(manager: &DownloadManager, count: usize) -> DownloadPageSnapshot {
        let start = std::time::Instant::now();
        loop {
            let snapshot = manager.current_page_snapshot();
            let done = snapshot.ordinary_items.iter().filter(|item| item.state == DownloadState::Completed).count()
                + snapshot
                    .folders
                    .iter()
                    .flat_map(|folder| folder.items.iter())
                    .filter(|item| item.state == DownloadState::Completed)
                    .count();
            if done >= count || start.elapsed() > std::time::Duration::from_secs(5) {
                return snapshot;
            }
            std::thread::sleep(std::time::Duration::from_millis(20));
        }
    }

    #[test]
    fn existing_file_is_skipped_without_network() {
        let manager = DownloadManager::new(2, None, None);
        manager.set_download_policy(DownloadPolicy {
            overwrite: false,
            ..DownloadPolicy::default()
        });
        let dir = tempfile::tempdir().unwrap();
        let macro_path = macro_file(dir.path(), "pic");
        let planned = plan_external(&request(&macro_path, "One", "9", 0), &DownloadPolicy::default()).unwrap();
        std::fs::write(&planned.files[0].destination, b"already").unwrap();
        manager.enqueue_external_image(request(&macro_path, "One", "9", 0));
        let snapshot = wait_completed(&manager, 1);
        assert_eq!(snapshot.ordinary_items.len(), 1);
        assert_eq!(snapshot.ordinary_items[0].state, DownloadState::Completed);
        assert_eq!(std::fs::read(&planned.files[0].destination).unwrap(), b"already");
    }

    #[test]
    fn same_key_replaces_and_moves_to_front() {
        let manager = DownloadManager::new(1, None, None);
        manager.set_download_policy(DownloadPolicy { overwrite: false, ..DownloadPolicy::default() });
        let dir = tempfile::tempdir().unwrap();
        let first = macro_file(dir.path(), "a");
        let second = macro_file(dir.path(), "b");
        for (path, title, id) in [(&first, "First", "1"), (&second, "Second", "2")] {
            let planned = plan_external(&request(path, title, id, 0), &DownloadPolicy::default()).unwrap();
            std::fs::write(&planned.files[0].destination, b"x").unwrap();
            manager.enqueue_external_image(request(path, title, id, 0));
        }
        manager.enqueue_external_image(request(&first, "Replaced", "1", 0));
        let snapshot = wait_completed(&manager, 2);
        assert_eq!(snapshot.ordinary_items.len(), 2);
        assert_eq!(snapshot.ordinary_items[0].title, "Replaced");
        assert_eq!(snapshot.ordinary_items[0].artwork_id, "1");
    }

    #[test]
    fn subscription_and_ordinary_keys_stay_separate() {
        let manager = DownloadManager::new(2, None, None);
        manager.set_download_policy(DownloadPolicy { overwrite: false, ..DownloadPolicy::default() });
        manager.set_subscriptions(vec![DownloadFolderMeta {
            subscription_id: 5,
            display_name: "Folder".to_string(),
            avatar_url: String::new(),
            subscription_type: 0,
            work_kind: 0,
        }]);
        let dir = tempfile::tempdir().unwrap();
        let path = macro_file(dir.path(), "shared");
        let planned = plan_external(&request(&path, "Ordinary", "3", 0), &DownloadPolicy::default()).unwrap();
        std::fs::write(&planned.files[0].destination, b"x").unwrap();
        manager.enqueue_external_image(request(&path, "Ordinary", "3", 0));
        manager.enqueue_external_image(request(&path, "Subscribed", "3", 5));
        let snapshot = wait_completed(&manager, 2);
        assert_eq!(snapshot.ordinary_items.len(), 1);
        assert_eq!(snapshot.folders.len(), 1);
        assert_eq!(snapshot.folders[0].items.len(), 1);
        assert_eq!(snapshot.folders[0].items[0].title, "Subscribed");
        assert_eq!(snapshot.folders[0].current_state, DownloadState::Completed);
        assert_eq!(snapshot.folders[0].completed_count, 1);
    }

    #[test]
    fn history_restore_does_not_restart_completed_work() {
        let dir = tempfile::tempdir().unwrap();
        let storage = Arc::new(StorageEngine::new(":memory:".to_string()).unwrap());
        let manager = DownloadManager::new(1, None, None);
        manager.bind_storage(storage.clone());
        manager.set_download_policy(DownloadPolicy { overwrite: false, ..DownloadPolicy::default() });
        let path = macro_file(dir.path(), "kept");
        let planned = plan_external(&request(&path, "Kept", "8", 0), &DownloadPolicy::default()).unwrap();
        std::fs::write(&planned.files[0].destination, b"kept").unwrap();
        let destination = manager.enqueue_external_image(request(&path, "Kept", "8", 0));
        let snapshot = wait_completed(&manager, 1);
        assert_eq!(snapshot.ordinary_items[0].state, DownloadState::Completed);
        let row = storage.get_download_history_by_destination(destination.clone()).unwrap().unwrap();
        assert_eq!(row.state, state_code(DownloadState::Completed));

        let restored = DownloadManager::new(1, None, None);
        restored.bind_storage(storage);
        restored.set_download_policy(DownloadPolicy { overwrite: false, ..DownloadPolicy::default() });
        restored.restore_histories();
        std::thread::sleep(std::time::Duration::from_millis(300));
        let snapshot = restored.current_page_snapshot();
        assert_eq!(snapshot.ordinary_items.len(), 1);
        assert_eq!(snapshot.ordinary_items[0].state, DownloadState::Completed);
        assert_eq!(snapshot.ordinary_items[0].title, "Kept");
        assert_eq!(std::fs::read(&planned.files[0].destination).unwrap(), b"kept");
    }
}
