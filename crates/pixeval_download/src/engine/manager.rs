// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::path::Path;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::{Arc, LazyLock};

use futures_util::StreamExt;
use parking_lot::RwLock;
use tokio::fs::{self, File};
use tokio::io::AsyncWriteExt;
use tokio::sync::Semaphore;
use tokio_util::sync::CancellationToken;

use pixeval_maho::{DnsResolver, MahoConfig, MahoHttpClient};

use crate::engine::callback::DownloadProgressCallback;
use crate::engine::key::DownloadTaskKey;
use crate::engine::state::DownloadState;
use crate::engine::task::{DownloadProgressInfo, DownloadTaskItem};

static DOWNLOAD_RUNTIME: LazyLock<tokio::runtime::Runtime> = LazyLock::new(|| {
    tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .thread_name("pixeval-download-worker")
        .build()
        .expect("Failed to build Tokio runtime for download manager")
});

#[derive(uniffi::Record, Clone, Debug, Default)]
pub struct DownloadNetworkOptions {
    pub proxy_url: Option<String>,
    pub static_domain_ips: HashMap<String, Vec<String>>,
}

#[derive(uniffi::Object)]
pub struct DownloadManager {
    concurrency_degree: Arc<AtomicUsize>,
    semaphore: Arc<Semaphore>,
    tasks: Arc<RwLock<HashMap<DownloadTaskKey, DownloadTaskItem>>>,
    cancel_tokens: Arc<RwLock<HashMap<DownloadTaskKey, CancellationToken>>>,
    callback: Option<Arc<dyn DownloadProgressCallback>>,
    client: Arc<RwLock<MahoHttpClient>>,
}

#[uniffi::export]
impl DownloadManager {
    #[uniffi::constructor]
    pub fn new(
        concurrency_degree: u32,
        callback: Option<Box<dyn DownloadProgressCallback>>,
        network_options: Option<DownloadNetworkOptions>,
    ) -> Arc<Self> {
        let concurrency = (concurrency_degree as usize).max(1);
        let client = Self::build_client(network_options.as_ref());
        let cb: Option<Arc<dyn DownloadProgressCallback>> = callback.map(Arc::from);

        Arc::new(Self {
            concurrency_degree: Arc::new(AtomicUsize::new(concurrency)),
            semaphore: Arc::new(Semaphore::new(concurrency)),
            tasks: Arc::new(RwLock::new(HashMap::new())),
            cancel_tokens: Arc::new(RwLock::new(HashMap::new())),
            callback: cb,
            client: Arc::new(RwLock::new(client)),
        })
    }

    pub fn update_network_options(&self, network_options: Option<DownloadNetworkOptions>) {
        let new_client = Self::build_client(network_options.as_ref());
        *self.client.write() = new_client;
    }

    pub fn enqueue_task(
        &self,
        key: DownloadTaskKey,
        url: String,
        destination: String,
        overwrite: bool,
    ) {
        let item = DownloadTaskItem::new(key.clone(), url.clone(), destination.clone(), overwrite);
        self.tasks.write().insert(key.clone(), item);

        let token = CancellationToken::new();
        self.cancel_tokens
            .write()
            .insert(key.clone(), token.clone());

        if let Some(cb) = &self.callback {
            cb.on_state_changed(key.clone(), DownloadState::Queued, None);
        }

        let tasks = self.tasks.clone();
        let cancel_tokens = self.cancel_tokens.clone();
        let sem_arc = self.semaphore.clone();
        let callback = self.callback.clone();
        let client = self.client.read().clone();

        DOWNLOAD_RUNTIME.spawn(async move {
            let _permit = match sem_arc.acquire().await {
                Ok(p) => p,
                Err(_) => return,
            };

            if token.is_cancelled() {
                return;
            }

            // Check if already finished or should skip existing
            if !overwrite && Path::new(&destination).exists() {
                {
                    let mut lock = tasks.write();
                    if let Some(t) = lock.get_mut(&key) {
                        t.state = DownloadState::Completed;
                        t.progress_percentage = 100.0;
                    }
                }
                cancel_tokens.write().remove(&key);
                if let Some(cb) = &callback {
                    cb.on_progress(key.clone(), 100.0, 0, 0);
                    cb.on_state_changed(key.clone(), DownloadState::Completed, None);
                    cb.on_completed(key.clone(), destination);
                }
                return;
            }

            // Start downloading
            {
                let mut lock = tasks.write();
                if let Some(t) = lock.get_mut(&key) {
                    t.state = DownloadState::Running;
                }
            }
            if let Some(cb) = &callback {
                cb.on_state_changed(key.clone(), DownloadState::Running, None);
            }

            let temp_dest = format!("{destination}.pixevaldownloading");
            if let Some(parent) = Path::new(&temp_dest).parent() {
                let _ = fs::create_dir_all(parent).await;
            }

            const MAX_RETRIES: u32 = 3;
            let mut last_error: Option<String> = None;

            for attempt in 1..=MAX_RETRIES {
                if token.is_cancelled() {
                    let _ = fs::remove_file(&temp_dest).await;
                    cancel_tokens.write().remove(&key);
                    return;
                }

                if attempt > 1 {
                    let backoff_ms = 500 * (1 << (attempt - 2));
                    tokio::select! {
                        _ = tokio::time::sleep(std::time::Duration::from_millis(backoff_ms)) => {},
                        _ = token.cancelled() => {
                            let _ = fs::remove_file(&temp_dest).await;
                            cancel_tokens.write().remove(&key);
                            return;
                        }
                    }
                }

                let _ = fs::remove_file(&temp_dest).await;

                let send_future = client
                    .get(&url)
                    .header("Referer", "https://app-api.pixiv.net/")
                    .header("User-Agent", "PixivAndroidApp/6.199.0 (Android 15.0; Pixel 8)")
                    .send();

                let response = tokio::select! {
                    res = send_future => res,
                    _ = token.cancelled() => {
                        let _ = fs::remove_file(&temp_dest).await;
                        cancel_tokens.write().remove(&key);
                        return;
                    }
                };

                let response = match response {
                    Ok(r) if r.status().is_success() => r,
                    Ok(r) => {
                        let status = r.status();
                        let err_msg = format!("HTTP error: {status}");
                        if status == http::StatusCode::NOT_FOUND || status.is_client_error() {
                            Self::handle_error(
                                &tasks,
                                &cancel_tokens,
                                &key,
                                &temp_dest,
                                &err_msg,
                                callback.as_deref(),
                            )
                            .await;
                            return;
                        }
                        last_error = Some(err_msg);
                        continue;
                    }
                    Err(e) => {
                        last_error = Some(format!("Network error: {e}"));
                        continue;
                    }
                };

                let total_bytes = response.content_length().unwrap_or(0);
                let mut file = match File::create(&temp_dest).await {
                    Ok(f) => f,
                    Err(e) => {
                        let err_msg = format!("File create error: {e}");
                        Self::handle_error(
                            &tasks,
                            &cancel_tokens,
                            &key,
                            &temp_dest,
                            &err_msg,
                            callback.as_deref(),
                        )
                        .await;
                        return;
                    }
                };

                let mut downloaded_bytes = 0u64;
                let mut stream = response.bytes_stream();
                let mut stream_failed = false;

                loop {
                    let chunk_result = tokio::select! {
                        chunk_opt = stream.next() => {
                            match chunk_opt {
                                Some(res) => res,
                                None => break,
                            }
                        }
                        _ = token.cancelled() => {
                            drop(file);
                            let _ = fs::remove_file(&temp_dest).await;
                            cancel_tokens.write().remove(&key);
                            return;
                        }
                    };

                    match chunk_result {
                        Ok(chunk) => {
                            if let Err(e) = file.write_all(&chunk).await {
                                let err_msg = format!("Write error: {e}");
                                Self::handle_error(
                                    &tasks,
                                    &cancel_tokens,
                                    &key,
                                    &temp_dest,
                                    &err_msg,
                                    callback.as_deref(),
                                )
                                .await;
                                return;
                            }
                            downloaded_bytes += chunk.len() as u64;
                            let percentage = if total_bytes > 0 {
                                ((downloaded_bytes as f64 / total_bytes as f64) * 100.0).min(100.0)
                            } else {
                                0.0
                            };

                            {
                                let mut lock = tasks.write();
                                if let Some(t) = lock.get_mut(&key) {
                                    t.downloaded_bytes = downloaded_bytes;
                                    t.total_bytes = total_bytes;
                                    t.progress_percentage = percentage;
                                }
                            }

                            if let Some(cb) = &callback {
                                cb.on_progress(
                                    key.clone(),
                                    percentage,
                                    downloaded_bytes,
                                    total_bytes,
                                );
                            }
                        }
                        Err(e) => {
                            last_error = Some(format!("Stream read error: {e}"));
                            stream_failed = true;
                            break;
                        }
                    }
                }

                if stream_failed {
                    continue;
                }

                if let Err(e) = file.flush().await {
                    last_error = Some(format!("Flush error: {e}"));
                    continue;
                }
                drop(file);

                // Rename temp file to final destination
                if overwrite && Path::new(&destination).exists() {
                    let _ = fs::remove_file(&destination).await;
                }

                if let Err(e) = fs::rename(&temp_dest, &destination).await {
                    let err_msg = format!("Rename error: {e}");
                    Self::handle_error(
                        &tasks,
                        &cancel_tokens,
                        &key,
                        &temp_dest,
                        &err_msg,
                        callback.as_deref(),
                    )
                    .await;
                    return;
                }

                {
                    let mut lock = tasks.write();
                    if let Some(t) = lock.get_mut(&key) {
                        t.state = DownloadState::Completed;
                        t.progress_percentage = 100.0;
                        t.downloaded_bytes = total_bytes;
                    }
                }
                cancel_tokens.write().remove(&key);

                if let Some(cb) = &callback {
                    cb.on_progress(key.clone(), 100.0, total_bytes, total_bytes);
                    cb.on_state_changed(key.clone(), DownloadState::Completed, None);
                    cb.on_completed(key.clone(), destination);
                }
                return;
            }

            let err_msg =
                last_error.unwrap_or_else(|| "Download failed after multiple attempts".to_string());
            Self::handle_error(
                &tasks,
                &cancel_tokens,
                &key,
                &temp_dest,
                &err_msg,
                callback.as_deref(),
            )
            .await;
        });
    }

    pub fn pause_task(&self, key: DownloadTaskKey) -> bool {
        self.pause_task_ref(&key)
    }

    pub fn resume_task(&self, key: DownloadTaskKey) -> bool {
        self.resume_task_ref(&key)
    }

    pub fn cancel_task(&self, key: DownloadTaskKey) -> bool {
        self.cancel_task_ref(&key)
    }

    pub fn reset_task(&self, key: DownloadTaskKey) -> bool {
        self.reset_task_ref(&key)
    }

    pub fn remove_task(&self, key: DownloadTaskKey) -> bool {
        self.remove_task_ref(&key)
    }

    pub fn clear_native_tasks(&self) {
        let keys: Vec<_> = self.tasks.read().keys().cloned().collect();
        for key in keys {
            self.cancel_task_ref(&key);
        }
        self.tasks.write().clear();
    }

    pub fn get_task_info(&self, key: DownloadTaskKey) -> Option<DownloadProgressInfo> {
        self.tasks.read().get(&key).map(|t| t.to_progress_info())
    }

    pub fn has_task(&self, key: DownloadTaskKey) -> bool {
        self.tasks.read().contains_key(&key)
    }

    pub fn set_concurrency(&self, concurrency: u32) {
        let c = (concurrency as usize).max(1);
        let old = self.concurrency_degree.swap(c, Ordering::SeqCst);
        if c > old {
            self.semaphore.add_permits(c - old);
        } else if c < old {
            self.semaphore.forget_permits(old - c);
        }
    }
}

impl DownloadManager {
    fn build_client(options: Option<&DownloadNetworkOptions>) -> MahoHttpClient {
        let resolver = DnsResolver::new();
        let mut all_domain_ips: HashMap<String, Vec<String>> = HashMap::new();
        let mut proxy_url = None;

        if let Some(opts) = options {
            if let Some(ref proxy_str) = opts.proxy_url {
                let trimmed = proxy_str.trim();
                if !trimmed.is_empty() {
                    proxy_url = Some(trimmed.to_string());
                }
            }

            for (domain, ips) in &opts.static_domain_ips {
                if !ips.is_empty() {
                    all_domain_ips.insert(domain.clone(), ips.clone());
                }
            }
        } else {
            all_domain_ips.insert(
                "i.pximg.net".to_string(),
                vec![
                    "210.140.139.134".to_string(),
                    "210.140.139.135".to_string(),
                    "210.140.139.136".to_string(),
                    "210.140.139.137".to_string(),
                ],
            );
            all_domain_ips.insert(
                "s.pximg.net".to_string(),
                vec![
                    "210.140.139.134".to_string(),
                    "210.140.139.135".to_string(),
                    "210.140.139.136".to_string(),
                    "210.140.139.137".to_string(),
                ],
            );
        }

        for (domain, ips) in all_domain_ips {
            let parsed: Vec<std::net::IpAddr> = ips.iter().filter_map(|s| s.parse().ok()).collect();
            resolver.set_static_ips(&domain, parsed);
        }

        let config = Arc::new(MahoConfig {
            enabled: true,
            split_delay_ms: 100,
            dns_resolver: resolver,
        });

        MahoHttpClient::new(config, proxy_url)
    }

    async fn handle_error(
        tasks: &Arc<RwLock<HashMap<DownloadTaskKey, DownloadTaskItem>>>,
        cancel_tokens: &Arc<RwLock<HashMap<DownloadTaskKey, CancellationToken>>>,
        key: &DownloadTaskKey,
        temp_dest: &str,
        err_msg: &str,
        callback: Option<&dyn DownloadProgressCallback>,
    ) {
        let _ = fs::remove_file(temp_dest).await;
        {
            let mut lock = tasks.write();
            if let Some(t) = lock.get_mut(key) {
                t.state = DownloadState::Error;
                t.error_message = Some(err_msg.to_string());
            }
        }
        cancel_tokens.write().remove(key);

        if let Some(cb) = callback {
            cb.on_state_changed(key.clone(), DownloadState::Error, Some(err_msg.to_string()));
        }
    }

    pub fn pause_task_ref(&self, key: &DownloadTaskKey) -> bool {
        if let Some(token) = self.cancel_tokens.write().remove(key) {
            token.cancel();
            let mut lock = self.tasks.write();
            if let Some(t) = lock.get_mut(key) {
                t.state = DownloadState::Paused;
                if let Some(cb) = &self.callback {
                    cb.on_state_changed(key.clone(), DownloadState::Paused, None);
                }
                return true;
            }
        }
        false
    }

    pub fn resume_task_ref(&self, key: &DownloadTaskKey) -> bool {
        let task_data = {
            let lock = self.tasks.read();
            lock.get(key).cloned()
        };

        if let Some(task) = task_data {
            if task.state == DownloadState::Paused {
                self.enqueue_task(task.key, task.url, task.destination, task.overwrite);
                return true;
            }
        }
        false
    }

    pub fn cancel_task_ref(&self, key: &DownloadTaskKey) -> bool {
        if let Some(token) = self.cancel_tokens.write().remove(key) {
            token.cancel();
        }

        let mut lock = self.tasks.write();
        if let Some(t) = lock.get_mut(key) {
            t.state = DownloadState::Cancelled;
            let temp = t.temp_destination.clone();
            DOWNLOAD_RUNTIME.spawn(async move {
                let _ = fs::remove_file(&temp).await;
            });
            if let Some(cb) = &self.callback {
                cb.on_state_changed(key.clone(), DownloadState::Cancelled, None);
            }
            return true;
        }
        false
    }

    pub fn reset_task_ref(&self, key: &DownloadTaskKey) -> bool {
        let task_data = {
            let lock = self.tasks.read();
            lock.get(key).cloned()
        };

        if let Some(task) = task_data {
            if task.state.is_terminal() {
                self.enqueue_task(task.key, task.url, task.destination, task.overwrite);
                return true;
            }
        }
        false
    }

    pub fn remove_task_ref(&self, key: &DownloadTaskKey) -> bool {
        self.cancel_task_ref(key);
        self.tasks.write().remove(key).is_some()
    }

    pub fn get_task_state(&self, key: &DownloadTaskKey) -> Option<DownloadState> {
        self.tasks.read().get(key).map(|t| t.state)
    }

    pub fn get_task(&self, key: &DownloadTaskKey) -> Option<DownloadTaskItem> {
        self.tasks.read().get(key).cloned()
    }

    pub fn list_tasks(&self) -> Vec<DownloadTaskItem> {
        self.tasks.read().values().cloned().collect()
    }
}
