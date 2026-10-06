uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod auth;
pub mod client;
pub mod models;
pub mod stream;
pub mod throttle;

pub use auth::*;
pub use client::*;
pub use models::*;
pub use stream::*;
pub use throttle::*;

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::Arc;
    use std::sync::atomic::{AtomicUsize, Ordering};

    #[test]
    fn test_token_response_serde() {
        let json = r#"{
            "access_token": "mock_access_token_12345",
            "expires_in": 3600,
            "token_type": "bearer",
            "refresh_token": "mock_refresh_token_67890",
            "user": {
                "id": "123456",
                "name": "PixivUser",
                "account": "pixiv_user_acc",
                "mail_address": "user@pixiv.net",
                "is_premium": true
            }
        }"#;

        let resp: TokenResponse = serde_json::from_str(json).unwrap();
        assert_eq!(resp.access_token, "mock_access_token_12345");
        assert_eq!(resp.expires_in, 3600);
        let user = resp.user.unwrap();
        assert_eq!(user.name, "PixivUser");
        assert!(user.is_premium);
    }

    #[test]
    fn test_spotlight_article_serde() {
        let json = r#"{
            "spotlight_articles": [
                {
                    "id": 10526,
                    "title": "Sample Spotlight",
                    "pure_title": "Sample Pure Title",
                    "thumbnail": "https://i.pximg.net/c/240x480/sample.jpg",
                    "article_url": "https://www.pixivision.net/zh/a/10526",
                    "publish_date": "2024-03-20T17:00:00+09:00",
                    "category": "spotlight",
                    "subcategory_label": "Illustration"
                }
            ],
            "next_url": "https://app-api.pixiv.net/v1/spotlight/articles?category=all&offset=10"
        }"#;

        let resp: SpotlightResponse = serde_json::from_str(json).unwrap();
        assert_eq!(resp.spotlight_articles.len(), 1);
        let article = &resp.spotlight_articles[0];
        assert_eq!(article.id, 10526);
        assert_eq!(article.title, "Sample Spotlight");
        assert_eq!(article.pure_title.as_deref(), Some("Sample Pure Title"));
        assert_eq!(article.category, "spotlight");
        assert_eq!(resp.next_url.as_deref(), Some("https://app-api.pixiv.net/v1/spotlight/articles?category=all&offset=10"));
    }

    #[test]
    fn test_illustration_serde() {
        let json = r#"{
            "id": 98765432,
            "title": "Sample Art",
            "type": "illust",
            "image_urls": {
                "square_medium": "https://i.pximg.net/sq.jpg",
                "medium": "https://i.pximg.net/med.jpg",
                "large": "https://i.pximg.net/large.jpg",
                "original": "https://i.pximg.net/orig.jpg"
            },
            "user": {
                "id": 111,
                "name": "Artist",
                "account": "artist_acc"
            },
            "tags": [
                { "name": "original" },
                { "name": "cat", "translated_name": "猫" }
            ],
            "create_date": "2026-10-05T12:00:00+09:00",
            "page_count": 1,
            "width": 1920,
            "height": 1080,
            "total_bookmarks": 450,
            "total_view": 2300,
            "is_bookmarked": false
        }"#;

        let illust: Illustration = serde_json::from_str(json).unwrap();
        assert_eq!(illust.id, 98765432);
        assert_eq!(illust.title, "Sample Art");
        assert_eq!(illust.tags.len(), 2);
        assert_eq!(illust.tags[1].translated_name.as_deref(), Some("猫"));
        assert_eq!(illust.width, 1920);
        assert_eq!(illust.height, 1080);
    }

    #[test]
    fn test_user_response_serde() {
        let json = r#"{
            "user_previews": [
                {
                    "user": {
                        "id": 12345,
                        "name": "ArtistOne",
                        "account": "artist_one",
                        "is_followed": true
                    }
                }
            ],
            "next_url": "https://app-api.pixiv.net/v1/user/following?next=2"
        }"#;

        let resp: UserResponse = serde_json::from_str(json).unwrap();
        assert_eq!(resp.user_previews.len(), 1);
        assert_eq!(resp.user_previews[0].user.name, "ArtistOne");
        assert!(resp.user_previews[0].user.is_followed);
        assert_eq!(
            resp.next_url.as_deref(),
            Some("https://app-api.pixiv.net/v1/user/following?next=2")
        );
    }

    struct MockMultiPageFetcher {
        call_count: AtomicUsize,
    }

    impl PageFetcher<u32> for MockMultiPageFetcher {
        fn fetch_page<'a>(
            &'a self,
            _next_url: Option<&'a str>,
        ) -> std::pin::Pin<Box<dyn std::future::Future<Output = PageFetchResult<u32>> + Send + 'a>>
        {
            Box::pin(async move {
                let call = self.call_count.fetch_add(1, Ordering::SeqCst);
                match call {
                    0 => Ok((
                        vec![1, 2, 3],
                        Some("https://api.pixiv.net/page2".to_string()),
                    )),
                    1 => Ok((vec![4, 5], Some("https://api.pixiv.net/page3".to_string()))),
                    2 => Ok((vec![6], None)),
                    _ => Ok((vec![], None)),
                }
            })
        }
    }

    #[tokio::test]
    async fn test_mako_fetch_engine_streaming() {
        let fetcher = Arc::new(MockMultiPageFetcher {
            call_count: AtomicUsize::new(0),
        });
        let engine = MakoFetchEngine::new(fetcher, Some("https://api.pixiv.net/page1".to_string()));

        let mut collected = Vec::new();
        while let Some(item) = engine.next().await {
            collected.push(item);
        }

        assert_eq!(collected, vec![1, 2, 3, 4, 5, 6]);
        assert_eq!(engine.requested_pages(), 3);

        // Subsequent next() returns None
        assert_eq!(engine.next().await, None);
    }

    #[tokio::test]
    async fn test_mako_fetch_engine_cancellation() {
        let fetcher = Arc::new(MockMultiPageFetcher {
            call_count: AtomicUsize::new(0),
        });
        let engine = MakoFetchEngine::new(fetcher, Some("https://api.pixiv.net/page1".to_string()));

        assert_eq!(engine.next().await, Some(1));
        assert_eq!(engine.next().await, Some(2));

        engine.cancel();
        assert!(engine.is_cancelled());
        assert_eq!(engine.next().await, None);
    }

    #[tokio::test]
    async fn test_request_throttler() {
        let throttler = RequestThrottler::new(20);
        let start = std::time::Instant::now();
        throttler.throttle().await;
        throttler.throttle().await;
        let elapsed = start.elapsed();
        assert!(elapsed >= std::time::Duration::from_millis(15));
    }

    #[tokio::test]
    async fn test_mako_client_configuration_and_proxy() {
        let config = MakoConfigurationDto {
            domain_fronting_enabled: false,
            cooldown_ms: 100,
            split_delay_ms: 50,
            host_ips: std::collections::HashMap::new(),
            proxy_url: Some("http://127.0.0.1:7890".to_string()),
            ..Default::default()
        };
        let client = MakoClient::new(config).expect("client creation should succeed");

        let updated_config = MakoConfigurationDto {
            domain_fronting_enabled: false,
            cooldown_ms: 200,
            split_delay_ms: 50,
            host_ips: std::collections::HashMap::new(),
            proxy_url: None,
            ..Default::default()
        };
        client
            .update_configuration(updated_config)
            .expect("update configuration should succeed");
    }

    #[test]
    fn test_novel_content_serde() {
        let json = r#"{
            "id": "12345678",
            "title": "测试小说",
            "seriesId": "999",
            "seriesTitle": "测试系列",
            "seriesIsWatched": true,
            "userId": "1001",
            "coverUrl": "https://i.pximg.net/cover.jpg",
            "tags": ["东方Project", "博丽灵梦"],
            "caption": "小说简介",
            "cdate": "2026-10-06T12:00:00+09:00",
            "rating": { "like": 10, "bookmark": 20, "view": 30 },
            "text": "正文内容第一页[newpage]第二页[chapter:终章]",
            "illusts": {
                "555": {
                    "visible": true,
                    "id": "555",
                    "page": "1",
                    "illust": {
                        "title": "插图",
                        "description": "",
                        "restrict": 0,
                        "xRestrict": 0,
                        "sl": "0",
                        "tags": [],
                        "images": { "medium": "https://i.pximg.net/illust_med.jpg" }
                    },
                    "user": {
                        "id": "1001",
                        "name": "Artist",
                        "image": "https://i.pximg.net/user.jpg"
                    }
                }
            },
            "images": {
                "777": {
                    "novelImageId": "777",
                    "sl": "1",
                    "urls": {
                        "240mw": "https://i.pximg.net/240.jpg",
                        "480mw": "https://i.pximg.net/480.jpg",
                        "1200x1200": "https://i.pximg.net/1200.jpg",
                        "128x128": "https://i.pximg.net/128.jpg",
                        "original": "https://i.pximg.net/orig.jpg"
                    }
                }
            },
            "seriesNavigation": {
                "nextNovel": {
                    "id": "12345679",
                    "viewable": true,
                    "contentOrder": "2",
                    "title": "下篇",
                    "coverUrl": "https://i.pximg.net/cover2.jpg"
                },
                "prevNovel": {
                    "id": "12345677",
                    "viewable": true,
                    "contentOrder": "1",
                    "title": "上篇",
                    "coverUrl": "https://i.pximg.net/cover0.jpg"
                }
            },
            "aiType": 0,
            "isOriginal": true,
            "language": "zh"
        }"#;

        let novel_content: NovelContent = serde_json::from_str(json).unwrap();
        assert_eq!(novel_content.id, 12345678);
        assert_eq!(novel_content.title, "测试小说");
        assert_eq!(novel_content.series_id, Some(999));
        assert_eq!(novel_content.series_title.as_deref(), Some("测试系列"));
        assert_eq!(novel_content.illusts.len(), 1);
        assert_eq!(novel_content.illusts[0].id, 555);
        assert_eq!(novel_content.images.len(), 1);
        assert_eq!(novel_content.images[0].novel_image_id, 777);

        let nav = novel_content.series_navigation.unwrap();
        let next = nav.next_novel.unwrap();
        assert_eq!(next.id, 12345679);
        assert_eq!(next.title, "下篇");
        let prev = nav.prev_novel.unwrap();
        assert_eq!(prev.id, 12345677);
        assert_eq!(prev.title, "上篇");
    }

    #[test]
    fn test_extract_novel_json_from_html() {
        let html = r#"
            <!DOCTYPE html>
            <html>
            <head><title>Pixiv Novel</title></head>
            <body>
            <script>
            window.__INITIAL_DATA__ = {
                "novel": {
                    "id": 88888,
                    "title": "Embedded Novel",
                    "userId": 123,
                    "coverUrl": "https://i.pximg.net/cover.jpg",
                    "tags": ["tag1"],
                    "caption": "test",
                    "cdate": "2026-10-06T00:00:00+09:00",
                    "text": "Hello novel [newpage] world!",
                    "illusts": [],
                    "images": [],
                    "aiType": 0,
                    "isOriginal": false,
                    "language": "ja"
                }
            };
            </script>
            </body>
            </html>
        "#;

        let json_str = extract_novel_json_from_html(html).expect("should extract json");
        let novel: NovelContent = serde_json::from_str(&json_str).expect("should parse json");
        assert_eq!(novel.id, 88888);
        assert_eq!(novel.title, "Embedded Novel");
        assert_eq!(novel.text, "Hello novel [newpage] world!");
    }

    #[test]
    fn test_search_options_serde() {
        let json = r#"{
            "illust": {
                "bookmark_ranges": [
                    { "bookmark_num_min": "*", "bookmark_num_max": "100" }
                ],
                "show_ai_condition": true,
                "lang": { "options": [{ "code": "zh", "name": "中文" }] },
                "tool": { "options": ["Photoshop", "SAI"] }
            },
            "novel": {
                "bookmark_ranges": [],
                "show_ai_condition": true,
                "lang": { "options": [] },
                "genre": { "options": [{ "id": 1, "label": "Romance" }] },
                "word_count_supported_languages": "ja,en"
            }
        }"#;

        let raw: SearchOptionsRaw = serde_json::from_str(json).unwrap();
        assert_eq!(raw.illust.tool.options.len(), 2);
        assert_eq!(raw.novel.genre.options.len(), 1);
        assert_eq!(raw.novel.genre.options[0].label, "Romance");
    }
}
