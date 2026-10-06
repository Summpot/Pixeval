// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::error::UpdateError;

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum UpdateState {
    /// 当前版本已是最新
    UpToDate,
    /// 主版本更新 (Major)
    MajorUpdate,
    /// 次版本更新 (Minor)
    MinorUpdate,
    /// 补丁/生成更新 (Build/Patch)
    BuildUpdate,
    /// 测试/内测版本 (比远程最新发布更新)
    Insider,
    /// 无法判定/未知状态
    Unknown,
}

/// 将各种格式的版本字符串规范化为标准 SemVer
/// 支持去除 "v"/"V" 前缀、4段式版本 (e.g. 5.0.13.0) 归一化等。
pub fn parse_normalized_version(raw: &str) -> Result<semver::Version, UpdateError> {
    let trimmed = raw.trim();
    if trimmed.is_empty() {
        return Err(UpdateError::VersionParse {
            message: "Version string is empty".to_string(),
        });
    }

    let without_prefix = trimmed.strip_prefix(['v', 'V']).unwrap_or(trimmed);

    // 尝试直接作为 SemVer 解析
    if let Ok(ver) = semver::Version::parse(without_prefix) {
        return Ok(ver);
    }

    // 处理带 build metadata 或 prerelease 的 4 段式版本，如 "5.0.13.0+git123"
    let (core, extra) = if let Some((c, m)) = without_prefix.split_once('+') {
        (c, Some(format!("+{m}")))
    } else if let Some((c, p)) = without_prefix.split_once('-') {
        (c, Some(format!("-{p}")))
    } else {
        (without_prefix, None)
    };

    let parts: Vec<&str> = core.split('.').collect();
    let normalized_core = match parts.as_slice() {
        [major, minor] => format!("{major}.{minor}.0"),
        [major, minor, patch] => format!("{major}.{minor}.{patch}"),
        [major, minor, patch, rev] => {
            // 4 段式版本 (System.Version 格式如 5.0.13.0)
            if let Ok(rev_num) = rev.parse::<u64>() {
                if rev_num == 0 {
                    format!("{major}.{minor}.{patch}")
                } else {
                    format!("{major}.{minor}.{patch}-rev.{rev_num}")
                }
            } else {
                format!("{major}.{minor}.{patch}")
            }
        }
        _ => {
            return Err(UpdateError::VersionParse {
                message: format!("Unsupported version format: '{raw}'"),
            });
        }
    };

    let candidate = match extra {
        Some(ext) => format!("{normalized_core}{ext}"),
        None => normalized_core,
    };

    semver::Version::parse(&candidate).map_err(|e| UpdateError::VersionParse {
        message: format!("Failed to parse normalized version '{candidate}': {e}"),
    })
}

/// 比较当前版本与目标更新版本的状态
pub fn compare_update_state(
    current: &semver::Version,
    remote: &semver::Version,
) -> UpdateState {
    if current > remote {
        return UpdateState::Insider;
    }
    if current == remote {
        return UpdateState::UpToDate;
    }

    if remote.major != current.major {
        return if remote.major > current.major {
            UpdateState::MajorUpdate
        } else {
            UpdateState::Insider
        };
    }

    if remote.minor != current.minor {
        return if remote.minor > current.minor {
            UpdateState::MinorUpdate
        } else {
            UpdateState::Insider
        };
    }

    if remote.patch != current.patch {
        return if remote.patch > current.patch {
            UpdateState::BuildUpdate
        } else {
            UpdateState::Insider
        };
    }

    // 仅 pre-release 或 revision 差异，Pixeval 不单独发布 revision，归为次要更新
    UpdateState::MinorUpdate
}

/// 比较版本字符串
pub fn compare_version_strings(current: &str, remote: &str) -> UpdateState {
    let current_ver = match parse_normalized_version(current) {
        Ok(v) => v,
        Err(_) => return UpdateState::Unknown,
    };
    let remote_ver = match parse_normalized_version(remote) {
        Ok(v) => v,
        Err(_) => return UpdateState::Unknown,
    };

    compare_update_state(&current_ver, &remote_ver)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_version_normalization() {
        assert_eq!(
            parse_normalized_version("5.0.13").unwrap(),
            semver::Version::new(5, 0, 13)
        );
        assert_eq!(
            parse_normalized_version("v5.0.13").unwrap(),
            semver::Version::new(5, 0, 13)
        );
        assert_eq!(
            parse_normalized_version("V5.0.13.0").unwrap(),
            semver::Version::new(5, 0, 13)
        );
        assert_eq!(
            parse_normalized_version("5.0").unwrap(),
            semver::Version::new(5, 0, 0)
        );
        assert_eq!(
            parse_normalized_version("5.0.13.1").unwrap().to_string(),
            "5.0.13-rev.1"
        );
        assert_eq!(
            parse_normalized_version("5.0.13-beta.1").unwrap().to_string(),
            "5.0.13-beta.1"
        );
    }

    #[test]
    fn test_compare_update_states() {
        assert_eq!(
            compare_version_strings("5.0.13.0", "5.0.13"),
            UpdateState::UpToDate
        );
        assert_eq!(
            compare_version_strings("5.0.13.0", "5.0.14"),
            UpdateState::BuildUpdate
        );
        assert_eq!(
            compare_version_strings("5.0.13", "5.1.0"),
            UpdateState::MinorUpdate
        );
        assert_eq!(
            compare_version_strings("5.0.13", "6.0.0"),
            UpdateState::MajorUpdate
        );
        assert_eq!(
            compare_version_strings("5.0.14", "5.0.13"),
            UpdateState::Insider
        );
        assert_eq!(
            compare_version_strings("6.0.0", "5.9.9"),
            UpdateState::Insider
        );
        assert_eq!(
            compare_version_strings("invalid", "5.0.13"),
            UpdateState::Unknown
        );
    }
}
