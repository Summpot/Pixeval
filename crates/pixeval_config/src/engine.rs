// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::fs;
use std::path::Path;

use crate::error::ConfigError;
use crate::layout::*;
use crate::migration::migrate_yaml_str;
use crate::navigation::*;

#[derive(Clone, uniffi::Object)]
pub struct ConfigEngine;

#[uniffi::export]
impl ConfigEngine {
    #[uniffi::constructor]
    pub fn new() -> Self {
        Self
    }

    pub fn migrate_yaml(&self, legacy_yaml: String) -> Result<String, ConfigError> {
        migrate_yaml_str(&legacy_yaml)
    }

    pub fn validate_and_normalize_yaml(&self, yaml: String) -> Result<String, ConfigError> {
        let val: serde_yaml::Value = serde_yaml::from_str(&yaml)?;
        let out = serde_yaml::to_string(&val)?;
        Ok(out)
    }

    pub fn load_from_file(&self, path: String) -> Result<String, ConfigError> {
        let raw = fs::read_to_string(&path)?;
        migrate_yaml_str(&raw)
    }

    pub fn save_to_file(&self, path: String, content: String) -> Result<(), ConfigError> {
        let target_path = Path::new(&path);
        if let Some(parent) = target_path.parent() {
            if !parent.as_os_str().is_empty() {
                fs::create_dir_all(parent)?;
            }
        }

        let tmp_path = format!("{path}.tmp");
        fs::write(&tmp_path, content)?;
        fs::rename(&tmp_path, target_path)?;
        Ok(())
    }

    pub fn yaml_to_json(&self, yaml: String) -> Result<String, ConfigError> {
        let val: serde_yaml::Value = serde_yaml::from_str(&yaml)?;
        let json_val = serde_json::to_value(&val)?;
        Ok(serde_json::to_string(&json_val)?)
    }

    pub fn json_to_yaml(&self, json: String) -> Result<String, ConfigError> {
        let val: serde_json::Value = serde_json::from_str(&json)?;
        let yaml_str = serde_yaml::to_string(&val)?;
        Ok(yaml_str)
    }

    pub fn parse_navigation_yaml(
        &self,
        yaml: String,
        known_pages: Vec<String>,
        known_icons: Vec<String>,
        known_i18n_keys: Option<Vec<String>>,
    ) -> NavigationParseResult {
        let parser = parser::NavigationParser::new(
            &yaml,
            known_pages,
            known_icons,
            known_i18n_keys,
        );
        parser.parse()
    }

    pub fn format_navigation_yaml(
        &self,
        settings: NavigationYamlSettings,
    ) -> Result<String, ConfigError> {
        format_navigation_yaml(&settings)
    }

    pub fn layout_can_place(
        &self,
        cards: Vec<HomeCardBounds>,
        moving_index: Option<u32>,
        bounds: HomeCardBounds,
        row_count: i32,
        column_count: i32,
    ) -> bool {
        can_place(&cards, moving_index, bounds, row_count, column_count)
    }

    pub fn layout_try_find_free_position(
        &self,
        cards: Vec<HomeCardBounds>,
        column_span: i32,
        row_span: i32,
        row_count: i32,
        column_count: i32,
    ) -> Option<GridPosition> {
        try_find_free_position(&cards, column_span, row_span, row_count, column_count)
    }

    pub fn layout_try_find_best_fitting_free_position(
        &self,
        cards: Vec<HomeCardBounds>,
        preferred_column_span: i32,
        preferred_row_span: i32,
        row_count: i32,
        column_count: i32,
    ) -> Option<GridPlacement> {
        try_find_best_fitting_free_position(
            &cards,
            preferred_column_span,
            preferred_row_span,
            row_count,
            column_count,
        )
    }

    pub fn layout_clamp(
        &self,
        bounds: HomeCardBounds,
        row_count: i32,
        column_count: i32,
    ) -> HomeCardBounds {
        clamp(bounds, row_count, column_count)
    }

    pub fn layout_can_resize_grid(
        &self,
        cards: Vec<HomeCardBounds>,
        row_count: i32,
        column_count: i32,
    ) -> bool {
        can_resize_grid(&cards, row_count, column_count)
    }

    pub fn layout_is_within_grid(
        &self,
        bounds: HomeCardBounds,
        row_count: i32,
        column_count: i32,
    ) -> bool {
        is_within_grid(bounds, row_count, column_count)
    }

    pub fn layout_overlaps(
        &self,
        first: HomeCardBounds,
        second: HomeCardBounds,
    ) -> bool {
        overlaps(first, second)
    }

    pub fn layout_normalize_cards(
        &self,
        cards: Vec<HomeCardBounds>,
        row_count: i32,
        column_count: i32,
    ) -> CardNormalizationResult {
        normalize_cards(cards, row_count, column_count)
    }

    pub fn get_card_metadata(&self, source_kind: HomePageCardSourceKind) -> HomeCardMetadata {
        get_card_metadata(source_kind)
    }

    pub fn get_all_card_metadata(&self) -> Vec<HomeCardMetadata> {
        get_all_card_metadata()
    }

    pub fn create_default_cards(&self) -> Vec<HomePageCardLayout> {
        create_default_cards()
    }

    pub fn layout_calculate_edit_candidate(
        &self,
        action: HomeCardEditAction,
        start_bounds: HomeCardBounds,
        delta_column: i32,
        delta_row: i32,
        row_count: i32,
        column_count: i32,
    ) -> HomeCardBounds {
        calculate_edit_candidate(action, start_bounds, delta_column, delta_row, row_count, column_count)
    }

    pub fn parse_home_page_cards_yaml(&self, yaml: String) -> Result<Vec<HomePageCardLayout>, ConfigError> {
        let cards: Vec<HomePageCardLayout> = serde_yaml::from_str(&yaml)?;
        Ok(cards)
    }

    pub fn format_home_page_cards_yaml(&self, cards: Vec<HomePageCardLayout>) -> Result<String, ConfigError> {
        let yaml = serde_yaml::to_string(&cards)?;
        Ok(yaml)
    }

    pub fn load_home_page_cards_from_file(&self, path: String) -> Result<Vec<HomePageCardLayout>, ConfigError> {
        let raw = fs::read_to_string(&path)?;
        self.parse_home_page_cards_yaml(raw)
    }

    pub fn save_home_page_cards_to_file(&self, path: String, cards: Vec<HomePageCardLayout>) -> Result<(), ConfigError> {
        let content = self.format_home_page_cards_yaml(cards)?;
        self.save_to_file(path, content)
    }
}
