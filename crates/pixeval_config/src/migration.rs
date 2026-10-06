// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::error::ConfigError;

pub fn migrate_yaml_str(yaml_str: &str) -> Result<String, ConfigError> {
    if yaml_str.trim().is_empty() {
        return Ok(yaml_str.to_string());
    }

    let mut root_val: serde_yaml::Value = serde_yaml::from_str(yaml_str)?;
    if let serde_yaml::Value::Mapping(ref mut root) = root_val {
        migrate_root_mapping(root);
    }

    let migrated_str = serde_yaml::to_string(&root_val)?;
    Ok(migrated_str)
}

pub fn migrate_root_mapping(root: &mut serde_yaml::Mapping) {
    move_properties(
        root,
        "ApplicationSettings",
        "HomePage",
        &[
            "HomePageRows",
            "HomePageColumns",
            "HideHomePageToolbar",
            "HideHomePageCardTitle",
        ],
    );

    move_properties(
        root,
        "BrowsingExperienceSettings",
        "AutoPlay",
        &[
            "IllustrationViewerAutoPlayInterval",
            "IllustrationViewerAutoPlayMode",
            "IllustrationViewerAutoPlayScope",
        ],
    );

    move_properties(
        root,
        "ApplicationSettings",
        "FileCache",
        &["LimitFileCacheSize", "FileCacheSizeLimitInMegabytes"],
    );

    move_properties(
        root,
        "BrowsingExperienceSettings",
        "ThumbnailLayout",
        &[
            "ThumbnailLayoutType",
            "IllustrationLinedFlowItemHeight",
            "IllustrationGridItemSize",
            "IllustrationGridLineSize",
            "IllustrationMasonryColumnWidth",
        ],
    );

    move_properties(
        root,
        "SearchSettings",
        "RankOptions",
        &["IllustrationRankOption", "NovelRankOption"],
    );

    move_properties(
        root,
        "DownloadSettings",
        "DownloadFormats",
        &[
            "IllustrationDownloadFormat",
            "UgoiraDownloadFormat",
            "NovelDownloadFormat",
        ],
    );

    move_properties(
        root,
        "NetworkSettings",
        "PixivDomainFronting",
        &[
            "EnablePixivDomainFronting",
            "PixivDomainFrontingType",
            "PixivAppApiNameResolver",
            "PixivWebApiNameResolver",
            "PixivAccountNameResolver",
            "PixivOAuthNameResolver",
            "PixivImageNameResolver",
            "PixivImageNameResolver2",
        ],
    );

    move_properties(
        root,
        "NetworkSettings",
        "GitHubDomainFronting",
        &[
            "EnableGitHubDomainFronting",
            "GitHubNameResolver",
            "GitHubApiNameResolver",
            "GitHubAvatarNameResolver",
            "GitHubUserContentNameResolver",
            "GitHubAssetsNameResolver",
            "GitHubCodeloadNameResolver",
        ],
    );

    move_properties(
        root,
        "NetworkSettings",
        "ProxySettings",
        &["ProxyType", "Proxy"],
    );
}

fn move_properties(
    root: &mut serde_yaml::Mapping,
    group_name: &str,
    nested_name: &str,
    property_names: &[&str],
) {
    let group_key = serde_yaml::Value::String(group_name.to_string());
    if let Some(serde_yaml::Value::Mapping(group)) = root.get_mut(&group_key) {
        let nested_key = serde_yaml::Value::String(nested_name.to_string());

        let mut to_move = Vec::new();
        for &name in property_names {
            let key = serde_yaml::Value::String(name.to_string());
            if let Some(val) = group.remove(&key) {
                to_move.push((key, val));
            }
        }

        if !to_move.is_empty() {
            if !group.contains_key(&nested_key) {
                group.insert(
                    nested_key.clone(),
                    serde_yaml::Value::Mapping(serde_yaml::Mapping::new()),
                );
            }

            if let Some(serde_yaml::Value::Mapping(nested)) = group.get_mut(&nested_key) {
                for (key, val) in to_move {
                    if !nested.contains_key(&key) {
                        nested.insert(key, val);
                    }
                }
            }
        }
    }
}
