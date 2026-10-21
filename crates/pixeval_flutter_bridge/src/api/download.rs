use crate::frb_generated::StreamSink;
use crate::state::{APP_STATE, DOWNLOAD_EVENT_TASKS, cancel_event_task, spawn_event_task};
use pixeval_download::DownloadTaskKey;

#[derive(Clone, Debug, PartialEq)]
pub enum DownloadProgressEvent {
    ProgressUpdated {
        destination: String,
        progress_percentage: f64,
        downloaded_bytes: u64,
        total_bytes: u64,
    },
    StateChanged {
        destination: String,
        state: String,
        error_message: Option<String>,
    },
    Completed {
        destination: String,
    },
}

#[derive(Clone, Debug, PartialEq)]
pub struct DownloadTaskSummary {
    pub destination: String,
    pub artwork_id: Option<String>,
    pub state: String,
    pub progress_percentage: f64,
    pub downloaded_bytes: u64,
    pub total_bytes: u64,
}

pub fn create_download_event_stream(
    sink: StreamSink<DownloadProgressEvent>,
    stream_id: Option<String>,
) {
    let mut rx = APP_STATE.download_event_sender.subscribe();
    spawn_event_task(
        &DOWNLOAD_EVENT_TASKS,
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

pub fn cancel_download_event_stream(stream_id: Option<String>) {
    cancel_event_task(
        &DOWNLOAD_EVENT_TASKS,
        stream_id.unwrap_or_else(|| "legacy".into()),
    );
}

pub fn init_download_manager(concurrency: u32) -> Result<(), String> {
    let _ = APP_STATE.init_download_manager_with_concurrency(concurrency)?;
    Ok(())
}

pub fn start_download(
    url: String,
    destination: String,
    artwork_id: Option<String>,
    overwrite: bool,
) -> Result<(), String> {
    let dm = APP_STATE.get_or_init_download_manager()?;
    let key = DownloadTaskKey {
        destination: destination.clone(),
        work_subscription_id: 0,
        artwork_id,
    };
    dm.enqueue_task(key, url, destination, overwrite);
    Ok(())
}

pub fn pause_download(destination: String) -> bool {
    if let Ok(dm) = APP_STATE.get_or_init_download_manager() {
        if let Some(key) = dm.find_key_by_destination(&destination) {
            dm.pause_task(key)
        } else {
            dm.pause_task(DownloadTaskKey::new_ordinary(destination))
        }
    } else {
        false
    }
}

pub fn resume_download(destination: String) -> bool {
    if let Ok(dm) = APP_STATE.get_or_init_download_manager() {
        if let Some(key) = dm.find_key_by_destination(&destination) {
            dm.resume_task(key)
        } else {
            dm.resume_task(DownloadTaskKey::new_ordinary(destination))
        }
    } else {
        false
    }
}

pub fn cancel_download(destination: String) -> bool {
    if let Ok(dm) = APP_STATE.get_or_init_download_manager() {
        if let Some(key) = dm.find_key_by_destination(&destination) {
            dm.cancel_task(key)
        } else {
            dm.cancel_task(DownloadTaskKey::new_ordinary(destination))
        }
    } else {
        false
    }
}

pub fn remove_download(destination: String) -> bool {
    if let Ok(dm) = APP_STATE.get_or_init_download_manager() {
        if let Some(key) = dm.find_key_by_destination(&destination) {
            dm.remove_task(key)
        } else {
            dm.remove_task(DownloadTaskKey::new_ordinary(destination))
        }
    } else {
        false
    }
}

pub fn clear_all_downloads() {
    if let Ok(dm) = APP_STATE.get_or_init_download_manager() {
        dm.clear_native_tasks();
    }
}
