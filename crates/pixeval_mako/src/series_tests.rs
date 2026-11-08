use super::*;
use std::collections::HashMap;
use std::sync::atomic::{AtomicUsize, Ordering};
use pixeval_maho::{MahoError, MahoResponseData, MahoTransport};
use serde_json::json;

struct Transport {
    body: Vec<u8>,
    calls: Arc<AtomicUsize>,
    expected_path: &'static str,
}

#[async_trait::async_trait]
impl MahoTransport for Transport {
    async fn send(&self, _: u64, method: String, url: String,
        _: HashMap<String, String>, _: Vec<u8>) -> Result<MahoResponseData, MahoError> {
        assert_eq!(method, "GET");
        assert!(url.contains(self.expected_path));
        self.calls.fetch_add(1, Ordering::SeqCst);
        Ok(MahoResponseData { status: 200, headers: HashMap::new(), body: self.body.clone() })
    }
    fn cancel(&self, _: u64) {}
}

#[tokio::test]
async fn series_metadata_and_initial_page_come_from_one_response() {
    for kind in [SimpleWorkKind::Illustration, SimpleWorkKind::Novel] {
        let client = MakoClient::new(MakoConfigurationDto::default()).unwrap();
        client.oauth.set_token_response(TokenResponse { access_token: "fixture".into(), expires_in: 3600, ..Default::default() });
        let detail = json!({"id": 42, "title": "Actual series", "caption": "Full caption",
            "watchlist_added": true, "content_count": 17,
            "cover_image_urls": {"medium": "https://fixture.invalid/cover"}});
        let body = if kind.is_novel() {
            let novel = Novel { id: 7, title: "First chapter".into(), ..Default::default() };
            json!({"novel_series_detail": detail, "novel_series_first_novel": novel, "novels": [novel], "next_url": null})
        } else {
            let illust = Illustration { id: 7, title: "First chapter".into(), ..Default::default() };
            json!({"illust_series_detail": detail, "illust_series_first_illust": illust, "illusts": [illust], "next_url": null})
        };
        let calls = Arc::new(AtomicUsize::new(0));
        client.set_transport(Some(Box::new(Transport {
            body: serde_json::to_vec(&body).unwrap(), calls: calls.clone(),
            expected_path: if kind.is_novel() { "/v2/novel/series" } else { "/v1/illust/series" },
        })));
        let query = client.get_work_series(kind, 42).await.unwrap();
        assert_eq!(query.detail().title, "Actual series");
        assert_eq!(query.detail().mask_text.as_deref(), Some("Full caption"));
        assert_eq!(query.detail().published_content_count, Some(17));
        assert!(query.detail().watchlist_added);
        assert_eq!(query.detail().cover_url.as_deref(), Some("https://fixture.invalid/cover"));
        assert!(query.first().is_some());
        let page = query.engine().next_page_once().await.unwrap();
        assert_eq!(page.items.len(), 1);
        assert!(page.exhausted);
        assert!(query.engine().next_page_once().await.unwrap().items.is_empty());
        assert_eq!(calls.load(Ordering::SeqCst), 1, "Opening the initial page must not refetch series details");
    }
}

#[tokio::test]
async fn missing_series_metadata_is_an_error_not_a_fabricated_series() {
    let client = MakoClient::new(MakoConfigurationDto::default()).unwrap();
    client.oauth.set_token_response(TokenResponse { access_token: "fixture".into(), expires_in: 3600, ..Default::default() });
    client.set_transport(Some(Box::new(Transport {
        body: br#"{"illusts":[],"next_url":null}"#.to_vec(),
        calls: Arc::new(AtomicUsize::new(0)), expected_path: "/v1/illust/series",
    })));
    assert!(matches!(client.get_work_series(SimpleWorkKind::Illustration, 42).await, Err(MakoError::Json { .. })));
}

struct BlockingTransport {
    started: Arc<tokio::sync::Notify>,
    release: Arc<tokio::sync::Notify>,
}

#[async_trait::async_trait]
impl MahoTransport for BlockingTransport {
    async fn send(&self, _: u64, _: String, url: String,
        _: HashMap<String, String>, _: Vec<u8>) -> Result<MahoResponseData, MahoError> {
        if url.ends_with("/slow") {
            self.started.notify_one();
            self.release.notified().await;
        }
        Ok(MahoResponseData { status: 200, headers: HashMap::new(), body: b"{}".to_vec() })
    }
    fn cancel(&self, _: u64) {}
}

#[tokio::test]
async fn pending_metadata_does_not_block_an_independent_feed() {
    let client = MakoClient::new(MakoConfigurationDto::default()).unwrap();
    client.oauth.set_token_response(TokenResponse { access_token: "fixture".into(), expires_in: 3600, ..Default::default() });
    client.throttler.set_cooldown_ms(0);
    let started = Arc::new(tokio::sync::Notify::new());
    let release = Arc::new(tokio::sync::Notify::new());
    client.set_transport(Some(Box::new(BlockingTransport { started: started.clone(), release: release.clone() })));
    let first = client.clone();
    let slow = tokio::spawn(async move { first.request_get("https://fixture.invalid/slow").await });
    started.notified().await;
    let result = tokio::time::timeout(std::time::Duration::from_secs(2),
        client.request_get("https://fixture.invalid/feed")).await;
    release.notify_one();
    slow.await.unwrap().unwrap();
    result.expect("A pending response must not own the request-start gate").unwrap();
}

#[tokio::test]
async fn novel_content_cache_reuses_successes_and_isolates_accounts() {
    let client = MakoClient::new(MakoConfigurationDto::default()).unwrap();
    let token = TokenResponse { access_token: "fixture".into(), expires_in: 3600,
        user: Some(TokenUser { id: "first".into(), ..Default::default() }), ..Default::default() };
    client.oauth.set_token_response(token.clone());
    client.throttler.set_cooldown_ms(0);
    let calls = Arc::new(AtomicUsize::new(0));
    client.set_transport(Some(Box::new(Transport {
        body: br#"<script>{"novel":{"id":42,"text":"Cached body"}}</script>"#.to_vec(),
        calls: calls.clone(), expected_path: "/webview/v2/novel",
    })));
    assert_eq!(client.get_novel_content_cached(42).await.unwrap().text, "Cached body");
    assert_eq!(client.get_novel_content_cached(42).await.unwrap().text, "Cached body");
    assert_eq!(calls.load(Ordering::SeqCst), 1);
    client.get_novel_content_structured(42).await.unwrap();
    assert_eq!(calls.load(Ordering::SeqCst), 2, "Explicit exports fetch fresh content");
    client.set_user(TokenUser { id: "second".into(), ..Default::default() });
    client.get_novel_content_cached(42).await.unwrap();
    assert_eq!(calls.load(Ordering::SeqCst), 3, "Another account must fetch its own content");
    client.clear_token();
    client.oauth.set_token_response(token);
    client.get_novel_content_cached(42).await.unwrap();
    assert_eq!(calls.load(Ordering::SeqCst), 4, "Signing out releases cached content");
}
