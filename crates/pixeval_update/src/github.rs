// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use pixeval_maho::MahoHttpClient;

use crate::error::UpdateError;
use crate::models::{AppRelease, RawGitHubRelease, ReleaseAsset};
use crate::version::parse_normalized_version;

/// 解析 SHA256SUMS.txt 内容
/// 格式为: `<hex_hash>  <filename>` 或 `<hex_hash> *<filename>`
pub fn parse_sha256sums(content: &str) -> HashMap<String, String> {
    let mut map = HashMap::new();
    for line in content.lines() {
        let line = line.trim();
        if line.is_empty() || line.starts_with('#') {
            continue;
        }

        // 分割 hash 和 filename
        let mut parts = line.split_whitespace();
        if let (Some(hash), Some(filename)) = (parts.next(), parts.next()) {
            let filename = filename.strip_prefix('*').unwrap_or(filename);
            // 确保文件名去除了路径前缀
            let clean_name = filename.rsplit(['/', '\\']).next().unwrap_or(filename);
            map.insert(clean_name.to_string(), hash.to_lowercase());
        }
    }
    map
}

/// 从 GitHub 获取发布列表
pub(crate) async fn fetch_github_releases(
    client: &MahoHttpClient,
    owner: &str,
    repo: &str,
    include_prereleases: bool,
) -> Result<Vec<RawGitHubRelease>, UpdateError> {
    let url = format!("https://api.github.com/repos/{owner}/{repo}/releases");

    let response = client
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
            message: format!("Failed to fetch releases from GitHub: {status}"),
        });
    }

    let releases: Vec<RawGitHubRelease> = response.json().await.map_err(UpdateError::from)?;

    // 过滤 draft 和 prerelease
    let filtered = releases
        .into_iter()
        .filter(|r| !r.draft && (include_prereleases || !r.prerelease))
        .collect();

    Ok(filtered)
}

/// 抓取 release 中的 SHA256SUMS.txt (若存在)
pub(crate) async fn fetch_release_checksums(
    client: &MahoHttpClient,
    release: &RawGitHubRelease,
) -> HashMap<String, String> {
    if let Some(checksum_asset) = release
        .assets
        .iter()
        .find(|a| a.name.eq_ignore_ascii_case("SHA256SUMS.txt") || a.name.ends_with(".sha256"))
    {
        if let Ok(resp) = client
            .get(&checksum_asset.browser_download_url)
            .header("User-Agent", "Pixeval-Updater")
            .send()
            .await
        {
            if resp.status().is_success() {
                if let Ok(text) = resp.text().await {
                    return parse_sha256sums(&text);
                }
            }
        }
    }
    HashMap::new()
}

/// 将 RawGitHubRelease 转换为导出的 AppRelease，并按 semver 降序排序
pub(crate) fn convert_and_sort_releases(
    raw_releases: Vec<RawGitHubRelease>,
    latest_checksums: Option<&HashMap<String, String>>,
) -> Vec<AppRelease> {
    let mut releases: Vec<(semver::Version, AppRelease)> = raw_releases
        .into_iter()
        .enumerate()
        .filter_map(|(idx, raw)| {
            let ver = parse_normalized_version(&raw.tag_name).ok()?;
            let checksum_map = if idx == 0 {
                latest_checksums
            } else {
                None
            };

            let assets = raw
                .assets
                .into_iter()
                .map(|a| {
                    let sha256 = checksum_map
                        .and_then(|map| map.get(&a.name))
                        .cloned();

                    ReleaseAsset {
                        name: a.name,
                        download_url: a.browser_download_url,
                        size: a.size,
                        content_type: a.content_type.unwrap_or_else(|| "application/octet-stream".to_string()),
                        sha256,
                    }
                })
                .collect();

            let title = raw.name.unwrap_or_else(|| raw.tag_name.clone());
            let app_release = AppRelease {
                version: ver.to_string(),
                tag_name: raw.tag_name,
                title,
                release_notes: raw.body.unwrap_or_default(),
                published_at: raw.published_at,
                html_url: raw.html_url,
                is_prerelease: raw.prerelease,
                assets,
            };

            Some((ver, app_release))
        })
        .collect();

    // 按版本从高到低排序 (降序)
    releases.sort_by(|(v1, _), (v2, _)| v2.cmp(v1));

    releases.into_iter().map(|(_, rel)| rel).collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_sha256sums() {
        let text = r#"
# Checksums for Pixeval 5.0.13
730d07528e5730d07528e5730d07528e5730d07528e5730d07528e5730d07528  Pixeval-5.0.13-win-x64-Setup.exe
4a89bc528e5730d07528e5730d07528e5730d07528e5730d07528e5730d07528 *artifacts/release/Pixeval-5.0.13-linux-x64.tar.gz
"#;
        let sums = parse_sha256sums(text);
        assert_eq!(
            sums.get("Pixeval-5.0.13-win-x64-Setup.exe"),
            Some(&"730d07528e5730d07528e5730d07528e5730d07528e5730d07528e5730d07528".to_string())
        );
        assert_eq!(
            sums.get("Pixeval-5.0.13-linux-x64.tar.gz"),
            Some(&"4a89bc528e5730d07528e5730d07528e5730d07528e5730d07528e5730d07528".to_string())
        );
    }

    #[test]
    fn test_convert_and_sort_releases() {
        let raw = vec![
            RawGitHubRelease {
                tag_name: "5.0.12".to_string(),
                name: Some("v5.0.12".to_string()),
                body: Some("Old note".to_string()),
                draft: false,
                prerelease: false,
                published_at: None,
                html_url: "https://github.com/Pixeval/Pixeval/releases/tag/5.0.12".to_string(),
                assets: vec![],
            },
            RawGitHubRelease {
                tag_name: "v5.0.13".to_string(),
                name: Some("v5.0.13".to_string()),
                body: Some("New note".to_string()),
                draft: false,
                prerelease: false,
                published_at: None,
                html_url: "https://github.com/Pixeval/Pixeval/releases/tag/5.0.13".to_string(),
                assets: vec![],
            },
        ];

        let converted = convert_and_sort_releases(raw, None);
        assert_eq!(converted.len(), 2);
        assert_eq!(converted[0].version, "5.0.13");
        assert_eq!(converted[1].version, "5.0.12");
    }
}
