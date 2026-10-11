// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;

use pixeval_mako::NovelContent;
use tokio_util::sync::CancellationToken;

use crate::engine::key::DownloadTaskKey;
use crate::engine::state::DownloadState;

use super::kinds::WorkBlueprint;
use super::snapshot::{
    DownloadFolderMeta, DownloadFolderSnapshot, DownloadItemSnapshot, DownloadPageSnapshot, FolderFetchState,
    aggregate_items,
};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Control {
    Run,
    Pause,
    Cancel,
}

#[derive(Clone, Debug)]
pub struct ChildFile {
    pub url: String,
    pub destination: String,
    pub state: DownloadState,
    pub progress: f64,
    pub error: Option<String>,
    pub skipped: bool,
}

#[derive(Clone, Debug, Default)]
pub struct NovelAssets {
    pub text: String,
    pub title: String,
    pub author: String,
    pub language: String,
    pub caption: String,
    pub content: Option<NovelContent>,
}

#[derive(Clone, Debug)]
pub struct WorkItem {
    pub key: DownloadTaskKey,
    pub blueprint: WorkBlueprint,
    pub state: DownloadState,
    pub error: Option<String>,
    pub initialized: bool,
    pub pending: bool,
    pub is_processing: bool,
    pub control: Control,
    pub children: Vec<ChildFile>,
    pub subscription_id: i64,
    pub generation: u64,
    pub token: CancellationToken,
    pub novel: NovelAssets,
}

#[derive(Clone, Debug, Default)]
pub struct WorkBook {
    pub order: Vec<DownloadTaskKey>,
    pub works: HashMap<DownloadTaskKey, WorkItem>,
    pub folders: Vec<DownloadFolderMeta>,
    pub fetches: HashMap<i64, FolderFetchState>,
}

impl WorkItem {
    pub fn fresh(key: DownloadTaskKey, blueprint: WorkBlueprint, subscription_id: i64) -> Self {
        let children = children_from_blueprint(&blueprint);
        Self {
            key,
            blueprint,
            state: DownloadState::Queued,
            error: None,
            initialized: false,
            pending: false,
            is_processing: false,
            control: Control::Run,
            children,
            subscription_id,
            generation: 1,
            token: CancellationToken::new(),
            novel: NovelAssets::default(),
        }
    }

    pub fn presentation(&self) -> (DownloadState, f64, u32, u32, u32, bool) {
        if !self.initialized {
            let progress = if self.state == DownloadState::Queued { 0.0 } else { 100.0 };
            let error_count = if self.error.is_some() { 1 } else { 0 };
            return (self.state, progress, 0, 0, error_count, self.is_processing);
        }

        if self.children.is_empty() {
            let progress = match self.state {
                DownloadState::Queued => 0.0,
                DownloadState::Running => self.child_average(),
                _ => 100.0,
            };
            let error_count = if self.error.is_some() || self.state == DownloadState::Error {
                1
            } else {
                0
            };
            let state = if self.pending && self.state != DownloadState::Error {
                DownloadState::Pending
            } else {
                self.state
            };
            return (state, progress, 0, 0, error_count, self.is_processing || self.pending);
        }

        let mut active = 0u32;
        let mut completed = 0u32;
        let mut errors = 0u32;
        let mut is_error = self.error.is_some();
        let mut is_running = false;
        let mut is_paused = false;
        let mut is_cancelled = false;
        let mut is_queued = false;
        let mut is_pending = false;
        for child in &self.children {
            match child.state {
                DownloadState::Queued | DownloadState::Running | DownloadState::Pending | DownloadState::Paused
                | DownloadState::Cancelled => active += 1,
                DownloadState::Completed => completed += 1,
                DownloadState::Error => errors += 1,
            }
            match child.state {
                DownloadState::Error => is_error = true,
                DownloadState::Running => is_running = true,
                DownloadState::Paused => is_paused = true,
                DownloadState::Cancelled => is_cancelled = true,
                DownloadState::Queued => is_queued = true,
                DownloadState::Pending => is_pending = true,
                DownloadState::Completed => {}
            }
        }
        let state = if is_error {
            DownloadState::Error
        } else if is_cancelled {
            DownloadState::Cancelled
        } else if is_paused {
            DownloadState::Paused
        } else if is_running {
            DownloadState::Running
        } else if is_queued {
            DownloadState::Queued
        } else if is_pending || self.pending {
            DownloadState::Pending
        } else {
            DownloadState::Completed
        };
        let error_count = if self.error.is_some() { 1 } else { errors };
        (
            state,
            self.child_average(),
            active,
            completed,
            error_count,
            self.is_processing || self.pending,
        )
    }

    fn child_average(&self) -> f64 {
        if self.children.is_empty() {
            return if self.state == DownloadState::Queued { 0.0 } else { 100.0 };
        }
        let sum: f64 = self.children.iter().map(|child| child.progress).sum();
        sum / self.children.len() as f64
    }
}

pub fn children_from_blueprint(blueprint: &WorkBlueprint) -> Vec<ChildFile> {
    blueprint
        .files
        .iter()
        .map(|file| ChildFile {
            url: file.url.clone(),
            destination: file.destination.clone(),
            state: DownloadState::Queued,
            progress: 0.0,
            error: None,
            skipped: false,
        })
        .collect()
}

impl WorkBook {
    pub fn insert_front(&mut self, key: DownloadTaskKey) {
        self.order.retain(|existing| existing != &key);
        self.order.insert(0, key);
    }

    pub fn insert_back(&mut self, key: DownloadTaskKey) {
        if !self.order.iter().any(|existing| existing == &key) {
            self.order.push(key);
        }
    }

    pub fn snapshot(&self) -> DownloadPageSnapshot {
        let mut ordinary = Vec::new();
        let mut by_subscription: HashMap<i64, Vec<DownloadItemSnapshot>> = HashMap::new();
        for key in &self.order {
            let Some(work) = self.works.get(key) else {
                continue;
            };
            let item = item_snapshot(work);
            if work.subscription_id == 0 {
                ordinary.push(item);
            } else {
                by_subscription.entry(work.subscription_id).or_default().push(item);
            }
        }

        let folders = self
            .folders
            .iter()
            .map(|meta| {
                let items = by_subscription.remove(&meta.subscription_id).unwrap_or_default();
                folder_snapshot(meta, items, self.fetches.get(&meta.subscription_id))
            })
            .collect();

        DownloadPageSnapshot {
            ordinary_items: ordinary,
            folders,
        }
    }
}

fn item_snapshot(work: &WorkItem) -> DownloadItemSnapshot {
    let (state, progress, active, completed, errors, processing) = work.presentation();
    DownloadItemSnapshot {
        key: work.key.clone(),
        title: work.blueprint.title.clone(),
        author: work.blueprint.author.clone(),
        thumbnail_url: work.blueprint.thumbnail_url.clone(),
        website_uri: work.blueprint.website_uri.clone(),
        app_uri: work.blueprint.app_uri.clone(),
        state,
        progress_percentage: progress,
        active_count: active,
        completed_count: completed,
        error_count: errors,
        error_message: work.error.clone().or_else(|| {
            work.children.iter().find_map(|child| child.error.clone())
        }),
        open_destination: work.blueprint.open_destination.clone(),
        is_processing: processing,
        artwork_id: work.blueprint.artwork_id.clone(),
        work_subscription_id: work.subscription_id,
    }
}

fn folder_snapshot(
    meta: &DownloadFolderMeta,
    items: Vec<DownloadItemSnapshot>,
    fetch: Option<&FolderFetchState>,
) -> DownloadFolderSnapshot {
    let (state, progress, active, completed, errors) = aggregate_items(&items);
    let fetch = fetch.cloned().unwrap_or_default();
    DownloadFolderSnapshot {
        subscription_id: meta.subscription_id,
        display_name: meta.display_name.clone(),
        avatar_url: meta.avatar_url.clone(),
        subscription_type: meta.subscription_type,
        work_kind: meta.work_kind,
        total_count: items.len() as u32,
        active_count: active,
        completed_count: completed,
        error_count: errors,
        progress_percentage: progress,
        current_state: state,
        items,
        is_fetching: fetch.is_fetching,
        fetched_count: fetch.fetched_count,
        retry_at_timestamp: fetch.retry_at_timestamp,
    }
}
