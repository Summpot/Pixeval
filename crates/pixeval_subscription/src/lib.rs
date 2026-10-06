uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod engine;
pub mod error;
pub mod models;
pub mod queue;

pub use engine::*;
pub use error::*;
pub use models::*;
pub use queue::*;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_duplicate_stop_fuse() {
        let engine = SubscriptionSyncEngine::new(Some(3), None);

        // 1st duplicate
        assert!(!engine.record_work_processed(1, true));
        // 2nd duplicate
        assert!(!engine.record_work_processed(1, true));
        // Non-duplicate resets
        assert!(!engine.record_work_processed(1, false));

        // 1st duplicate again
        assert!(!engine.record_work_processed(1, true));
        // 2nd duplicate
        assert!(!engine.record_work_processed(1, true));
        // 3rd duplicate triggers stop!
        assert!(engine.record_work_processed(1, true));
    }

    #[test]
    fn test_queue_superseding_and_deduplication() {
        let engine = SubscriptionSyncEngine::new(None, None);

        assert!(engine.queue_sync_subscription(1));
        assert!(!engine.queue_sync_subscription(1)); // duplicate rejected
        assert!(engine.queue_sync_subscription(2));
        assert_eq!(engine.get_pending_count(), 2);

        // Global sync supersedes all pending
        assert!(engine.queue_sync_all());
        assert_eq!(engine.get_pending_count(), 1);

        let req = engine.try_dequeue_sync_request();
        assert_eq!(req, Some(SyncRequestKind::All));

        engine.complete_active_sync_request(SyncRequestKind::All);
        assert_eq!(engine.get_pending_count(), 0);
    }

    #[test]
    fn test_fetch_state_lifecycle() {
        let engine = SubscriptionSyncEngine::new(None, None);

        engine.set_fetch_state(42, 1, 30, SubscriptionStatus::Fetching);
        let state = engine.get_fetch_state(42).unwrap();
        assert_eq!(state.page, 1);
        assert_eq!(state.total_fetched, 30);
        assert_eq!(state.status, SubscriptionStatus::Fetching);

        engine.set_fetch_state(42, 2, 60, SubscriptionStatus::Completed);
        let state2 = engine.get_fetch_state(42).unwrap();
        assert_eq!(state2.page, 2);
        assert_eq!(state2.total_fetched, 60);
        assert_eq!(state2.status, SubscriptionStatus::Completed);
    }

    #[test]
    fn test_cancellation_and_config_update() {
        let engine = SubscriptionSyncEngine::new(Some(5), None);
        assert!(!engine.is_sync_in_progress());

        let config = SubscriptionSyncConfig {
            download_path_macro: "@{id}".to_string(),
            base_download_dir: "C:/Downloads".to_string(),
            overwrite: true,
            duplicate_stop_threshold: 10,
            my_user_id: Some(12345),
        };
        engine.update_config(config);

        assert!(engine.queue_sync_subscription(100));
        assert!(engine.queue_sync_subscription(200));
        assert_eq!(engine.get_pending_count(), 2);

        engine.cancel_all();
        assert_eq!(engine.get_pending_count(), 0);
        assert!(!engine.is_sync_in_progress());
    }
}
