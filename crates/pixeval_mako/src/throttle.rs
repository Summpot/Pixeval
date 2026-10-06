// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::sync::atomic::{AtomicU64, Ordering};
use std::time::{Duration, Instant};
use tokio::sync::Mutex;

pub struct RequestThrottler {
    cooldown_ms: AtomicU64,
    last_request: Mutex<Option<Instant>>,
}

impl RequestThrottler {
    pub fn new(cooldown_ms: u64) -> Self {
        Self {
            cooldown_ms: AtomicU64::new(cooldown_ms),
            last_request: Mutex::new(None),
        }
    }

    pub fn set_cooldown_ms(&self, cooldown_ms: u64) {
        self.cooldown_ms.store(cooldown_ms, Ordering::Relaxed);
    }

    pub async fn throttle(&self) {
        let cooldown = Duration::from_millis(self.cooldown_ms.load(Ordering::Relaxed));
        let mut guard = self.last_request.lock().await;
        if let Some(last) = *guard {
            let elapsed = last.elapsed();
            if elapsed < cooldown {
                tokio::time::sleep(cooldown - elapsed).await;
            }
        }
        *guard = Some(Instant::now());
    }
}
