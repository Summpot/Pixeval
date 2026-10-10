// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::path::Path;

use futures_util::StreamExt;
use pixeval_maho::MahoHttpClient;
use tokio::fs::{self, File};
use tokio::io::AsyncWriteExt;
use tokio::sync::Semaphore;
use tokio_util::sync::CancellationToken;

const MAX_RETRIES: u32 = 3;

#[derive(Debug)]
pub enum TransferOutcome {
    Completed { skipped: bool },
    Cancelled,
    Failed(String),
}

pub async fn download_file(
    client: &MahoHttpClient,
    semaphore: &Semaphore,
    token: &CancellationToken,
    url: &str,
    destination: &str,
    overwrite: bool,
    mut on_progress: impl FnMut(f64),
) -> TransferOutcome {
    let permit = match semaphore.acquire().await {
        Ok(permit) => permit,
        Err(_) => return TransferOutcome::Cancelled,
    };

    if token.is_cancelled() {
        drop(permit);
        return TransferOutcome::Cancelled;
    }

    if !url.starts_with("http://") && !url.starts_with("https://") {
        on_progress(100.0);
        return TransferOutcome::Completed { skipped: true };
    }

    if Path::new(destination).exists() && !overwrite {
        on_progress(100.0);
        return TransferOutcome::Completed { skipped: true };
    }

    let temp_dest = format!("{destination}.pixevaldownloading");
    if let Some(parent) = Path::new(&temp_dest).parent() {
        if let Err(err) = fs::create_dir_all(parent).await {
            return TransferOutcome::Failed(format!("Create directory error: {err}"));
        }
    }

    let mut last_error: Option<String> = None;
    for attempt in 1..=MAX_RETRIES {
        if token.is_cancelled() {
            let _ = fs::remove_file(&temp_dest).await;
            return TransferOutcome::Cancelled;
        }
        if attempt > 1 {
            let backoff_ms = 500 * (1 << (attempt - 2));
            tokio::select! {
                _ = tokio::time::sleep(std::time::Duration::from_millis(backoff_ms)) => {}
                _ = token.cancelled() => {
                    let _ = fs::remove_file(&temp_dest).await;
                    return TransferOutcome::Cancelled;
                }
            }
        }

        let _ = fs::remove_file(&temp_dest).await;
        let send_future = client
            .get(url)
            .header("Referer", "https://app-api.pixiv.net/")
            .header("User-Agent", "PixivAndroidApp/6.199.0 (Android 15.0; Pixel 8)")
            .send();
        let response = tokio::select! {
            result = send_future => result,
            _ = token.cancelled() => {
                let _ = fs::remove_file(&temp_dest).await;
                return TransferOutcome::Cancelled;
            }
        };

        let response = match response {
            Ok(response) if response.status().is_success() => response,
            Ok(response) => {
                let status = response.status();
                let message = format!("HTTP error: {status}");
                if status == http::StatusCode::NOT_FOUND || status.is_client_error() {
                    let _ = fs::remove_file(&temp_dest).await;
                    return TransferOutcome::Failed(message);
                }
                last_error = Some(message);
                continue;
            }
            Err(err) => {
                last_error = Some(format!("Network error: {err}"));
                continue;
            }
        };

        let total_bytes = response.content_length().unwrap_or(0);
        let mut file = match File::create(&temp_dest).await {
            Ok(file) => file,
            Err(err) => return TransferOutcome::Failed(format!("File create error: {err}")),
        };
        let mut downloaded = 0u64;
        let mut stream = response.bytes_stream();
        let mut stream_failed = false;
        loop {
            let chunk = tokio::select! {
                next = stream.next() => next,
                _ = token.cancelled() => {
                    drop(file);
                    let _ = fs::remove_file(&temp_dest).await;
                    return TransferOutcome::Cancelled;
                }
            };
            match chunk {
                None => break,
                Some(Ok(bytes)) => {
                    if let Err(err) = file.write_all(&bytes).await {
                        let _ = fs::remove_file(&temp_dest).await;
                        return TransferOutcome::Failed(format!("Write error: {err}"));
                    }
                    downloaded += bytes.len() as u64;
                    let percentage = if total_bytes > 0 {
                        ((downloaded as f64 / total_bytes as f64) * 100.0).min(100.0)
                    } else {
                        0.0
                    };
                    on_progress(percentage);
                }
                Some(Err(err)) => {
                    last_error = Some(format!("Stream read error: {err}"));
                    stream_failed = true;
                    break;
                }
            }
        }
        if stream_failed {
            continue;
        }
        if let Err(err) = file.flush().await {
            last_error = Some(format!("Flush error: {err}"));
            continue;
        }
        drop(file);

        if Path::new(destination).exists() {
            if !overwrite {
                let _ = fs::remove_file(&temp_dest).await;
                on_progress(100.0);
                return TransferOutcome::Completed { skipped: true };
            }
            let _ = fs::remove_file(destination).await;
        }
        if let Err(err) = fs::rename(&temp_dest, destination).await {
            let _ = fs::remove_file(&temp_dest).await;
            return TransferOutcome::Failed(format!("Rename error: {err}"));
        }
        on_progress(100.0);
        return TransferOutcome::Completed { skipped: false };
    }

    let _ = fs::remove_file(&temp_dest).await;
    TransferOutcome::Failed(last_error.unwrap_or_else(|| "Download failed after multiple attempts".to_string()))
}

pub fn commit_file(temporary: &str, destination: &str, overwrite: bool) -> Result<(), String> {
    if let Some(parent) = Path::new(destination).parent() {
        std::fs::create_dir_all(parent).map_err(|err| format!("Create directory error: {err}"))?;
    }
    if Path::new(destination).exists() {
        if !overwrite {
            let _ = std::fs::remove_file(temporary);
            return Ok(());
        }
        let _ = std::fs::remove_file(destination);
    }
    std::fs::rename(temporary, destination).map_err(|err| format!("Rename error: {err}"))
}

pub fn remove_path(path: &str) {
    let target = Path::new(path);
    if target.is_dir() {
        let _ = std::fs::remove_dir_all(target);
    } else if target.exists() {
        let _ = std::fs::remove_file(target);
    }
    let temp = format!("{path}.pixevaldownloading");
    let _ = std::fs::remove_file(temp);
}

#[cfg(test)]
mod tests {
    use super::commit_file;

    #[test]
    fn commit_keeps_existing_destination_until_overwrite() {
        let dir = std::env::temp_dir().join(format!("pixeval-commit-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&dir);
        std::fs::create_dir_all(&dir).unwrap();
        let destination = dir.join("destination.txt");
        let temporary = dir.join("download.tmp");
        std::fs::write(&destination, "old").unwrap();
        std::fs::write(&temporary, "new").unwrap();

        commit_file(temporary.to_str().unwrap(), destination.to_str().unwrap(), false).unwrap();
        assert_eq!(std::fs::read_to_string(&destination).unwrap(), "old");
        assert!(!temporary.exists());

        std::fs::write(&temporary, "new").unwrap();
        commit_file(temporary.to_str().unwrap(), destination.to_str().unwrap(), true).unwrap();
        assert_eq!(std::fs::read_to_string(&destination).unwrap(), "new");
        assert!(!temporary.exists());
        let _ = std::fs::remove_dir_all(&dir);
    }
}
