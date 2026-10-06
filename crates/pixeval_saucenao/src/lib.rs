uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod client;
pub mod error;
pub mod models;

pub use client::SauceNaoClient;
pub use error::SauceNaoError;
pub use models::{SauceNaoItem, SauceNaoSearchResult};

#[uniffi::export]
pub fn saucenao_ping() -> String {
    "saucenao_pong".to_string()
}

#[cfg(test)]
mod tests {
    use crate::models::{RawSauceNaoResponse, map_raw_result};

    #[test]
    fn test_parse_pixiv_response() {
        let json = r#"{
            "header": {
                "status": 0,
                "message": "",
                "short_remaining": 4,
                "long_remaining": 99
            },
            "results": [
                {
                    "header": {
                        "similarity": "95.50",
                        "thumbnail": "https://img1.saucenao.com/thumb/12345.jpg",
                        "index_id": 5,
                        "index_name": "Index #5: Pixiv Images"
                    },
                    "data": {
                        "ext_urls": ["https://www.pixiv.net/artworks/12345678"],
                        "title": "Test Pixiv Art",
                        "pixiv_id": 12345678,
                        "member_name": "Pixiv Artist",
                        "member_id": 87654321
                    }
                }
            ]
        }"#;

        let parsed: RawSauceNaoResponse = serde_json::from_str(json).unwrap();
        assert_eq!(parsed.header.status, 0);
        assert_eq!(parsed.results.len(), 1);

        let item = map_raw_result(parsed.results.into_iter().next().unwrap());
        assert_eq!(item.similarity, 95.50);
        assert_eq!(item.platform, "pixiv");
        assert_eq!(item.artwork_id, Some("12345678".to_string()));
        assert_eq!(item.title, "Test Pixiv Art");
        assert_eq!(item.author_name, "Pixiv Artist");
        assert_eq!(item.author_url, Some("https://www.pixiv.net/users/87654321".to_string()));
        assert_eq!(item.thumbnail_url, "https://img1.saucenao.com/thumb/12345.jpg");
        assert!(!item.is_nsfw);
    }

    #[test]
    fn test_parse_danbooru_response() {
        let json = r#"{
            "header": {
                "status": 0,
                "short_remaining": 3,
                "long_remaining": 98
            },
            "results": [
                {
                    "header": {
                        "similarity": "88.12",
                        "thumbnail": "https://img2.saucenao.com/thumb/99999.jpg",
                        "index_id": 9,
                        "index_name": "Index #9: Danbooru"
                    },
                    "data": {
                        "ext_urls": ["https://danbooru.donmai.us/posts/555666"],
                        "danbooru_id": 555666,
                        "creator": "Danbooru Artist",
                        "characters": "Hatsune Miku",
                        "source": "https://twitter.com/i/web/status/123"
                    }
                }
            ]
        }"#;

        let parsed: RawSauceNaoResponse = serde_json::from_str(json).unwrap();
        let item = map_raw_result(parsed.results.into_iter().next().unwrap());
        assert_eq!(item.similarity, 88.12);
        assert_eq!(item.platform, "danbooru");
        assert_eq!(item.artwork_id, Some("555666".to_string()));
        assert_eq!(item.author_name, "Danbooru Artist");
    }

    #[test]
    fn test_parse_gelbooru_and_yandere_response() {
        let json = r#"{
            "header": {
                "status": 0
            },
            "results": [
                {
                    "header": {
                        "similarity": "90.00",
                        "thumbnail": "https://img.saucenao.com/thumb/1.jpg",
                        "index_id": 25,
                        "index_name": "Index #25: Gelbooru"
                    },
                    "data": {
                        "gelbooru_id": 789012,
                        "creator": ["Artist A", "Artist B"]
                    }
                },
                {
                    "header": {
                        "similarity": "85.20",
                        "thumbnail": "https://img.saucenao.com/thumb/2.jpg",
                        "index_id": 12,
                        "index_name": "Index #12: Yande.re"
                    },
                    "data": {
                        "yandere_id": 345678,
                        "creator": "Yandere Artist"
                    }
                }
            ]
        }"#;

        let parsed: RawSauceNaoResponse = serde_json::from_str(json).unwrap();
        assert_eq!(parsed.results.len(), 2);

        let mut it = parsed.results.into_iter();
        let item1 = map_raw_result(it.next().unwrap());
        assert_eq!(item1.platform, "gelbooru");
        assert_eq!(item1.artwork_id, Some("789012".to_string()));
        assert_eq!(item1.author_name, "Artist A, Artist B");

        let item2 = map_raw_result(it.next().unwrap());
        assert_eq!(item2.platform, "yandere");
        assert_eq!(item2.artwork_id, Some("345678".to_string()));
    }

    #[test]
    fn test_nsfw_index_detection() {
        use crate::models::is_index_nsfw;
        assert!(is_index_nsfw(0));
        assert!(is_index_nsfw(1));
        assert!(is_index_nsfw(2));
        assert!(is_index_nsfw(16));
        assert!(is_index_nsfw(18));
        assert!(is_index_nsfw(22));
        assert!(is_index_nsfw(38));
        assert!(!is_index_nsfw(5)); // Pixiv
        assert!(!is_index_nsfw(9)); // Danbooru
    }
}
