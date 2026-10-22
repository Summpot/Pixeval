use super::api::{download::DownloadProgressEvent, events::SessionEvent};
use super::state::APP_STATE;
use parking_lot::Mutex;
use std::collections::VecDeque;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Weak};
use tokio::sync::Notify;

/// A bounded queue reports overflow rather than silently losing terminal or
/// session events. Subscribers own their cancellation and native lifetime.
struct EventQueue<T> {
    values: Mutex<VecDeque<T>>,
    notify: Notify,
    cancelled: AtomicBool,
    overflow: AtomicBool,
    capacity: usize,
}

impl<T> EventQueue<T> {
    fn cancel(&self) {
        self.cancelled.store(true, Ordering::Release);
        self.notify.notify_one();
    }
    async fn next(&self) -> Result<Option<T>, String> {
        loop {
            let notified = self.notify.notified();
            tokio::pin!(notified);
            notified.as_mut().enable();
            if self.cancelled.load(Ordering::Acquire) {
                return Ok(None);
            }
            if self.overflow.load(Ordering::Acquire) {
                self.cancel();
                return Err("Native event subscriber exceeded its queue capacity".into());
            }
            if let Some(event) = self.values.lock().pop_front() {
                return Ok(Some(event));
            }
            notified.await;
        }
    }
}

pub struct EventHub<T> {
    subscribers: Arc<Mutex<Vec<Weak<EventQueue<T>>>>>,
    capacity: usize,
}
impl<T> Clone for EventHub<T> {
    fn clone(&self) -> Self {
        Self {
            subscribers: self.subscribers.clone(),
            capacity: self.capacity,
        }
    }
}
impl<T: Clone> EventHub<T> {
    pub fn new(capacity: usize) -> Self {
        Self {
            subscribers: Default::default(),
            capacity,
        }
    }
    fn subscribe(&self) -> Arc<EventQueue<T>> {
        let queue = Arc::new(EventQueue {
            values: Default::default(),
            notify: Notify::new(),
            cancelled: AtomicBool::new(false),
            overflow: AtomicBool::new(false),
            capacity: self.capacity,
        });
        self.subscribers.lock().push(Arc::downgrade(&queue));
        queue
    }
    pub fn send(&self, event: T) -> Result<(), ()> {
        let mut subscribers = self.subscribers.lock();
        subscribers.retain(|subscriber| {
            let Some(queue) = subscriber.upgrade() else {
                return false;
            };
            if queue.cancelled.load(Ordering::Acquire) {
                return false;
            }
            let mut values = queue.values.lock();
            if values.len() >= queue.capacity {
                queue.overflow.store(true, Ordering::Release);
            } else {
                values.push_back(event.clone());
            }
            drop(values);
            queue.notify.notify_one();
            true
        });
        Ok(())
    }
}

#[derive(uniffi::Object)]
pub struct SessionEventReceiver {
    inner: Arc<EventQueue<SessionEvent>>,
}
#[uniffi::export(async_runtime = "tokio")]
#[boltffi::export]
impl SessionEventReceiver {
    #[uniffi::constructor]
    pub fn new() -> Self {
        Self {
            inner: APP_STATE.event_broadcaster.subscribe(),
        }
    }
    pub async fn next(&self) -> Result<Option<SessionEvent>, String> {
        async_compat::Compat::new(self.inner.next()).await
    }
    pub fn cancel(&self) {
        self.inner.cancel();
    }
}
impl Drop for SessionEventReceiver {
    fn drop(&mut self) {
        self.inner.cancel();
    }
}

#[derive(uniffi::Object)]
pub struct DownloadEventReceiver {
    inner: Arc<EventQueue<DownloadProgressEvent>>,
}
#[uniffi::export(async_runtime = "tokio")]
#[boltffi::export]
impl DownloadEventReceiver {
    #[uniffi::constructor]
    pub fn new() -> Self {
        Self {
            inner: APP_STATE.download_event_sender.subscribe(),
        }
    }
    pub async fn next(&self) -> Result<Option<DownloadProgressEvent>, String> {
        async_compat::Compat::new(self.inner.next()).await
    }
    pub fn cancel(&self) {
        self.inner.cancel();
    }
}
impl Drop for DownloadEventReceiver {
    fn drop(&mut self) {
        self.inner.cancel();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[tokio::test]
    async fn cancellation_interrupts_pending_read() {
        let hub = EventHub::<u32>::new(1);
        let receiver = hub.subscribe();
        let pending = receiver.next();
        receiver.cancel();
        assert_eq!(pending.await.unwrap(), None);
    }
    #[tokio::test]
    async fn queue_reports_overflow() {
        let hub = EventHub::new(1);
        let receiver = hub.subscribe();
        hub.send(1).unwrap();
        hub.send(2).unwrap();
        assert!(receiver.next().await.is_err());
        assert_eq!(receiver.next().await.unwrap(), None);
    }
}
