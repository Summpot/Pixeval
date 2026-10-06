uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod engine;
pub mod error;
pub mod migration;

pub use engine::*;
pub use error::*;
pub use migration::*;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_legacy_yaml_migration() {
        let legacy_yaml = r#"
ApplicationSettings:
  LimitFileCacheSize: true
  FileCacheSizeLimitInMegabytes: 123
  HomePageRows: 9
  HomePageColumns: 3
  HideHomePageToolbar: true
  HideHomePageCardTitle: true
BrowsingExperienceSettings:
  IllustrationViewerAutoPlayInterval: 12
  IllustrationViewerAutoPlayMode: Loop
  IllustrationViewerAutoPlayScope: AllWorks
  ThumbnailLayoutType: Grid
  IllustrationLinedFlowItemHeight: 111
  IllustrationGridItemSize: 222
  IllustrationGridLineSize: 333
  IllustrationMasonryColumnWidth: 444
SearchSettings:
  IllustrationRankOption: Month
  NovelRankOption: Week
DownloadSettings:
  IllustrationDownloadFormat: custom-image
  UgoiraDownloadFormat: custom-animation
  NovelDownloadFormat: custom-novel
NetworkSettings:
  EnablePixivDomainFronting: false
  PixivDomainFrontingType: Fragmentation
  PixivAppApiNameResolver: [127.0.0.1]
  PixivWebApiNameResolver: [127.0.0.2]
  PixivAccountNameResolver: [127.0.0.3]
  PixivOAuthNameResolver: [127.0.0.4]
  PixivImageNameResolver: [127.0.0.5]
  PixivImageNameResolver2: [127.0.0.6]
  EnableGitHubDomainFronting: false
  GitHubNameResolver: [127.0.0.7]
  GitHubApiNameResolver: [127.0.0.8]
  GitHubAvatarNameResolver: [127.0.0.9]
  GitHubUserContentNameResolver: [127.0.0.10]
  GitHubAssetsNameResolver: [127.0.0.11]
  GitHubCodeloadNameResolver: []
  ProxyType: Custom
  Proxy: http://localhost:4321
"#;

        let engine = ConfigEngine::new();
        let migrated = engine.migrate_yaml(legacy_yaml.to_string()).unwrap();

        let val: serde_yaml::Value = serde_yaml::from_str(&migrated).unwrap();
        let root = val.as_mapping().unwrap();

        let app_settings = root.get(&serde_yaml::Value::String("ApplicationSettings".to_string())).unwrap().as_mapping().unwrap();
        assert!(!app_settings.contains_key(&serde_yaml::Value::String("LimitFileCacheSize".to_string())));
        assert!(app_settings.contains_key(&serde_yaml::Value::String("FileCache".to_string())));
        assert!(app_settings.contains_key(&serde_yaml::Value::String("HomePage".to_string())));

        let file_cache = app_settings.get(&serde_yaml::Value::String("FileCache".to_string())).unwrap().as_mapping().unwrap();
        assert_eq!(file_cache.get(&serde_yaml::Value::String("LimitFileCacheSize".to_string())).unwrap(), &serde_yaml::Value::Bool(true));
        assert_eq!(file_cache.get(&serde_yaml::Value::String("FileCacheSizeLimitInMegabytes".to_string())).unwrap(), &serde_yaml::Value::Number(123.into()));

        let net_settings = root.get(&serde_yaml::Value::String("NetworkSettings".to_string())).unwrap().as_mapping().unwrap();
        assert!(!net_settings.contains_key(&serde_yaml::Value::String("Proxy".to_string())));
        assert!(net_settings.contains_key(&serde_yaml::Value::String("ProxySettings".to_string())));
        let proxy_settings = net_settings.get(&serde_yaml::Value::String("ProxySettings".to_string())).unwrap().as_mapping().unwrap();
        assert_eq!(proxy_settings.get(&serde_yaml::Value::String("Proxy".to_string())).unwrap(), &serde_yaml::Value::String("http://localhost:4321".to_string()));
    }

    #[test]
    fn test_new_values_precedence() {
        let yaml = r#"
ApplicationSettings:
  LimitFileCacheSize: true
  FileCacheSizeLimitInMegabytes: 123
  FileCache:
    LimitFileCacheSize: false
"#;
        let engine = ConfigEngine::new();
        let migrated = engine.migrate_yaml(yaml.to_string()).unwrap();
        let val: serde_yaml::Value = serde_yaml::from_str(&migrated).unwrap();
        let root = val.as_mapping().unwrap();
        let app_settings = root.get(&serde_yaml::Value::String("ApplicationSettings".to_string())).unwrap().as_mapping().unwrap();
        let file_cache = app_settings.get(&serde_yaml::Value::String("FileCache".to_string())).unwrap().as_mapping().unwrap();

        // LimitFileCacheSize should be false (existing nested value wins)
        assert_eq!(file_cache.get(&serde_yaml::Value::String("LimitFileCacheSize".to_string())).unwrap(), &serde_yaml::Value::Bool(false));
        // FileCacheSizeLimitInMegabytes should be 123 (moved from parent)
        assert_eq!(file_cache.get(&serde_yaml::Value::String("FileCacheSizeLimitInMegabytes".to_string())).unwrap(), &serde_yaml::Value::Number(123.into()));
    }

    #[test]
    fn test_atomic_save_and_load() {
        let temp_dir = tempfile::tempdir().unwrap();
        let file_path = temp_dir.path().join("config.yaml").to_str().unwrap().to_string();

        let engine = ConfigEngine::new();
        let sample_yaml = "key: value\n";
        engine.save_to_file(file_path.clone(), sample_yaml.to_string()).unwrap();

        let loaded = engine.load_from_file(file_path).unwrap();
        assert!(loaded.contains("key: value"));
    }

    #[test]
    fn test_save_to_bare_file() {
        let temp_dir = tempfile::tempdir().unwrap();
        let prev_dir = std::env::current_dir().unwrap();
        std::env::set_current_dir(temp_dir.path()).unwrap();

        let engine = ConfigEngine::new();
        let sample = "bare: true\n";
        let res = engine.save_to_file("bare_config.yaml".to_string(), sample.to_string());
        let _ = std::env::set_current_dir(prev_dir);

        assert!(res.is_ok());
    }
}
