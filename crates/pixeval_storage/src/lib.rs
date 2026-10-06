uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod engine;
pub mod error;
pub mod models;
pub mod repository;
pub mod schema;

pub use engine::*;
pub use error::*;
pub use models::*;
pub use repository::*;

#[cfg(test)]
mod tests {
    use std::sync::Arc;
    use super::*;

    #[test]
    fn test_search_history_crud() {
        let storage = StorageEngine::new(":memory:".to_string()).unwrap();

        assert_eq!(storage.count_search_history().unwrap(), 0);

        let rec = storage
            .insert_search_history("pixiv".to_string(), Some("Pixiv".to_string()), "2026-10-06T00:00:00Z".to_string())
            .unwrap();
        assert_eq!(rec.value, "pixiv");
        assert_eq!(storage.count_search_history().unwrap(), 1);

        let queried = storage.get_search_history_by_value("pixiv".to_string()).unwrap();
        assert!(queried.is_some());
        assert_eq!(queried.unwrap().translated_name, Some("Pixiv".to_string()));

        let upserted = storage
            .upsert_search_history("pixiv".to_string(), Some("Pixiv Updated".to_string()), "2026-10-06T01:00:00Z".to_string())
            .unwrap();
        assert_eq!(upserted.translated_name, Some("Pixiv Updated".to_string()));
        assert_eq!(storage.count_search_history().unwrap(), 1);

        let deleted = storage.try_delete_search_history_by_value("pixiv".to_string()).unwrap();
        assert!(deleted);
        assert_eq!(storage.count_search_history().unwrap(), 0);
    }

    #[test]
    fn test_browse_history_cascade_and_stream() {
        let storage = StorageEngine::new(":memory:".to_string()).unwrap();

        let rec1 = storage
            .add_or_replace_browse_history(
                "100".to_string(),
                Some("Illustration".to_string()),
                "Illustration:100".to_string(),
                "{\"title\":\"test1\"}".to_string(),
            )
            .unwrap();
        assert_eq!(rec1.work_key, "Illustration:100");
        assert_eq!(storage.count_browse_history().unwrap(), 1);

        // Replacing should reuse/cascade payload
        let rec2 = storage
            .add_or_replace_browse_history(
                "100".to_string(),
                Some("Illustration".to_string()),
                "Illustration:100".to_string(),
                "{\"title\":\"test1-updated\"}".to_string(),
            )
            .unwrap();
        assert_eq!(rec2.work_key, "Illustration:100");
        assert_eq!(storage.count_browse_history().unwrap(), 1);

        let stream = storage.stream_browse_history(0, 10).unwrap();
        assert_eq!(stream.len(), 1);
        assert_eq!(stream[0].payload_json, Some("{\"title\":\"test1-updated\"}".to_string()));

        storage.clear_browse_history().unwrap();
        assert_eq!(storage.count_browse_history().unwrap(), 0);
    }

    #[test]
    fn test_subscription_download_identity_and_orphans() {
        let storage = StorageEngine::new(":memory:".to_string()).unwrap();

        let r1 = storage
            .add_or_replace_subscription_download_history(
                "1".to_string(),
                Some("Illustration".to_string()),
                "C:/dest1.jpg".to_string(),
                3,
                None,
                None,
                10,
                "art1".to_string(),
                "{\"art\":1}".to_string(),
            )
            .unwrap();
        assert_eq!(r1.work_subscription_id, 10);
        assert_eq!(r1.state, 3);

        assert!(storage
            .contains_subscription_download_identity(10, "art1".to_string(), "C:/dest1.jpg".to_string())
            .unwrap());
        assert!(!storage
            .contains_subscription_download_identity(10, "art1".to_string(), "C:/other.jpg".to_string())
            .unwrap());

        // Batch addition
        let entries = vec![
            SubscriptionDownloadHistoryRecord {
                history_entry_id: 0,
                id: "2".to_string(),
                serialize_key: Some("Illustration".to_string()),
                destination: "C:/dest2.jpg".to_string(),
                state: 3,
                format_token: None,
                error_message: None,
                work_subscription_id: 20,
                artwork_id: "art2".to_string(),
                payload_json: Some("{\"art\":2}".to_string()),
            },
            SubscriptionDownloadHistoryRecord {
                history_entry_id: 0,
                id: "3".to_string(),
                serialize_key: Some("Illustration".to_string()),
                destination: "C:/dest3.jpg".to_string(),
                state: 3,
                format_token: None,
                error_message: None,
                work_subscription_id: 30,
                artwork_id: "art3".to_string(),
                payload_json: Some("{\"art\":3}".to_string()),
            },
        ];
        storage.add_or_replace_subscription_download_history_batch(entries).unwrap();
        assert_eq!(storage.count_subscription_download_history().unwrap(), 3);

        // Delete orphans: keeping only sub 10 and 20
        let deleted = storage.delete_orphan_subscription_downloads(vec![10, 20]).unwrap();
        assert_eq!(deleted, 1);
        assert_eq!(storage.count_subscription_download_history().unwrap(), 2);
    }

    #[test]
    fn test_subscription_rules_and_blocked_users() {
        let storage = StorageEngine::new(":memory:".to_string()).unwrap();

        let sub = storage
            .upsert_subscription(
                12345,
                1,
                0,
                "Artist Name".to_string(),
                "Artist".to_string(),
                "avatar.jpg".to_string(),
                "2026-10-06".to_string(),
                Some("99999".to_string()),
            )
            .unwrap();
        assert_eq!(sub.id, 12345);

        let fetched = storage.get_subscription_by_key(12345, 1, 0).unwrap();
        assert!(fetched.is_some());
        assert_eq!(fetched.unwrap().title, "Artist Name");

        // Blocked user
        storage.add_or_update_blocked_user(999, "Spammer".to_string(), "avatar.jpg".to_string(), "spammer_acc".to_string()).unwrap();
        assert!(storage.is_user_blocked(999).unwrap());
        assert!(!storage.is_user_blocked(1000).unwrap());

        let all_blocked = storage.get_all_blocked_users().unwrap();
        assert_eq!(all_blocked.len(), 1);
        assert_eq!(all_blocked[0].account, "spammer_acc");

        storage.try_delete_blocked_user(999).unwrap();
        assert!(!storage.is_user_blocked(999).unwrap());
    }

    #[derive(Clone)]
    struct TestObserver {
        browse_count: Arc<std::sync::atomic::AtomicUsize>,
        watch_later_count: Arc<std::sync::atomic::AtomicUsize>,
        download_count: Arc<std::sync::atomic::AtomicUsize>,
        search_count: Arc<std::sync::atomic::AtomicUsize>,
    }

    impl TestObserver {
        fn new() -> Self {
            Self {
                browse_count: Arc::new(std::sync::atomic::AtomicUsize::new(0)),
                watch_later_count: Arc::new(std::sync::atomic::AtomicUsize::new(0)),
                download_count: Arc::new(std::sync::atomic::AtomicUsize::new(0)),
                search_count: Arc::new(std::sync::atomic::AtomicUsize::new(0)),
            }
        }
    }

    impl StorageObserver for TestObserver {
        fn on_browse_history_changed(&self) {
            self.browse_count.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        }
        fn on_watch_later_changed(&self) {
            self.watch_later_count.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        }
        fn on_download_history_changed(&self) {
            self.download_count.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        }
        fn on_search_history_changed(&self) {
            self.search_count.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        }
    }

    #[test]
    fn test_storage_observer_and_repositories() {
        let storage = StorageEngine::new(":memory:".to_string()).unwrap();
        let observer = TestObserver::new();
        storage.register_observer(Box::new(observer.clone()));

        // History repository
        let h_repo = storage.history();
        h_repo.add_or_replace_browse_history("1".to_string(), None, "Work:1".to_string(), "{}".to_string()).unwrap();
        assert_eq!(observer.browse_count.load(std::sync::atomic::Ordering::SeqCst), 1);

        h_repo.insert_search_history("query".to_string(), None, "time".to_string()).unwrap();
        assert_eq!(observer.search_count.load(std::sync::atomic::Ordering::SeqCst), 1);

        // Watch later repository
        let wl_repo = storage.watch_later();
        assert!(!wl_repo.contains_watch_later("Work:2".to_string()).unwrap());
        wl_repo.add_or_replace_watch_later("2".to_string(), None, "Work:2".to_string(), "{}".to_string()).unwrap();
        assert_eq!(observer.watch_later_count.load(std::sync::atomic::Ordering::SeqCst), 1);
        assert!(wl_repo.contains_watch_later("Work:2".to_string()).unwrap());

        // Download repository
        let dl_repo = storage.download();
        dl_repo.add_or_replace_download_history("1".to_string(), None, "dest1.jpg".to_string(), 1, None, None, "{}".to_string()).unwrap();
        assert_eq!(observer.download_count.load(std::sync::atomic::Ordering::SeqCst), 1);

        dl_repo.update_download_history_state("dest1.jpg".to_string(), 3, None).unwrap();
        assert_eq!(observer.download_count.load(std::sync::atomic::Ordering::SeqCst), 2);
    }

    #[test]
    fn test_cursor_pagination() {
        let storage = StorageEngine::new(":memory:".to_string()).unwrap();
        let wl_repo = storage.watch_later();

        for i in 1..=5 {
            wl_repo.add_or_replace_watch_later(
                i.to_string(),
                None,
                format!("Work:{i}"),
                format!("{{\"id\":{i}}}"),
            ).unwrap();
        }

        // Page 1: take 2
        let page1 = wl_repo.stream_watch_later_cursor(None, 2).unwrap();
        assert_eq!(page1.len(), 2);
        assert_eq!(page1[0].id, "5");
        assert_eq!(page1[1].id, "4");

        // Page 2: take 2 with cursor at id 4
        let page2 = wl_repo.stream_watch_later_cursor(Some(page1[1].history_entry_id), 2).unwrap();
        assert_eq!(page2.len(), 2);
        assert_eq!(page2[0].id, "3");
        assert_eq!(page2[1].id, "2");

        // Page 3: take 2 with cursor at id 2
        let page3 = wl_repo.stream_watch_later_cursor(Some(page2[1].history_entry_id), 2).unwrap();
        assert_eq!(page3.len(), 1);
        assert_eq!(page3[0].id, "1");
    }

    #[test]
    fn test_login_user_upsert_stability() {
        let storage = StorageEngine::new(":memory:".to_string()).unwrap();

        let initial = LoginUserRecord {
            history_entry_id: 0,
            user_id: 123456,
            refresh_token: "token_abc".to_string(),
            name: "TestUser".to_string(),
            account: "test_acc".to_string(),
            mail_address: "test@example.com".to_string(),
            is_premium: false,
            x_restrict: 0,
            is_mail_authorized: true,
            require_policy_agreement: false,
            avatar16_url: String::new(),
            avatar50_url: String::new(),
            avatar170_url: String::new(),
        };

        let inserted = storage.upsert_login_user(initial).unwrap();
        let hid = inserted.history_entry_id;
        assert!(hid > 0);

        // Upsert by user_id with history_entry_id = 0
        let mut update1 = inserted.clone();
        update1.history_entry_id = 0;
        update1.name = "TestUser2".to_string();
        update1.refresh_token = "token_xyz".to_string();
        let updated1 = storage.upsert_login_user(update1).unwrap();
        assert_eq!(updated1.history_entry_id, hid);
        assert_eq!(updated1.name, "TestUser2");

        // Upsert by history_entry_id
        let mut update2 = updated1.clone();
        update2.name = "TestUser3".to_string();
        let updated2 = storage.upsert_login_user(update2).unwrap();
        assert_eq!(updated2.history_entry_id, hid);
        assert_eq!(updated2.name, "TestUser3");
    }
}
