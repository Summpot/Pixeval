pub mod api;
mod frb_generated;
pub mod state;

#[cfg(test)]
mod tests {
    use super::api::auth::*;
    use super::api::deeplink::*;
    use super::api::download::*;
    use super::api::feed::*;
    use super::api::image::*;
    use super::api::network::*;
    use super::api::novel::*;
    use super::api::search::*;
    use super::api::simple::*;
    use super::api::storage::*;

    #[test]
    fn test_ping_and_version() {
        assert_eq!(version(), "0.1.0");
        assert_eq!(
            ping("Flutter".to_string()),
            "pong from pixeval_flutter_bridge: Flutter"
        );
    }

    #[tokio::test]
    async fn test_async_ping() {
        assert_eq!(
            async_ping("Tokio".to_string()).await,
            "async pong from pixeval_flutter_bridge: Tokio"
        );
    }

    #[test]
    fn test_network_config_read_and_update() {
        let initial = get_network_config();
        assert!(initial.domain_fronting_enabled);

        let mut updated = initial.clone();
        updated.split_delay_ms = 150;
        assert!(update_network_config(updated).is_ok());
    }

    #[test]
    fn test_auth_initial_state() {
        let token = get_refresh_token();
        assert!(token.is_none());

        let user = get_current_user();
        assert!(user.is_none());
    }

    #[test]
    fn test_feed_structures() {
        let dummy = IllustSummary {
            id: 123456,
            title: "Test Illust".to_string(),
            caption: "Description".to_string(),
            user_id: 789,
            user_name: "Artist".to_string(),
            user_account: "artist_acc".to_string(),
            user_avatar_url: "https://example.com/avatar.jpg".to_string(),
            preview_url: "https://example.com/preview.jpg".to_string(),
            original_url: "https://example.com/orig.jpg".to_string(),
            width: 1200,
            height: 1800,
            page_count: 1,
            is_bookmarked: false,
            total_bookmarks: 100,
            total_views: 2000,
            tags: vec!["pixiv".to_string(), "original".to_string()],
            is_r18: false,
            is_ugoira: false,
            thumbnail_url: None,
            filter_metadata: None,
            page_urls: vec!["https://example.com/orig.jpg".to_string()],
            original_page_urls: vec!["https://example.com/orig.jpg".to_string()],
        };
        assert_eq!(dummy.id, 123456);
        assert!(!dummy.is_r18);
        assert_eq!(dummy.page_urls.len(), 1);
    }

    #[test]
    fn test_image_cache_api() {
        let tmp = std::env::temp_dir()
            .join("pixeval_test_cache_unit")
            .to_string_lossy()
            .to_string();
        let _ = init_image_cache(tmp, Some(64 * 1024));
        let stats = get_cache_stats();
        assert_eq!(stats.entry_count, 0);
        let _ = clear_image_cache();
    }

    #[test]
    fn test_search_and_novel_dtos() {
        let tag = AutocompleteTagDto {
            name: "Frieren".to_string(),
            translated_name: Some("Sousou no Frieren".to_string()),
        };
        assert_eq!(tag.name, "Frieren");

        let novel_content = NovelContentDto {
            id: 999,
            pages: vec!["# Chapter 1\nOnce upon a time...".to_string()],
            total_pages: 1,
            chapters: Vec::new(),
        };
        assert_eq!(novel_content.pages.len(), 1);
        assert_eq!(novel_content.total_pages, 1);
    }

    #[test]
    fn test_deeplink_parsing() {
        assert_eq!(
            parse_pixiv_url("https://www.pixiv.net/artworks/12345678".to_string()),
            ParsedPixivLink::Artwork { id: 12345678 }
        );
        assert_eq!(
            parse_pixiv_url("https://pixiv.net/en/artworks/12345678?mode=all".to_string()),
            ParsedPixivLink::Artwork { id: 12345678 }
        );
        assert_eq!(
            parse_pixiv_url("pixiv://illusts/987654".to_string()),
            ParsedPixivLink::Artwork { id: 987654 }
        );
        assert_eq!(
            parse_pixiv_url("https://www.pixiv.net/novel/show.php?id=554433".to_string()),
            ParsedPixivLink::Novel { id: 554433 }
        );
        assert_eq!(
            parse_pixiv_url("pixiv://users/112233".to_string()),
            ParsedPixivLink::User { id: 112233 }
        );
        assert_eq!(
            parse_pixiv_url("https://google.com".to_string()),
            ParsedPixivLink::Unknown {
                raw_url: "https://google.com".to_string()
            }
        );
    }

    #[test]
    fn test_storage_history() {
        let db_path = ":memory:".to_string();
        let _ = init_storage(db_path);
        let _ = add_search_history("Miku".to_string(), Some("Hatsune Miku".to_string()));
        let res = get_search_history(10).unwrap();
        assert_eq!(res.len(), 1);
        assert_eq!(res[0].value, "Miku");
        let _ = delete_search_history_item("Miku".to_string());
        assert_eq!(get_search_history(10).unwrap().len(), 0);
    }

    #[test]
    fn test_download_manager_init() {
        assert!(init_download_manager(4).is_ok());
    }

    #[test]
    fn rotated_mobile_token_survives_restart_and_can_be_cleared() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("session.db").to_string_lossy().into_owned();
        {
            let storage = pixeval_storage::StorageEngine::new(path.clone()).unwrap();
            storage
                .set_mobile_refresh_token(Some("old".into()))
                .unwrap();
            storage
                .set_mobile_refresh_token(Some("rotated".into()))
                .unwrap();
        }
        let storage = pixeval_storage::StorageEngine::new(path.clone()).unwrap();
        assert_eq!(
            storage.get_mobile_refresh_token().unwrap().as_deref(),
            Some("rotated")
        );
        storage.set_mobile_refresh_token(None).unwrap();
        drop(storage);
        assert!(
            pixeval_storage::StorageEngine::new(path)
                .unwrap()
                .get_mobile_refresh_token()
                .unwrap()
                .is_none()
        );
    }
}
