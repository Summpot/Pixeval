use crate::app::state::APP_STATE;
use pixeval_download::DownloadTaskKey;

#[derive(Clone, Debug, PartialEq, uniffi::Enum)]
#[boltffi::data]
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

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
#[boltffi::data]
pub struct DownloadTaskSummary {
    pub destination: String,
    pub artwork_id: Option<String>,
    pub state: String,
    pub progress_percentage: f64,
    pub downloaded_bytes: u64,
    pub total_bytes: u64,
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

pub mod ffi {

    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn init_download_manager(concurrency: u32) -> Result<(), String> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::init_download_manager(concurrency))
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn start_download(
        url: String,
        destination: String,
        artwork_id: Option<String>,
        overwrite: bool,
    ) -> Result<(), String> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || {
                super::start_download(url, destination, artwork_id, overwrite)
            })
            .await
            .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn pause_download(destination: String) -> bool {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::pause_download(destination))
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn resume_download(destination: String) -> bool {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::resume_download(destination))
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn cancel_download(destination: String) -> bool {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::cancel_download(destination))
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn remove_download(destination: String) -> bool {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::remove_download(destination))
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn clear_all_downloads() {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::clear_all_downloads())
                .await
                .expect("native application worker panicked")
        })
        .await
    }
}
