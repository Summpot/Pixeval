// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::path::Path;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};

use pixeval_maho::{DnsResolver, MahoConfig, MahoHttpClient};

use crate::downloader::{
    RETRY_DELAYS, apply_timeout, compute_file_sha256_sync, download_bytes, download_file_resumable,
    download_text, is_retryable,
};
use crate::error::UpdateError;
use crate::github::{convert_and_sort_releases, fetch_github_releases, fetch_release_checksums};
use crate::models::{
    AppRelease, RawGitHubRelease, ReleaseAsset, UpdateCheckResult, UpdateNetworkOptions,
};
use crate::version::{UpdateState, compare_version_strings};

/// 下载进度回调接口 (UniFFI 导出)
#[uniffi::export(callback_interface)]
pub trait UpdateProgressCallback: Send + Sync {
    fn on_progress(&self, downloaded_bytes: u64, total_bytes: u64, percentage: f64);
}

/// 更新操作取消令牌
#[derive(Default, uniffi::Object)]
pub struct UpdateCancellationToken {
    cancelled: AtomicBool,
}

#[uniffi::export]
impl UpdateCancellationToken {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Arc::new(Self {
            cancelled: AtomicBool::new(false),
        })
    }

    pub fn cancel(&self) {
        self.cancelled.store(true, Ordering::Relaxed);
    }

    pub fn is_cancelled(&self) -> bool {
        self.cancelled.load(Ordering::Relaxed)
    }
}

/// 原生应用更新引擎
#[derive(uniffi::Object)]
pub struct UpdateEngine {
    repo_owner: String,
    repo_name: String,
    client: MahoHttpClient,
}

impl UpdateEngine {
    fn build_client(options: Option<&UpdateNetworkOptions>) -> MahoHttpClient {
        let enable_domain_fronting = options.map(|o| o.enable_domain_fronting).unwrap_or(true);
        let proxy_url = options.and_then(|o| o.proxy_url.clone());

        let resolver = DnsResolver::new();
        if enable_domain_fronting {
            // 设置标准默认 GitHub 域前置解析 IP
            resolver.set_static_ips(
                "github.com",
                vec![
                    "20.205.243.166".parse().unwrap(),
                    "140.82.112.3".parse().unwrap(),
                    "140.82.113.3".parse().unwrap(),
                    "140.82.114.3".parse().unwrap(),
                    "140.82.121.3".parse().unwrap(),
                ],
            );
            resolver.set_static_ips(
                "api.github.com",
                vec![
                    "20.205.243.168".parse().unwrap(),
                    "140.82.112.5".parse().unwrap(),
                    "140.82.113.5".parse().unwrap(),
                    "140.82.114.6".parse().unwrap(),
                    "140.82.121.5".parse().unwrap(),
                ],
            );
            resolver.set_static_ips(
                "avatars.githubusercontent.com",
                vec![
                    "185.199.108.133".parse().unwrap(),
                    "185.199.109.133".parse().unwrap(),
                    "185.199.110.133".parse().unwrap(),
                    "185.199.111.133".parse().unwrap(),
                ],
            );
            resolver.set_static_ips(
                "objects.githubusercontent.com",
                vec![
                    "185.199.108.133".parse().unwrap(),
                    "185.199.109.133".parse().unwrap(),
                    "185.199.110.133".parse().unwrap(),
                    "185.199.111.133".parse().unwrap(),
                ],
            );
            resolver.set_static_ips(
                "github-releases.githubusercontent.com",
                vec![
                    "185.199.108.133".parse().unwrap(),
                    "185.199.109.133".parse().unwrap(),
                    "185.199.110.133".parse().unwrap(),
                    "185.199.111.133".parse().unwrap(),
                ],
            );
            resolver.set_static_ips(
                "raw.githubusercontent.com",
                vec![
                    "185.199.108.133".parse().unwrap(),
                    "185.199.109.133".parse().unwrap(),
                    "185.199.110.133".parse().unwrap(),
                    "185.199.111.133".parse().unwrap(),
                ],
            );
            resolver.set_static_ips(
                "github.githubassets.com",
                vec![
                    "185.199.108.215".parse().unwrap(),
                    "185.199.109.215".parse().unwrap(),
                    "185.199.110.215".parse().unwrap(),
                    "185.199.111.215".parse().unwrap(),
                ],
            );
            resolver.set_static_ips(
                "codeload.github.com",
                vec![
                    "20.205.243.165".parse().unwrap(),
                    "140.82.112.9".parse().unwrap(),
                    "140.82.113.10".parse().unwrap(),
                    "140.82.114.10".parse().unwrap(),
                    "140.82.121.10".parse().unwrap(),
                ],
            );

            // 合并自定义外部 DNS 映射
            if let Some(opts) = options {
                for (host, ips) in &opts.custom_dns_mappings {
                    let parsed: Vec<_> = ips.iter().filter_map(|s| s.parse().ok()).collect();
                    if !parsed.is_empty() {
                        resolver.set_static_ips(host, parsed);
                    }
                }
            }
        }

        let config = Arc::new(MahoConfig {
            enabled: enable_domain_fronting,
            split_delay_ms: 100,
            dns_resolver: resolver,
        });

        MahoHttpClient::new(config, proxy_url)
    }
}

#[uniffi::export]
impl UpdateEngine {
    #[uniffi::constructor]
    pub fn new(options: Option<UpdateNetworkOptions>) -> Arc<Self> {
        Self::new_with_repo("Pixeval".to_string(), "Pixeval".to_string(), options)
    }

    #[uniffi::constructor]
    pub fn new_with_repo(
        owner: String,
        repo: String,
        options: Option<UpdateNetworkOptions>,
    ) -> Arc<Self> {
        let client = Self::build_client(options.as_ref());
        Arc::new(Self {
            repo_owner: owner,
            repo_name: repo,
            client,
        })
    }

    /// 检查更新
    pub async fn check_for_updates(
        &self,
        current_version: String,
        include_prereleases: bool,
    ) -> Result<UpdateCheckResult, UpdateError> {
        let raw_releases = fetch_github_releases(
            &self.client,
            &self.repo_owner,
            &self.repo_name,
            include_prereleases,
        )
        .await?;

        if raw_releases.is_empty() {
            return Ok(UpdateCheckResult {
                current_version,
                update_state: UpdateState::Unknown,
                latest_release: None,
                all_releases: vec![],
            });
        }

        // 仅抓取最新 Release 的校验和
        let latest_checksums = fetch_release_checksums(&self.client, &raw_releases[0]).await;
        let all_releases = convert_and_sort_releases(raw_releases, Some(&latest_checksums));

        let latest_release = all_releases.first().cloned();
        let update_state = match &latest_release {
            Some(latest) => compare_version_strings(&current_version, &latest.version),
            None => UpdateState::Unknown,
        };

        Ok(UpdateCheckResult {
            current_version,
            update_state,
            latest_release,
            all_releases,
        })
    }

    /// 获取所有发布列表
    pub async fn get_releases(
        &self,
        include_prereleases: bool,
    ) -> Result<Vec<AppRelease>, UpdateError> {
        let raw_releases = fetch_github_releases(
            &self.client,
            &self.repo_owner,
            &self.repo_name,
            include_prereleases,
        )
        .await?;

        if raw_releases.is_empty() {
            return Ok(vec![]);
        }

        let latest_checksums = fetch_release_checksums(&self.client, &raw_releases[0]).await;
        Ok(convert_and_sort_releases(
            raw_releases,
            Some(&latest_checksums),
        ))
    }

    /// 根据 Tag 获取特定发布的更新日志 Markdown
    pub async fn fetch_release_notes(&self, tag_name: String) -> Result<String, UpdateError> {
        let url = format!(
            "https://api.github.com/repos/{}/{}/releases/tags/{}",
            self.repo_owner, self.repo_name, tag_name
        );

        let response = self
            .client
            .get(&url)
            .header("Accept", "application/vnd.github+json")
            .header("User-Agent", "Pixeval-Updater")
            .send()
            .await
            .map_err(UpdateError::from)?;

        let status = response.status();
        if !status.is_success() {
            return Err(UpdateError::Http {
                status_code: status.as_u16(),
                message: format!("Failed to fetch release for tag '{tag_name}': {status}"),
            });
        }

        let release: RawGitHubRelease = response.json().await.map_err(UpdateError::from)?;
        Ok(release.body.unwrap_or_default())
    }

    /// 下载发布资产工件
    pub async fn download_asset(
        &self,
        asset: ReleaseAsset,
        destination_path: String,
        progress_callback: Option<Box<dyn UpdateProgressCallback>>,
        cancel_token: Option<Arc<UpdateCancellationToken>>,
    ) -> Result<String, UpdateError> {
        download_file_resumable(
            &self.client,
            &asset.download_url,
            Path::new(&destination_path),
            asset.sha256.as_deref(),
            progress_callback.as_deref(),
            cancel_token.as_deref(),
            None,
        )
        .await
    }

    /// 下载任意文件并可选校验 SHA-256。
    /// `timeout_minutes` 与 Velopack 一致，单位是分钟；空值或非正数表示不限时。
    pub async fn download_file(
        &self,
        url: String,
        destination_path: String,
        expected_sha256: Option<String>,
        progress_callback: Option<Box<dyn UpdateProgressCallback>>,
        cancel_token: Option<Arc<UpdateCancellationToken>>,
        headers: Option<HashMap<String, String>>,
        timeout_minutes: Option<f64>,
    ) -> Result<String, UpdateError> {
        apply_timeout(
            async {
                let mut attempt = 0usize;
                loop {
                    match download_file_resumable(
                        &self.client,
                        &url,
                        Path::new(&destination_path),
                        expected_sha256.as_deref(),
                        progress_callback.as_deref(),
                        cancel_token.as_deref(),
                        headers.as_ref(),
                    )
                    .await
                    {
                        Ok(value) => return Ok(value),
                        Err(err) if attempt < RETRY_DELAYS.len() && is_retryable(&err) => {
                            tokio::time::sleep(RETRY_DELAYS[attempt]).await;
                            attempt += 1;
                        }
                        Err(err) => return Err(err),
                    }
                }
            },
            timeout_minutes,
        )
        .await
    }

    /// 把更新资源下载到内存。
    pub async fn download_bytes(
        &self,
        url: String,
        headers: Option<HashMap<String, String>>,
        timeout_minutes: Option<f64>,
    ) -> Result<Vec<u8>, UpdateError> {
        apply_timeout(
            download_bytes(&self.client, &url, headers.as_ref()),
            timeout_minutes,
        )
        .await
    }

    /// 把更新资源下载为 UTF-8 文本。
    pub async fn download_text(
        &self,
        url: String,
        headers: Option<HashMap<String, String>>,
        timeout_minutes: Option<f64>,
    ) -> Result<String, UpdateError> {
        apply_timeout(
            download_text(&self.client, &url, headers.as_ref()),
            timeout_minutes,
        )
        .await
    }

    /// 计算指定文件的 SHA-256 十六进制值
    pub fn compute_file_sha256(&self, file_path: String) -> Result<String, UpdateError> {
        compute_file_sha256_sync(Path::new(&file_path))
    }

    /// 校验指定文件的 SHA-256
    pub fn verify_file_sha256(
        &self,
        file_path: String,
        expected_sha256: String,
    ) -> Result<bool, UpdateError> {
        let actual = compute_file_sha256_sync(Path::new(&file_path))?;
        Ok(actual.eq_ignore_ascii_case(expected_sha256.trim()))
    }

    /// 比较两个版本
    pub fn compare_versions(&self, current: String, remote: String) -> UpdateState {
        compare_version_strings(&current, &remote)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::downloader::compute_file_sha256;

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

    #[test]
    fn test_cancellation_token() {
        let token = UpdateCancellationToken::new();
        assert!(!token.is_cancelled());
        token.cancel();
        assert!(token.is_cancelled());
    }

    #[test]
    fn test_compare_versions_via_engine() {
        let engine = UpdateEngine::new(None);
        assert_eq!(
            engine.compare_versions("5.0.13.0".to_string(), "5.0.14".to_string()),
            UpdateState::BuildUpdate
        );
        assert_eq!(
            engine.compare_versions("5.0.13".to_string(), "5.0.13".to_string()),
            UpdateState::UpToDate
        );
        assert_eq!(
            engine.compare_versions("5.0.13".to_string(), "6.0.0".to_string()),
            UpdateState::MajorUpdate
        );
    }

    #[tokio::test]
    async fn test_verify_file_sha256() {
        let temp_dir = get_test_temp_dir();
        let file_path = temp_dir.path().join("verify.txt");
        tokio::fs::write(&file_path, b"test content for sha256")
            .await
            .unwrap();

        let engine = UpdateEngine::new(None);
        // echo -n "test content for sha256" | sha256sum -> 20689b7fae1aa14cfc9a41926c483a912aaad7ea4b971c26b5278c775a22830a
        let sha256 = compute_file_sha256(&file_path).await.unwrap();

        assert!(
            engine
                .verify_file_sha256(file_path.to_str().unwrap().to_string(), sha256.clone())
                .unwrap()
        );
        assert!(
            !engine
                .verify_file_sha256(
                    file_path.to_str().unwrap().to_string(),
                    "0000000000000000000000000000000000000000000000000000000000000000".to_string()
                )
                .unwrap()
        );
    }
}
