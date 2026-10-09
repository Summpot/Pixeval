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

#[uniffi::export]
pub fn compute_subscription_folder_snapshot(
    subscription_id: i64,
    items: Vec<FolderTaskItemState>,
    is_fetching: bool,
    fetched_count: u32,
    retry_at_timestamp: Option<i64>,
) -> SubscriptionFolderSnapshot {
    let total_count = items.len() as u32;
    if total_count == 0 {
        return SubscriptionFolderSnapshot {
            subscription_id,
            total_count: 0,
            active_count: 0,
            completed_count: 0,
            error_count: 0,
            progress_percentage: 100.0,
            current_state: 7, // Completed
            is_fetching,
            fetched_count,
            retry_at_timestamp,
        };
    }

    let mut active_count = 0u32;
    let mut completed_count = 0u32;
    let mut error_count = 0u32;
    let mut sum_progress = 0.0f64;

    let mut has_error = false;
    let mut has_cancelled = false;
    let mut has_paused = false;
    let mut has_running = false;
    let mut has_queued = false;
    let mut has_pending = false;

    for item in &items {
        active_count += item.active_count;
        completed_count += item.completed_count;
        error_count += item.error_count;
        sum_progress += item.progress_percentage;

        match item.state {
            5 => has_error = true,     // Error = 5
            4 => has_cancelled = true, // Cancelled = 4
            3 => has_paused = true,    // Paused = 3
            2 => has_running = true,   // Running = 2
            1 => has_queued = true,    // Queued = 1
            6 => has_pending = true,   // Pending = 6
            _ => {}
        }
    }

    let current_state = if has_error {
        5 // Error
    } else if has_cancelled {
        4 // Cancelled
    } else if has_paused {
        3 // Paused
    } else if has_running {
        2 // Running
    } else if has_queued {
        1 // Queued
    } else if has_pending {
        6 // Pending
    } else {
        7 // Completed
    };

    let progress_percentage = sum_progress / total_count as f64;

    SubscriptionFolderSnapshot {
        subscription_id,
        total_count,
        active_count,
        completed_count,
        error_count,
        progress_percentage,
        current_state,
        is_fetching,
        fetched_count,
        retry_at_timestamp,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_compute_subscription_folder_snapshot() {
        let items = vec![
            FolderTaskItemState {
                state: 2, // Running
                progress_percentage: 50.0,
                active_count: 1,
                completed_count: 0,
                error_count: 0,
            },
            FolderTaskItemState {
                state: 7, // Completed
                progress_percentage: 100.0,
                active_count: 0,
                completed_count: 1,
                error_count: 0,
            },
        ];

        let snapshot = compute_subscription_folder_snapshot(123, items, true, 5, Some(1700000000));
        assert_eq!(snapshot.subscription_id, 123);
        assert_eq!(snapshot.total_count, 2);
        assert_eq!(snapshot.active_count, 1);
        assert_eq!(snapshot.completed_count, 1);
        assert_eq!(snapshot.error_count, 0);
        assert_eq!(snapshot.progress_percentage, 75.0);
        assert_eq!(snapshot.current_state, 2); // Running
        assert!(snapshot.is_fetching);
        assert_eq!(snapshot.fetched_count, 5);
        assert_eq!(snapshot.retry_at_timestamp, Some(1700000000));
    }


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
