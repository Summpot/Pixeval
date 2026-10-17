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

pub struct MakoFeed<T> {
    fetcher: Arc<dyn PageFetcher<T>>,
    buffer: Mutex<VecDeque<T>>,
    next_url: Mutex<Option<String>>,
    first_fetch_done: AtomicBool,
    is_exhausted: AtomicBool,
    is_cancelled: AtomicBool,
    requested_pages: AtomicU32,
    last_error: parking_lot::RwLock<Option<String>>,
}

impl<T: Send + Sync + 'static> MakoFeed<T> {
    pub fn new(fetcher: Arc<dyn PageFetcher<T>>, initial_url: Option<String>) -> Self {
        Self {
            fetcher,
            buffer: Mutex::new(VecDeque::new()),
            next_url: Mutex::new(initial_url),
            first_fetch_done: AtomicBool::new(false),
            is_exhausted: AtomicBool::new(false),
            is_cancelled: AtomicBool::new(false),
            requested_pages: AtomicU32::new(0),
            last_error: parking_lot::RwLock::new(None),
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

    pub fn last_error(&self) -> Option<String> {
        self.last_error.read().clone()
    }

    pub fn is_completed(&self) -> bool {
        self.is_exhausted.load(Ordering::SeqCst)
    }

    pub async fn next_page(&self) -> Result<(Vec<T>, bool), String> {
        if self.is_cancelled() {
            return Err("cancelled".to_string());
        }

        {
            let mut buf = self.buffer.lock().await;
            if !buf.is_empty() {
                let items: Vec<T> = buf.drain(..).collect();
                let exhausted = self.is_exhausted.load(Ordering::SeqCst);
                return Ok((items, exhausted));
            }
        }

        if self.is_exhausted.load(Ordering::SeqCst) {
            return Ok((Vec::new(), true));
        }

        let current_url = {
            let guard = self.next_url.lock().await;
            guard.clone()
        };

        if self.first_fetch_done.load(Ordering::SeqCst) && current_url.is_none() {
            self.is_exhausted.store(true, Ordering::SeqCst);
            return Ok((Vec::new(), true));
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
            return Err("cancelled".to_string());
        }

        match fetch_res {
            Ok((items, new_next_url)) => {
                *self.last_error.write() = None;
                self.requested_pages.fetch_add(1, Ordering::Relaxed);
                {
                    let mut guard = self.next_url.lock().await;
                    *guard = new_next_url.clone();
                }
                let exhausted = new_next_url.is_none();
                if exhausted {
                    self.is_exhausted.store(true, Ordering::SeqCst);
                }
                Ok((items, exhausted))
            }
            Err(err) => {
                *self.last_error.write() = Some(err.clone());
                Err(err)
            }
        }
    }

    pub async fn next(&self) -> Option<T> {
        loop {
            if self.is_cancelled() {
                return None;
            }

            {
                let mut buf = self.buffer.lock().await;
                if let Some(item) = buf.pop_front() {
                    return Some(item);
                }
            }

            match self.next_page().await {
                Ok((items, exhausted)) => {
                    if items.is_empty() {
                        if exhausted {
                            return None;
                        }
                        continue;
                    }
                    let mut iter = items.into_iter();
                    let first = iter.next();
                    let mut buf = self.buffer.lock().await;
                    for item in iter {
                        buf.push_back(item);
                    }
                    return first;
                }
                Err(_) => return None,
            }
        }
    }
}

use crate::client::MakoError;
use crate::models::{CommentRecord, Illustration, Novel, Series, SpotlightArticle, User, WorkEntry};

fn map_page_error(message: String) -> MakoError {
    if message == "cancelled" {
        MakoError::InvalidParameter { message }
    } else {
        MakoError::Network { message }
    }
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug)]
pub struct IllustrationFeedPage {
    pub items: Vec<Illustration>,
    pub exhausted: bool,
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug)]
pub struct NovelFeedPage {
    pub items: Vec<Novel>,
    pub exhausted: bool,
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug)]
pub struct WorkFeedPage {
    pub items: Vec<WorkEntry>,
    pub exhausted: bool,
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug)]
pub struct UserFeedPage {
    pub items: Vec<User>,
    pub exhausted: bool,
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug)]
pub struct SeriesFeedPage {
    pub items: Vec<Series>,
    pub exhausted: bool,
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug)]
pub struct SpotlightFeedPage {
    pub items: Vec<SpotlightArticle>,
    pub exhausted: bool,
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug)]
pub struct CommentFeedPage {
    pub items: Vec<CommentRecord>,
    pub exhausted: bool,
}

macro_rules! export_feed {
    ($engine:ident, $item:ty, $page:ident) => {
        #[uniffi::export(async_runtime = "tokio")]
        impl $engine {
            pub async fn next(&self) -> Option<$item> {
                self.inner.next().await
            }

            pub async fn next_page(&self) -> Result<$page, MakoError> {
                self.inner
                    .next_page()
                    .await
                    .map(|(items, exhausted)| $page { items, exhausted })
                    .map_err(map_page_error)
            }

            pub fn last_error(&self) -> Option<String> {
                self.inner.last_error()
            }

            pub fn is_completed(&self) -> bool {
                self.inner.is_completed()
            }

            pub fn cancel(&self) {
                self.inner.cancel();
            }

            pub fn requested_pages(&self) -> u32 {
                self.inner.requested_pages()
            }
        }
    };
}

#[derive(uniffi::Object)]
pub struct IllustrationFeed {
    pub(crate) inner: Arc<MakoFeed<Illustration>>,
}

impl IllustrationFeed {
    pub fn new(inner: Arc<MakoFeed<Illustration>>) -> Self {
        Self { inner }
    }
}

export_feed!(IllustrationFeed, Illustration, IllustrationFeedPage);

#[derive(uniffi::Object)]
pub struct NovelFeed {
    pub(crate) inner: Arc<MakoFeed<Novel>>,
}

impl NovelFeed {
    pub fn new(inner: Arc<MakoFeed<Novel>>) -> Self {
        Self { inner }
    }
}

export_feed!(NovelFeed, Novel, NovelFeedPage);

#[derive(uniffi::Object)]
pub struct WorkFeed {
    pub(crate) inner: Arc<MakoFeed<WorkEntry>>,
}

impl WorkFeed {
    pub fn new(inner: Arc<MakoFeed<WorkEntry>>) -> Self {
        Self { inner }
    }
}

export_feed!(WorkFeed, WorkEntry, WorkFeedPage);

#[derive(uniffi::Object)]
pub struct UserFeed {
    pub(crate) inner: Arc<MakoFeed<User>>,
}

impl UserFeed {
    pub fn new(inner: Arc<MakoFeed<User>>) -> Self {
        Self { inner }
    }
}

export_feed!(UserFeed, User, UserFeedPage);

#[derive(uniffi::Object)]
pub struct SeriesFeed {
    pub(crate) inner: Arc<MakoFeed<Series>>,
}

impl SeriesFeed {
    pub fn new(inner: Arc<MakoFeed<Series>>) -> Self {
        Self { inner }
    }
}

export_feed!(SeriesFeed, Series, SeriesFeedPage);

#[derive(uniffi::Object)]
pub struct SpotlightFeed {
    pub(crate) inner: Arc<MakoFeed<SpotlightArticle>>,
}

impl SpotlightFeed {
    pub fn new(inner: Arc<MakoFeed<SpotlightArticle>>) -> Self {
        Self { inner }
    }
}

export_feed!(SpotlightFeed, SpotlightArticle, SpotlightFeedPage);

#[derive(uniffi::Object)]
pub struct CommentFeed {
    pub(crate) inner: Arc<MakoFeed<CommentRecord>>,
}

impl CommentFeed {
    pub fn new(inner: Arc<MakoFeed<CommentRecord>>) -> Self {
        Self { inner }
    }
}

export_feed!(CommentFeed, CommentRecord, CommentFeedPage);

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::VecDeque;
    use std::sync::Mutex;

    struct ScriptedFetcher {
        pages: Mutex<VecDeque<PageFetchResult<i32>>>,
    }

    impl ScriptedFetcher {
        fn new(pages: Vec<PageFetchResult<i32>>) -> Arc<Self> {
            Arc::new(Self {
                pages: Mutex::new(VecDeque::from(pages)),
            })
        }
    }

    impl PageFetcher<i32> for ScriptedFetcher {
        fn fetch_page<'a>(
            &'a self,
            _next_url: Option<&'a str>,
        ) -> Pin<Box<dyn Future<Output = PageFetchResult<i32>> + Send + 'a>> {
            Box::pin(async move {
                self.pages
                    .lock()
                    .unwrap()
                    .pop_front()
                    .unwrap_or(Ok((Vec::new(), None)))
            })
        }
    }

    fn engine(pages: Vec<PageFetchResult<i32>>) -> MakoFeed<i32> {
        MakoFeed::new(ScriptedFetcher::new(pages), Some("https://example.test/first".into()))
    }

    #[tokio::test]
    async fn next_page_empty_page_is_not_exhausted_when_next_url_remains() {
        let engine = engine(vec![
            Ok((Vec::new(), Some("https://example.test/second".into()))),
            Ok((vec![1, 2], None)),
        ]);

        let (items, exhausted) = engine.next_page().await.unwrap();
        assert!(items.is_empty());
        assert!(!exhausted);
        assert!(!engine.is_completed());
        assert_eq!(engine.requested_pages(), 1);

        let (items, exhausted) = engine.next_page().await.unwrap();
        assert_eq!(items, vec![1, 2]);
        assert!(exhausted);
        assert!(engine.is_completed());
    }

    struct FailingFetcher;

    impl PageFetcher<i32> for FailingFetcher {
        fn fetch_page<'a>(
            &'a self,
            _next_url: Option<&'a str>,
        ) -> Pin<Box<dyn Future<Output = PageFetchResult<i32>> + Send + 'a>> {
            Box::pin(async { Err("boom".into()) })
        }
    }

    #[tokio::test]
    async fn next_page_error_is_not_exhausted() {
        let engine = MakoFeed::new(
            Arc::new(FailingFetcher),
            Some("https://example.test/first".into()),
        );

        let err = engine.next_page().await.unwrap_err();
        assert_eq!(err, "boom");
        assert!(!engine.is_completed());
        assert_eq!(engine.last_error().as_deref(), Some("boom"));
        assert!(engine.next().await.is_none());
        assert!(!engine.is_completed());
    }

    #[tokio::test]
    async fn next_page_cancel_is_not_exhausted() {
        let engine = engine(vec![Ok((vec![1], None))]);
        engine.cancel();

        let err = engine.next_page().await.unwrap_err();
        assert_eq!(err, "cancelled");
        assert!(!engine.is_completed());
        assert!(engine.last_error().is_none());
        assert!(engine.next().await.is_none());
    }

    #[tokio::test]
    async fn next_still_yields_items_from_the_same_buffer() {
        let engine = engine(vec![Ok((vec![10, 20, 30], None))]);
        assert_eq!(engine.next().await, Some(10));
        assert_eq!(engine.next().await, Some(20));
        assert_eq!(engine.next().await, Some(30));
        assert_eq!(engine.next().await, None);
        assert!(engine.is_completed());
    }
}
