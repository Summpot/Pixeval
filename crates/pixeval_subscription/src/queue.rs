// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::VecDeque;
use crate::models::SyncRequestKind;

#[derive(Default, Debug)]
pub struct SubscriptionSyncQueue {
    requests: VecDeque<SyncRequestKind>,
}

impl SubscriptionSyncQueue {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn try_enqueue(&mut self, request: SyncRequestKind, active_request: Option<&SyncRequestKind>) -> bool {
        match request {
            SyncRequestKind::All => {
                if let Some(SyncRequestKind::All) = active_request {
                    return false;
                }
                if self.requests.contains(&SyncRequestKind::All) {
                    return false;
                }
                self.requests.clear();
                self.requests.push_back(SyncRequestKind::All);
                true
            }
            SyncRequestKind::Single { subscription_id } => {
                if self.requests.contains(&SyncRequestKind::All) {
                    return false;
                }
                if let Some(SyncRequestKind::Single { subscription_id: active_id }) = active_request {
                    if *active_id == subscription_id {
                        return false;
                    }
                }
                if self.requests.iter().any(|r| match r {
                    SyncRequestKind::Single { subscription_id: id } => *id == subscription_id,
                    _ => false,
                }) {
                    return false;
                }
                self.requests.push_back(SyncRequestKind::Single { subscription_id });
                true
            }
        }
    }

    pub fn remove_subscription(&mut self, subscription_id: i64) {
        self.requests.retain(|r| match r {
            SyncRequestKind::Single { subscription_id: id } => *id != subscription_id,
            SyncRequestKind::All => true,
        });
    }

    pub fn try_dequeue(&mut self) -> Option<SyncRequestKind> {
        self.requests.pop_front()
    }

    pub fn count(&self) -> usize {
        self.requests.len()
    }

    pub fn clear(&mut self) {
        self.requests.clear();
    }
}
