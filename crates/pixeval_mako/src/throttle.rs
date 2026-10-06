// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::sync::atomic::{AtomicU64, Ordering};
use std::time::{Duration, Instant};
use tokio::sync::Mutex;

pub struct RequestThrottler {
    cooldown_ms: AtomicU64,
    last_request: Mutex<Option<Instant>>,
    rate_limit_until: parking_lot::RwLock<Option<Instant>>,
    request_gate: Mutex<()>,
}

impl RequestThrottler {
    pub fn new(cooldown_ms: u64) -> Self {
        Self {
            cooldown_ms: AtomicU64::new(cooldown_ms),
            last_request: Mutex::new(None),
            rate_limit_until: parking_lot::RwLock::new(None),
            request_gate: Mutex::new(()),
        }
    }

    pub fn set_cooldown_ms(&self, cooldown_ms: u64) {
        self.cooldown_ms.store(cooldown_ms, Ordering::Relaxed);
    }

    pub fn update_rate_limit(&self, retry_after: Duration) {
        let until = Instant::now() + retry_after;
        let mut guard = self.rate_limit_until.write();
        if let Some(existing) = *guard {
            if until > existing {
                *guard = Some(until);
            }
        } else {
            *guard = Some(until);
        }
    }

    pub fn rate_limit_remaining(&self) -> Option<Duration> {
        let guard = self.rate_limit_until.read();
        if let Some(until) = *guard {
            let now = Instant::now();
            if until > now {
                return Some(until - now);
            }
        }
        None
    }

    pub fn parse_retry_after(header_val: &str) -> Duration {
        let trimmed = header_val.trim();
        if let Ok(secs) = trimmed.parse::<u64>() {
            return Duration::from_secs(secs);
        }
        if let Ok(date) = httpdate::parse_http_date(trimmed) {
            if let Ok(dur) = date.duration_since(std::time::SystemTime::now()) {
                return dur;
            }
        }
        Duration::from_secs(60)
    }

    /// Acquires the serialization gate across requests and waits for any rate-limit or cooldown.
    /// The returned guard holds the serialization lock until dropped, ensuring concurrent calls are serialized.
    pub async fn acquire_gate(&self) -> tokio::sync::MutexGuard<'_, ()> {
        let guard = self.request_gate.lock().await;

        // If currently under rate limit, wait until lifted
        if let Some(rem) = self.rate_limit_remaining() {
            tokio::time::sleep(rem).await;
        }

        // Wait for cooldown from the previous request
        let cooldown = Duration::from_millis(self.cooldown_ms.load(Ordering::Relaxed));
        let mut last_guard = self.last_request.lock().await;
        if let Some(last) = *last_guard {
            let elapsed = last.elapsed();
            if elapsed < cooldown {
                tokio::time::sleep(cooldown - elapsed).await;
            }
        }
        *last_guard = Some(Instant::now());

        guard
    }

    /// Convenience standalone throttle for background tasks or backward compatibility
    pub async fn throttle(&self) {
        let _g = self.acquire_gate().await;
    }
}
