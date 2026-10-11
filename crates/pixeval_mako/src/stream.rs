// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::VecDeque;
use std::future::Future;
use std::pin::Pin;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicU32, Ordering};
use tokio::sync::Mutex;

pub type PageFetchResult<T> = Result<(Vec<T>, Option<String>), String>;

pub trait PageFetcher<T>: Send + Sync {
    fn fetch_page<'a>(
        &'a self,
        next_url: Option<&'a str>,
    ) -> Pin<Box<dyn Future<Output = PageFetchResult<T>> + Send + 'a>>;
}

pub struct MakoFetchEngine<T> {
    fetcher: Arc<dyn PageFetcher<T>>,
    buffer: Mutex<VecDeque<T>>,
    next_url: Mutex<Option<String>>,
    first_fetch_done: AtomicBool,
    is_exhausted: AtomicBool,
    is_cancelled: AtomicBool,
    requested_pages: AtomicU32,
}

impl<T: Send + Sync + 'static> MakoFetchEngine<T> {
    pub fn new(fetcher: Arc<dyn PageFetcher<T>>, initial_url: Option<String>) -> Self {
        Self {
            fetcher,
            buffer: Mutex::new(VecDeque::new()),
            next_url: Mutex::new(initial_url),
            first_fetch_done: AtomicBool::new(false),
            is_exhausted: AtomicBool::new(false),
            is_cancelled: AtomicBool::new(false),
            requested_pages: AtomicU32::new(0),
        }
    }

    pub fn cancel(&self) {
        self.is_cancelled.store(true, Ordering::SeqCst);
    }

    pub fn is_cancelled(&self) -> bool {
        self.is_cancelled.load(Ordering::SeqCst)
    }

    pub fn requested_pages(&self) -> u32 {
        self.requested_pages.load(Ordering::Relaxed)
    }

    pub async fn next(&self) -> Option<T> {
        loop {
            if self.is_cancelled() {
                return None;
            }

            // Fast path: check current in-memory buffer
            {
                let mut buf = self.buffer.lock().await;
                if let Some(item) = buf.pop_front() {
                    return Some(item);
                }
            }

            // Buffer is empty, check if stream is exhausted
            if self.is_exhausted.load(Ordering::SeqCst) {
                return None;
            }

            // Fetch next page
            let current_url = {
                let guard = self.next_url.lock().await;
                guard.clone()
            };

            if self.first_fetch_done.load(Ordering::SeqCst) && current_url.is_none() {
                self.is_exhausted.store(true, Ordering::SeqCst);
                return None;
            }

            self.first_fetch_done.store(true, Ordering::SeqCst);
            let mut attempts = 0;
            let fetch_res = loop {
                attempts += 1;
                let res = self.fetcher.fetch_page(current_url.as_deref()).await;
                if res.is_ok() || attempts >= 3 || self.is_cancelled() {
                    break res;
                }
                tokio::time::sleep(std::time::Duration::from_millis(500 * (1 << (attempts - 1)))).await;
            };

            if self.is_cancelled() {
                return None;
            }

            match fetch_res {
                Ok((items, new_next_url)) => {
                    self.requested_pages.fetch_add(1, Ordering::Relaxed);
                    {
                        let mut guard = self.next_url.lock().await;
                        *guard = new_next_url.clone();
                    }

                    if new_next_url.is_none() {
                        self.is_exhausted.store(true, Ordering::SeqCst);
                    }

                    let mut buf = self.buffer.lock().await;
                    for item in items {
                        buf.push_back(item);
                    }

                    if let Some(item) = buf.pop_front() {
                        return Some(item);
                    }

                    if new_next_url.is_none() {
                        return None;
                    }
                    // If intermediate page had 0 items but has next_url, loop continues!
                }
                Err(_err) => {
                    // Do not mark exhausted on transient error so callers can retry
                    return None;
                }
            }
        }
    }
}

use crate::models::{CommentRecord, Illustration, Novel, Series, SpotlightArticle, User, WorkEntry};

#[derive(uniffi::Object)]
pub struct IllustrationFetchEngine {
    pub(crate) inner: Arc<MakoFetchEngine<Illustration>>,
}

impl IllustrationFetchEngine {
    pub fn new(inner: Arc<MakoFetchEngine<Illustration>>) -> Self {
        Self { inner }
    }
}

#[uniffi::export(async_runtime = "tokio")]
impl IllustrationFetchEngine {
    pub async fn next(&self) -> Option<Illustration> {
        self.inner.next().await
    }

    pub fn cancel(&self) {
        self.inner.cancel();
    }

    pub fn requested_pages(&self) -> u32 {
        self.inner.requested_pages()
    }
}

#[derive(uniffi::Object)]
pub struct NovelFetchEngine {
    pub(crate) inner: Arc<MakoFetchEngine<Novel>>,
}

impl NovelFetchEngine {
    pub fn new(inner: Arc<MakoFetchEngine<Novel>>) -> Self {
        Self { inner }
    }
}

#[uniffi::export(async_runtime = "tokio")]
impl NovelFetchEngine {
    pub async fn next(&self) -> Option<Novel> {
        self.inner.next().await
    }

    pub fn cancel(&self) {
        self.inner.cancel();
    }

    pub fn requested_pages(&self) -> u32 {
        self.inner.requested_pages()
    }
}

#[derive(uniffi::Object)]
pub struct WorkFetchEngine {
    pub(crate) inner: Arc<MakoFetchEngine<WorkEntry>>,
}

impl WorkFetchEngine {
    pub fn new(inner: Arc<MakoFetchEngine<WorkEntry>>) -> Self {
        Self { inner }
    }
}

#[uniffi::export(async_runtime = "tokio")]
impl WorkFetchEngine {
    pub async fn next(&self) -> Option<WorkEntry> {
        self.inner.next().await
    }

    pub fn cancel(&self) {
        self.inner.cancel();
    }

    pub fn requested_pages(&self) -> u32 {
        self.inner.requested_pages()
    }
}

#[derive(uniffi::Object)]
pub struct UserFetchEngine {
    pub(crate) inner: Arc<MakoFetchEngine<User>>,
}

impl UserFetchEngine {
    pub fn new(inner: Arc<MakoFetchEngine<User>>) -> Self {
        Self { inner }
    }
}

#[uniffi::export(async_runtime = "tokio")]
impl UserFetchEngine {
    pub async fn next(&self) -> Option<User> {
        self.inner.next().await
    }

    pub fn cancel(&self) {
        self.inner.cancel();
    }

    pub fn requested_pages(&self) -> u32 {
        self.inner.requested_pages()
    }
}

#[derive(uniffi::Object)]
pub struct SeriesFetchEngine {
    pub(crate) inner: Arc<MakoFetchEngine<Series>>,
}

impl SeriesFetchEngine {
    pub fn new(inner: Arc<MakoFetchEngine<Series>>) -> Self {
        Self { inner }
    }
}

#[uniffi::export(async_runtime = "tokio")]
impl SeriesFetchEngine {
    pub async fn next(&self) -> Option<Series> {
        self.inner.next().await
    }

    pub fn cancel(&self) {
        self.inner.cancel();
    }

    pub fn requested_pages(&self) -> u32 {
        self.inner.requested_pages()
    }
}

#[derive(uniffi::Object)]
pub struct SpotlightFetchEngine {
    pub(crate) inner: Arc<MakoFetchEngine<SpotlightArticle>>,
}

impl SpotlightFetchEngine {
    pub fn new(inner: Arc<MakoFetchEngine<SpotlightArticle>>) -> Self {
        Self { inner }
    }
}

#[uniffi::export(async_runtime = "tokio")]
impl SpotlightFetchEngine {
    pub async fn next(&self) -> Option<SpotlightArticle> {
        self.inner.next().await
    }

    pub fn cancel(&self) {
        self.inner.cancel();
    }

    pub fn requested_pages(&self) -> u32 {
        self.inner.requested_pages()
    }
}

#[derive(uniffi::Object)]
pub struct CommentFetchEngine {
    pub(crate) inner: Arc<MakoFetchEngine<CommentRecord>>,
}

impl CommentFetchEngine {
    pub fn new(inner: Arc<MakoFetchEngine<CommentRecord>>) -> Self {
        Self { inner }
    }
}

#[uniffi::export(async_runtime = "tokio")]
impl CommentFetchEngine {
    pub async fn next(&self) -> Option<CommentRecord> {
        self.inner.next().await
    }

    pub fn cancel(&self) {
        self.inner.cancel();
    }

    pub fn requested_pages(&self) -> u32 {
        self.inner.requested_pages()
    }
}
