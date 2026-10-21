use crate::frb_generated::StreamSink;
use crate::state::{APP_STATE, SESSION_EVENT_TASKS, cancel_event_task, spawn_event_task};

#[derive(Clone, Debug)]
pub enum SessionEvent {
    AuthInvalidated { reason: String },
    RateLimited { retry_after_seconds: u64 },
}

pub fn create_session_event_stream(sink: StreamSink<SessionEvent>, stream_id: Option<String>) {
    let mut rx = APP_STATE.event_broadcaster.subscribe();
    spawn_event_task(
        &SESSION_EVENT_TASKS,
        stream_id.unwrap_or_else(|| "legacy".into()),
        async move {
            loop {
                match rx.recv().await {
                    Ok(event) => {
                        if sink.add(event).is_err() {
                            break;
                        }
                    }
                    Err(tokio::sync::broadcast::error::RecvError::Lagged(_)) => {
                        continue;
                    }
                    Err(tokio::sync::broadcast::error::RecvError::Closed) => {
                        break;
                    }
                }
            }
        },
    );
}

pub fn cancel_session_event_stream(stream_id: Option<String>) {
    cancel_event_task(
        &SESSION_EVENT_TASKS,
        stream_id.unwrap_or_else(|| "legacy".into()),
    );
}
