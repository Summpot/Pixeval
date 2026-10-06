uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod engine;
pub mod error;
pub mod models;
pub mod schema;

pub use engine::*;
pub use error::*;
pub use models::*;

#[cfg(test)]
mod tests {
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
}
