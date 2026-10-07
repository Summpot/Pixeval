uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod client;
pub mod error;
pub mod models;
pub mod platforms;

pub use client::BooruClient;
pub use error::BooruError;
pub use models::{
    BooruPlatform, BooruPost, BooruPostPreview, BooruSearchResult, BooruTag, normalize_rating,
};

#[uniffi::export]
pub fn booru_ping() -> String {
    "booru_pong".to_string()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_platform_conversion() {
        assert_eq!(BooruPlatform::Danbooru.as_str(), "danbooru");
        assert_eq!(BooruPlatform::Gelbooru.as_str(), "gelbooru");
        assert_eq!(BooruPlatform::Yandere.as_str(), "yandere");
        assert_eq!(BooruPlatform::Sankaku.as_str(), "sankaku");
        assert_eq!(BooruPlatform::Rule34.as_str(), "rule34");

        assert_eq!(
            BooruPlatform::from_str_name("Danbooru"),
            Some(BooruPlatform::Danbooru)
        );
        assert_eq!(
            BooruPlatform::from_str_name("yande.re"),
            Some(BooruPlatform::Yandere)
        );
        assert_eq!(
            BooruPlatform::from_str_name("Rule34.xxx"),
            Some(BooruPlatform::Rule34)
        );
        assert_eq!(BooruPlatform::from_str_name("unknown"), None);
    }

    #[test]
    fn test_rating_normalization() {
        assert_eq!(normalize_rating("s"), "general");
        assert_eq!(normalize_rating("safe"), "general");
        assert_eq!(normalize_rating("general"), "general");
        assert_eq!(normalize_rating("sensitive"), "sensitive");
        assert_eq!(normalize_rating("q"), "questionable");
        assert_eq!(normalize_rating("questionable"), "questionable");
        assert_eq!(normalize_rating("e"), "explicit");
        assert_eq!(normalize_rating("explicit"), "explicit");
    }

    #[test]
    fn test_danbooru_json_deserialization() {
        let json = r#"{
            "id": 123456,
            "created_at": "2024-01-01T00:00:00.000+00:00",
            "uploader_id": 99,
            "uploader": { "id": 99, "name": "test_user" },
            "source": "https://example.com/source.jpg",
            "file_ext": "zip",
            "md5": "d41d8cd98f00b204e9800998ecf8427e",
            "rating": "s",
            "image_width": 1920,
            "image_height": 1080,
            "file_size": 204800,
            "file_url": "https://danbooru.donmai.us/data/original.zip",
            "large_file_url": "https://danbooru.donmai.us/data/sample.jpg",
            "preview_file_url": "https://danbooru.donmai.us/data/preview.jpg",
            "tag_string_artist": "artist_a",
            "tag_string_character": "character_b",
            "tag_string_copyright": "copyright_c",
            "tag_string_general": "tag_one tag_two",
            "tag_string_meta": "highres",
            "score": 42,
            "is_deleted": false,
            "is_banned": false,
            "media_metadata": {
                "metadata": {
                    "Ugoira:FrameDelays": [50, 50, 100]
                }
            }
        }"#;

        let parsed: serde_json::Value = serde_json::from_str(json).unwrap();
        assert_eq!(parsed["id"], 123456);
        assert_eq!(
            parsed["media_metadata"]["metadata"]["Ugoira:FrameDelays"][0],
            50
        );
    }
}
