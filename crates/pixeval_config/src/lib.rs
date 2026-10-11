uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod engine;
pub mod error;
pub mod layout;
pub mod migration;
pub mod navigation;
pub mod text;

pub use engine::*;
pub use error::*;
pub use layout::*;
pub use migration::*;
pub use navigation::*;

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

    fn create_test_dir() -> tempfile::TempDir {
        let repo_tmp = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .parent()
            .and_then(|p| p.parent())
            .map(|p| p.join("target").join("tmp"))
            .unwrap_or_else(std::env::temp_dir);
        let _ = std::fs::create_dir_all(&repo_tmp);
        tempfile::Builder::new()
            .prefix("test_cfg_")
            .tempdir_in(&repo_tmp)
            .unwrap_or_else(|_| {
                tempfile::Builder::new()
                    .prefix("test_cfg_")
                    .tempdir()
                    .unwrap()
            })
    }

    #[test]
    fn test_atomic_save_and_load() {
        let temp_dir = create_test_dir();
        let file_path = temp_dir.path().join("config.yaml").to_str().unwrap().to_string();

        let engine = ConfigEngine::new();
        let sample_yaml = "key: value\n";
        engine.save_to_file(file_path.clone(), sample_yaml.to_string()).unwrap();

        let loaded = engine.load_from_file(file_path).unwrap();
        assert!(loaded.contains("key: value"));
    }

    #[test]
    fn test_save_to_bare_file() {
        let temp_dir = create_test_dir();
        let prev_dir = std::env::current_dir().unwrap();
        std::env::set_current_dir(temp_dir.path()).unwrap();

        let engine = ConfigEngine::new();
        let sample = "bare: true\n";
        let res = engine.save_to_file("bare_config.yaml".to_string(), sample.to_string());
        let _ = std::env::set_current_dir(prev_dir);

        assert!(res.is_ok());
    }

    #[test]
    fn test_yaml_json_roundtrip() {
        let engine = ConfigEngine::new();
        let yaml = "name: Pixeval\nversion: 2\nenabled: true\nitems:\n  - a\n  - b\n";
        let json = engine.yaml_to_json(yaml.to_string()).unwrap();
        assert!(json.contains("\"name\":\"Pixeval\""));
        assert!(json.contains("\"version\":2"));
        assert!(json.contains("\"enabled\":true"));

        let restored_yaml = engine.json_to_yaml(json).unwrap();
        assert!(restored_yaml.contains("name: Pixeval"));
        assert!(restored_yaml.contains("version: 2"));
    }

    #[test]
    fn test_navigation_parser_and_formatter() {
        let engine = ConfigEngine::new();
        let valid_yaml = r#"
newTab: Search

header:
  - folder: Level1
    icon: Folder
    children:
      - folder: Level2
        icon: FolderOpen
        children:
          - page: Search

footer:
  - page: Settings
"#;

        let known_pages = vec!["Search".to_string(), "Settings".to_string()];
        let known_icons = vec!["Folder".to_string(), "FolderOpen".to_string()];
        let res = engine.parse_navigation_yaml(
            valid_yaml.to_string(),
            known_pages.clone(),
            known_icons.clone(),
            None,
        );
        assert!(res.is_valid);
        assert!(res.diagnostics.is_empty());
        let settings = res.settings.unwrap();
        assert_eq!(settings.new_tab.as_deref(), Some("Search"));

        let formatted = engine.format_navigation_yaml(settings).unwrap();
        assert!(formatted.contains("- folder: Level1"));
        assert!(formatted.contains("- folder: Level2"));
        assert!(formatted.contains("- page: Search"));
        assert!(formatted.contains("- page: Settings"));

        // Reject unknown field
        let invalid_field = r#"
newTab: Search
header:
  - page: Search
    visible: false
footer:
  - page: Settings
"#;
        let res_invalid = engine.parse_navigation_yaml(
            invalid_field.to_string(),
            known_pages.clone(),
            known_icons.clone(),
            None,
        );
        assert!(!res_invalid.is_valid);
        assert!(res_invalid
            .diagnostics
            .iter()
            .any(|d| d.kind == NavigationDiagnosticKind::UnknownField && d.arguments.contains(&"visible".to_string())));

        // Reject exceeding max depth
        let too_deep = r#"
newTab: Search
header:
  - folder: Level1
    children:
      - folder: Level2
        children:
          - folder: Level3
            children:
              - page: Search
footer:
  - page: Settings
"#;
        let res_deep = engine.parse_navigation_yaml(
            too_deep.to_string(),
            known_pages,
            known_icons,
            None,
        );
        assert!(!res_deep.is_valid);
        assert!(res_deep
            .diagnostics
            .iter()
            .any(|d| d.kind == NavigationDiagnosticKind::MaxDepthExceededFolder
                || d.kind == NavigationDiagnosticKind::MaxDepthExceeded));
    }

    #[test]
    fn test_card_layout_engine() {
        let engine = ConfigEngine::new();
        let existing = HomeCardBounds {
            column: 0,
            row: 0,
            column_span: 1,
            row_span: 1,
        };
        let moving = HomeCardBounds {
            column: 0,
            row: 0,
            column_span: 1,
            row_span: 1,
        };

        // Collision detection: same bounds collision when moving_index is None
        assert!(!engine.layout_can_place(vec![existing], None, moving, 2, 2));

        // When moving_index is Some(0), ignores original bounds
        assert!(engine.layout_can_place(vec![moving], Some(0), HomeCardBounds { column: 1, row: 1, column_span: 1, row_span: 1 }, 2, 2));

        // Free position scan
        let free_pos = engine.layout_try_find_free_position(vec![existing], 1, 1, 2, 2).unwrap();
        assert_eq!(free_pos.column, 1);
        assert_eq!(free_pos.row, 0);

        // Clamp
        let clamped = engine.layout_clamp(
            HomeCardBounds { column: -1, row: -1, column_span: 4, row_span: 4 },
            2,
            3,
        );
        assert_eq!(clamped, HomeCardBounds { column: 0, row: 0, column_span: 3, row_span: 2 });

        // Normalization (2D bin-packing & overlap resolution)
        let cards = vec![
            HomeCardBounds { column: 0, row: 0, column_span: 1, row_span: 1 },
            HomeCardBounds { column: 0, row: 0, column_span: 1, row_span: 1 }, // overlaps!
        ];
        let norm = engine.layout_normalize_cards(cards, 2, 2);
        assert!(norm.changed);
        assert_eq!(norm.placed_cards.len(), 2);
        assert_eq!(norm.placed_cards[0].bounds, HomeCardBounds { column: 0, row: 0, column_span: 1, row_span: 1 });
        assert_eq!(norm.placed_cards[1].bounds, HomeCardBounds { column: 1, row: 0, column_span: 1, row_span: 1 });
    }

    #[test]
    fn test_card_registry_and_default_cards() {
        let engine = ConfigEngine::new();
        let all_meta = engine.get_all_card_metadata();
        assert_eq!(all_meta.len(), 19);

        let spotlight_meta = engine.get_card_metadata(HomePageCardSourceKind::Spotlight);
        assert_eq!(spotlight_meta.parameter_flags, 0);

        let bookmarks_meta = engine.get_card_metadata(HomePageCardSourceKind::WorkBookmarks);
        assert!(bookmarks_meta.use_current_user_as_default);
        assert_ne!(bookmarks_meta.parameter_flags, 0);

        let defaults = engine.create_default_cards();
        assert_eq!(defaults.len(), 3);
        assert_eq!(defaults[0].source_kind, HomePageCardSourceKind::Spotlight);
        assert_eq!(defaults[1].source_kind, HomePageCardSourceKind::UserRecommended);
        assert_eq!(defaults[2].source_kind, HomePageCardSourceKind::WorkRecommended);
    }

    #[test]
    fn test_layout_calculate_edit_candidate() {
        let engine = ConfigEngine::new();
        let start = HomeCardBounds { column: 1, row: 1, column_span: 2, row_span: 2 };

        // Move inside grid
        let moved = engine.layout_calculate_edit_candidate(HomeCardEditAction::Move, start, 1, 1, 6, 6);
        assert_eq!(moved, HomeCardBounds { column: 2, row: 2, column_span: 2, row_span: 2 });

        // Move clamped to bottom-right
        let clamped_move = engine.layout_calculate_edit_candidate(HomeCardEditAction::Move, start, 10, 10, 4, 4);
        assert_eq!(clamped_move, HomeCardBounds { column: 2, row: 2, column_span: 2, row_span: 2 });

        // Resize right
        let resized_right = engine.layout_calculate_edit_candidate(HomeCardEditAction::ResizeRight, start, 1, 0, 6, 6);
        assert_eq!(resized_right, HomeCardBounds { column: 1, row: 1, column_span: 3, row_span: 2 });

        // Resize left with minimum span enforcement
        let resized_left = engine.layout_calculate_edit_candidate(HomeCardEditAction::ResizeLeft, start, 5, 0, 6, 6);
        assert_eq!(resized_left.column_span, 1);
        assert_eq!(resized_left.column, 2);
    }

    #[test]
    fn test_home_page_cards_yaml_roundtrip() {
        let engine = ConfigEngine::new();
        let default_cards = engine.create_default_cards();

        let yaml = engine.format_home_page_cards_yaml(default_cards.clone()).unwrap();
        assert!(yaml.contains("Spotlight"));
        assert!(yaml.contains("UserRecommended"));
        assert!(yaml.contains("WorkRecommended"));

        let parsed = engine.parse_home_page_cards_yaml(yaml).unwrap();
        assert_eq!(parsed.len(), 3);
        assert_eq!(parsed[0].source_kind, HomePageCardSourceKind::Spotlight);
        assert_eq!(parsed[1].source_kind, HomePageCardSourceKind::UserRecommended);
        assert_eq!(parsed[2].source_kind, HomePageCardSourceKind::WorkRecommended);

        // Test file persistence
        let dir = create_test_dir();
        let file_path = dir.path().join("home_page_cards.yaml").to_str().unwrap().to_string();
        engine.save_home_page_cards_to_file(file_path.clone(), parsed).unwrap();

        let loaded = engine.load_home_page_cards_from_file(file_path).unwrap();
        assert_eq!(loaded.len(), 3);
        assert_eq!(loaded[0].source_kind, HomePageCardSourceKind::Spotlight);
    }
}
