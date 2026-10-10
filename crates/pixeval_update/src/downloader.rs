// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::path::{Path, PathBuf};
use std::time::Duration;

use futures_util::StreamExt;
use http::StatusCode;
use pixeval_maho::{MahoHttpClient, MahoResponse};
use sha2::{Digest, Sha256};
use tokio::fs::{File, OpenOptions};
use tokio::io::{AsyncReadExt, AsyncWriteExt};

use crate::engine::{UpdateCancellationToken, UpdateProgressCallback};
use crate::error::UpdateError;

/// 计算文件的 SHA-256 十六进制散列值 (同步模式)
pub fn compute_file_sha256_sync(path: &Path) -> Result<String, UpdateError> {
    use std::fs::File;
    use std::io::Read;

    let mut file = File::open(path).map_err(|e| UpdateError::Io {
        message: format!("Failed to open file for SHA-256 calculation: {e}"),
    })?;

    let mut hasher = Sha256::new();
    let mut buffer = [0u8; 64 * 1024];

    loop {
        let bytes_read = file.read(&mut buffer).map_err(|e| UpdateError::Io {
            message: format!("Failed to read file for SHA-256 calculation: {e}"),
        })?;

        if bytes_read == 0 {
            break;
        }

        hasher.update(&buffer[..bytes_read]);
    }

    Ok(hex::encode(hasher.finalize()))
}

/// 计算文件的 SHA-256 十六进制散列值
pub async fn compute_file_sha256(path: &Path) -> Result<String, UpdateError> {
    let mut file = File::open(path).await.map_err(|e| UpdateError::Io {
        message: format!("Failed to open file for SHA-256 calculation: {e}"),
    })?;

    let mut hasher = Sha256::new();
    let mut buffer = [0u8; 64 * 1024];

    loop {
        let bytes_read = file.read(&mut buffer).await.map_err(|e| UpdateError::Io {
            message: format!("Failed to read file for SHA-256 calculation: {e}"),
        })?;

        if bytes_read == 0 {
            break;
        }

        hasher.update(&buffer[..bytes_read]);
    }

    Ok(hex::encode(hasher.finalize()))
}

pub(crate) const RETRY_DELAYS: [Duration; 2] =
    [Duration::from_millis(100), Duration::from_millis(500)];

/// Velopack 的超时单位是分钟。`None`、非正数和非有限值表示不限时。
pub(crate) fn timeout_from_minutes(minutes: Option<f64>) -> Option<Duration> {
    let minutes = minutes?;
    if !minutes.is_finite() || minutes <= 0.0 {
        return None;
    }

    let seconds = minutes * 60.0;
    if !seconds.is_finite() || seconds > u64::MAX as f64 {
        return None;
    }

    Duration::try_from_secs_f64(seconds).ok()
}

pub(crate) fn is_retryable(err: &UpdateError) -> bool {
    match err {
        UpdateError::Network { .. } | UpdateError::Io { .. } => true,
        UpdateError::Http { status_code, .. } => {
            *status_code == 408 || *status_code == 429 || (500..=599).contains(status_code)
        }
        _ => false,
    }
}

/// 默认请求头可被调用方同名头覆盖。`Range` 由断点续传逻辑在其后单独设置。
pub(crate) fn request_headers(extra: Option<&HashMap<String, String>>) -> Vec<(String, String)> {
    let mut headers = vec![
        ("User-Agent".to_string(), "Pixeval-Updater".to_string()),
        ("Accept".to_string(), "*/*".to_string()),
        ("Connection".to_string(), "close".to_string()),
    ];

    if let Some(extra) = extra {
        for (name, value) in extra {
            if let Some(existing) = headers
                .iter_mut()
                .find(|(header, _)| header.eq_ignore_ascii_case(name))
            {
                existing.1.clone_from(value);
            } else {
                headers.push((name.clone(), value.clone()));
            }
        }
    }

    headers
}

pub(crate) async fn apply_timeout<T>(
    operation: impl Future<Output = Result<T, UpdateError>>,
    timeout_minutes: Option<f64>,
) -> Result<T, UpdateError> {
    let Some(duration) = timeout_from_minutes(timeout_minutes) else {
        return operation.await;
    };

    match tokio::time::timeout(duration, operation).await {
        Ok(result) => result,
        Err(_) => Err(UpdateError::Network {
            message: "Update download timed out".to_string(),
        }),
    }
}

/// 发送支持 HTTP 重定向追踪的 GET 请求
async fn send_with_redirects(
    client: &MahoHttpClient,
    initial_url: &str,
    range_offset: u64,
    extra_headers: Option<&HashMap<String, String>>,
) -> Result<MahoResponse, UpdateError> {
    let mut current_url = initial_url.to_string();
    const MAX_REDIRECTS: usize = 10;

    for _ in 0..MAX_REDIRECTS {
        let mut builder = client.get(&current_url);
        for (name, value) in request_headers(extra_headers) {
            builder = builder.header(name.as_str(), value.as_str());
        }

        if range_offset > 0 {
            builder = builder.header("Range", format!("bytes={range_offset}-"));
        }

        let response = builder.send().await.map_err(UpdateError::from)?;
        let status = response.status();

        if status.is_redirection() {
            if let Some(loc_val) = response.headers().get(http::header::LOCATION) {
                if let Ok(loc_str) = loc_val.to_str() {
                    let base_url =
                        url::Url::parse(&current_url).map_err(|e| UpdateError::Network {
                            message: format!("Invalid URL '{current_url}': {e}"),
                        })?;
                    let next_url = base_url.join(loc_str).map_err(|e| UpdateError::Network {
                        message: format!("Invalid redirect URL '{loc_str}': {e}"),
                    })?;
                    current_url = next_url.to_string();
                    continue;
                }
            }
            return Err(UpdateError::Network {
                message: format!("Redirect status {status} missing Location header"),
            });
        }

        return Ok(response);
    }

    Err(UpdateError::Network {
        message: format!("Too many redirects for URL: {initial_url}"),
    })
}

/// 执行支持断点续传、重定向与 SHA-256 校验的文件下载
pub async fn download_file_resumable(
    client: &MahoHttpClient,
    url: &str,
    destination: &Path,
    expected_sha256: Option<&str>,
    progress_callback: Option<&dyn UpdateProgressCallback>,
    cancel_token: Option<&UpdateCancellationToken>,
    extra_headers: Option<&HashMap<String, String>>,
) -> Result<String, UpdateError> {
    if let Some(parent) = destination.parent() {
        tokio::fs::create_dir_all(parent)
            .await
            .map_err(|e| UpdateError::Io {
                message: format!("Failed to create destination directory: {e}"),
            })?;
    }

    let temp_dest = PathBuf::from(format!("{}.part", destination.display()));

    // 探测既有部分下载文件大小
    let mut existing_len = 0u64;
    if temp_dest.exists() {
        if let Ok(metadata) = tokio::fs::metadata(&temp_dest).await {
            existing_len = metadata.len();
        }
    }

    // 尝试断点续传请求
    let mut response = send_with_redirects(client, url, existing_len, extra_headers).await?;
    let status = response.status();

    let (mut file, mut downloaded_bytes, total_bytes) = match status {
        StatusCode::PARTIAL_CONTENT => {
            // 服务器接受 Range 请求，进入断点追加模式
            let total = response
                .headers()
                .get(http::header::CONTENT_RANGE)
                .and_then(|val| val.to_str().ok())
                .and_then(|val| val.rsplit_once('/'))
                .and_then(|(_, total_str)| total_str.parse::<u64>().ok())
                .unwrap_or_else(|| existing_len + response.content_length().unwrap_or(0));

            let f = OpenOptions::new()
                .create(true)
                .append(true)
                .open(&temp_dest)
                .await
                .map_err(|e| UpdateError::Io {
                    message: format!("Failed to open partial file for append: {e}"),
                })?;

            (f, existing_len, total)
        }
        StatusCode::OK => {
            // 从头开始全量下载
            let total = response.content_length().unwrap_or(0);
            let f = OpenOptions::new()
                .create(true)
                .write(true)
                .truncate(true)
                .open(&temp_dest)
                .await
                .map_err(|e| UpdateError::Io {
                    message: format!("Failed to create temp download file: {e}"),
                })?;

            (f, 0u64, total)
        }
        StatusCode::RANGE_NOT_SATISFIABLE => {
            // 范围不可满足，截断并重新全量请求
            let retry_resp = send_with_redirects(client, url, 0, extra_headers).await?;
            let retry_status = retry_resp.status();
            if !retry_status.is_success() {
                return Err(UpdateError::Http {
                    status_code: retry_status.as_u16(),
                    message: format!("HTTP error on retry: {retry_status}"),
                });
            }

            let total = retry_resp.content_length().unwrap_or(0);
            let f = OpenOptions::new()
                .create(true)
                .write(true)
                .truncate(true)
                .open(&temp_dest)
                .await
                .map_err(|e| UpdateError::Io {
                    message: format!("Failed to recreate temp download file: {e}"),
                })?;

            response = retry_resp;
            (f, 0u64, total)
        }
        other => {
            return Err(UpdateError::Http {
                status_code: other.as_u16(),
                message: format!("HTTP download request failed with status: {other}"),
            });
        }
    };

    if let Some(cb) = progress_callback {
        let pct = if total_bytes > 0 {
            ((downloaded_bytes as f64) / (total_bytes as f64) * 100.0).clamp(0.0, 100.0)
        } else {
            0.0
        };
        cb.on_progress(downloaded_bytes, total_bytes, pct);
    }

    let mut stream = response.bytes_stream();

    while let Some(chunk_res) = stream.next().await {
        if let Some(token) = cancel_token {
            if token.is_cancelled() {
                return Err(UpdateError::Cancelled);
            }
        }

        let chunk = chunk_res.map_err(UpdateError::from)?;
        file.write_all(&chunk).await.map_err(|e| UpdateError::Io {
            message: format!("Failed to write chunk to file: {e}"),
        })?;

        downloaded_bytes += chunk.len() as u64;

        if let Some(cb) = progress_callback {
            let pct = if total_bytes > 0 {
                ((downloaded_bytes as f64) / (total_bytes as f64) * 100.0).clamp(0.0, 100.0)
            } else {
                0.0
            };
            cb.on_progress(downloaded_bytes, total_bytes, pct);
        }
    }

    file.flush().await.map_err(|e| UpdateError::Io {
        message: format!("Failed to flush downloaded file: {e}"),
    })?;
    drop(file);

    // 校验 SHA-256 散列值
    let actual_sha256 = compute_file_sha256(&temp_dest).await?;

    if let Some(expected) = expected_sha256 {
        let expected_clean = expected.trim();
        if !expected_clean.is_empty() && !actual_sha256.eq_ignore_ascii_case(expected_clean) {
            return Err(UpdateError::ChecksumMismatch {
                expected: expected_clean.to_string(),
                actual: actual_sha256,
            });
        }
    }

    // 校验通过，原子重命名替换
    if destination.exists() {
        let _ = tokio::fs::remove_file(destination).await;
    }
    tokio::fs::rename(&temp_dest, destination)
        .await
        .map_err(|e| UpdateError::Io {
            message: format!("Failed to atomically rename part file to destination: {e}"),
        })?;

    Ok(actual_sha256)
}

pub(crate) async fn download_bytes(
    client: &MahoHttpClient,
    url: &str,
    extra_headers: Option<&HashMap<String, String>>,
) -> Result<Vec<u8>, UpdateError> {
    let mut attempt = 0;
    loop {
        match download_bytes_once(client, url, extra_headers).await {
            Ok(value) => return Ok(value),
            Err(err) if attempt < RETRY_DELAYS.len() && is_retryable(&err) => {
                tokio::time::sleep(RETRY_DELAYS[attempt]).await;
                attempt += 1;
            }
            Err(err) => return Err(err),
        }
    }
}

async fn download_bytes_once(
    client: &MahoHttpClient,
    url: &str,
    extra_headers: Option<&HashMap<String, String>>,
) -> Result<Vec<u8>, UpdateError> {
    let response = send_with_redirects(client, url, 0, extra_headers).await?;
    let status = response.status();
    if !status.is_success() {
        return Err(UpdateError::Http {
            status_code: status.as_u16(),
            message: format!("HTTP download failed with status: {status}"),
        });
    }

    let bytes = response.bytes().await.map_err(UpdateError::from)?;
    Ok(bytes.to_vec())
}

pub(crate) async fn download_text(
    client: &MahoHttpClient,
    url: &str,
    extra_headers: Option<&HashMap<String, String>>,
) -> Result<String, UpdateError> {
    let bytes = download_bytes(client, url, extra_headers).await?;
    String::from_utf8(bytes).map_err(|err| UpdateError::Io {
        message: format!("Update response is not valid UTF-8: {err}"),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn get_test_temp_dir() -> tempfile::TempDir {
        let repo_tmp =
            std::path::PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../../target/tmp");
        let _ = std::fs::create_dir_all(&repo_tmp);
        if repo_tmp.exists() {
            tempfile::Builder::new()
                .prefix("test_")
                .tempdir_in(repo_tmp)
                .unwrap()
        } else {
            tempfile::Builder::new().prefix("test_").tempdir().unwrap()
        }
    }

    #[tokio::test]
    async fn test_compute_file_sha256() {
        let temp_dir = get_test_temp_dir();
        let file_path = temp_dir.path().join("test.txt");

        tokio::fs::write(&file_path, b"hello pixeval updater")
            .await
            .unwrap();

        let hash = compute_file_sha256(&file_path).await.unwrap();
        // SHA-256 of "hello pixeval updater":
        // echo -n "hello pixeval updater" | sha256sum -> d99659f71c4c952b610cff2d87e5bba0f98365dcffab25f6966606a461e7a0e3
        let mut hasher = Sha256::new();
        hasher.update(b"hello pixeval updater");
        let expected = hex::encode(hasher.finalize());

        assert_eq!(hash, expected);
    }

    #[test]
    fn timeout_from_minutes_treats_non_positive_and_non_finite_as_unlimited() {
        assert!(timeout_from_minutes(None).is_none());
        assert!(timeout_from_minutes(Some(0.0)).is_none());
        assert!(timeout_from_minutes(Some(-1.0)).is_none());
        assert!(timeout_from_minutes(Some(f64::INFINITY)).is_none());
        assert!(timeout_from_minutes(Some(f64::NAN)).is_none());
        assert_eq!(
            timeout_from_minutes(Some(1.0)),
            Some(Duration::from_secs(60))
        );
        assert_eq!(
            timeout_from_minutes(Some(0.5)),
            Some(Duration::from_secs(30))
        );
    }

    #[test]
    fn retryable_errors_match_transient_failures() {
        assert!(is_retryable(&UpdateError::Network {
            message: "connection reset".to_string(),
        }));
        assert!(is_retryable(&UpdateError::Io {
            message: "socket reset".to_string(),
        }));
        assert!(is_retryable(&UpdateError::Http {
            status_code: 408,
            message: "timeout".to_string(),
        }));
        assert!(is_retryable(&UpdateError::Http {
            status_code: 429,
            message: "rate limit".to_string(),
        }));
        assert!(is_retryable(&UpdateError::Http {
            status_code: 503,
            message: "unavailable".to_string(),
        }));
        assert!(!is_retryable(&UpdateError::Http {
            status_code: 404,
            message: "missing".to_string(),
        }));
        assert!(!is_retryable(&UpdateError::Cancelled));
        assert!(!is_retryable(&UpdateError::ChecksumMismatch {
            expected: "a".to_string(),
            actual: "b".to_string(),
        }));
    }

    #[test]
    fn request_headers_let_caller_override_defaults() {
        let mut extra = HashMap::new();
        extra.insert(
            "Accept".to_string(),
            "application/vnd.github.v3+json".to_string(),
        );
        extra.insert("Authorization".to_string(), "Bearer token".to_string());

        let headers = request_headers(Some(&extra));
        assert_eq!(
            headers
                .iter()
                .find(|(name, _)| name == "Accept")
                .map(|(_, value)| value.as_str()),
            Some("application/vnd.github.v3+json")
        );
        assert_eq!(
            headers
                .iter()
                .find(|(name, _)| name == "User-Agent")
                .map(|(_, value)| value.as_str()),
            Some("Pixeval-Updater")
        );
        assert_eq!(
            headers
                .iter()
                .find(|(name, _)| name == "Connection")
                .map(|(_, value)| value.as_str()),
            Some("close")
        );
        assert_eq!(
            headers
                .iter()
                .find(|(name, _)| name == "Authorization")
                .map(|(_, value)| value.as_str()),
            Some("Bearer token")
        );
    }
}
