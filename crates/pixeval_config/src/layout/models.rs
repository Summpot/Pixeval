// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default, uniffi::Record)]
pub struct HomeCardBounds {
    pub column: i32,
    pub row: i32,
    pub column_span: i32,
    pub row_span: i32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default, uniffi::Record)]
pub struct GridPosition {
    pub column: i32,
    pub row: i32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default, uniffi::Record)]
pub struct GridPlacement {
    pub column: i32,
    pub row: i32,
    pub column_span: i32,
    pub row_span: i32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, uniffi::Record)]
pub struct CardNormalizationItem {
    pub index: u32,
    pub bounds: HomeCardBounds,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct CardNormalizationResult {
    pub placed_cards: Vec<CardNormalizationItem>,
    pub removed_indices: Vec<u32>,
    pub changed: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, serde::Serialize, serde::Deserialize, uniffi::Enum)]
pub enum HomePageCardSourceKind {
    WorkRecommended,
    WorkBookmarks,
    WorkRanking,
    WorkNew,
    WorkFollowing,
    WorkMyPixiv,
    WorkRelated,
    SingleSeries,
    WorkPosts,
    WorkSearch,
    UserRecommended,
    UserSearch,
    UserFollowing,
    UserFollower,
    UserMyPixiv,
    Spotlight,
    SingleImage,
    SingleNovel,
    SingleUser,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, uniffi::Enum)]
pub enum HomeCardEditAction {
    Move,
    ResizeLeft,
    ResizeTop,
    ResizeRight,
    ResizeBottom,
    ResizeTopLeft,
    ResizeTopRight,
    ResizeBottomRight,
    ResizeBottomLeft,
}

pub mod card_param_flags {
    pub const WORK_TYPE: u32 = 1 << 0;
    pub const SIMPLE_WORK_TYPE: u32 = 1 << 1;
    pub const PRIVACY_POLICY: u32 = 1 << 2;
    pub const RANK_OPTION: u32 = 1 << 3;
    pub const RANKING_DATE: u32 = 1 << 4;
    pub const USER_ID: u32 = 1 << 5;
    pub const ENTRY_ID: u32 = 1 << 6;
    pub const SEARCH_TEXT: u32 = 1 << 7;
    pub const TAG: u32 = 1 << 8;
    pub const SERIES_ID: u32 = 1 << 9;
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct HomeCardMetadata {
    pub source_kind: HomePageCardSourceKind,
    pub parameter_flags: u32,
    pub default_column_span: i32,
    pub default_row_span: i32,
    pub default_work_type: u32,
    pub default_simple_work_type: u32,
    pub default_privacy_policy: u32,
    pub use_current_user_as_default: bool,
}

fn default_span() -> i32 {
    1
}

fn is_zero_u32(val: &u32) -> bool {
    *val == 0
}

fn is_zero_i32(val: &i32) -> bool {
    *val == 0
}

fn is_zero_i64(val: &i64) -> bool {
    *val == 0
}

#[derive(Debug, Clone, PartialEq, Eq, serde::Serialize, serde::Deserialize, uniffi::Record)]
#[serde(rename_all = "PascalCase")]
pub struct HomePageCardLayout {
    pub source_kind: HomePageCardSourceKind,
    #[serde(rename = "WorkType", default, skip_serializing_if = "is_zero_u32")]
    pub raw_work_type: u32,
    #[serde(rename = "SimpleWorkType", default, skip_serializing_if = "is_zero_u32")]
    pub raw_simple_work_type: u32,
    #[serde(rename = "PrivacyPolicy", default, skip_serializing_if = "is_zero_u32")]
    pub raw_privacy_policy: u32,
    #[serde(rename = "RankOption", default, skip_serializing_if = "is_zero_u32")]
    pub raw_rank_option: u32,
    #[serde(default, skip_serializing_if = "is_zero_i64")]
    pub user_id: i64,
    #[serde(default, skip_serializing_if = "is_zero_i64")]
    pub entry_id: i64,
    #[serde(default, skip_serializing_if = "is_zero_i64")]
    pub series_id: i64,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub search_text: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub tag: Option<String>,
    #[serde(default, skip_serializing_if = "is_zero_u32")]
    pub background_color: u32,
    #[serde(default, skip_serializing_if = "std::ops::Not::not")]
    pub use_specified_ranking_date: bool,
    #[serde(rename = "RankingDate", default, skip_serializing_if = "Option::is_none")]
    pub raw_ranking_date: Option<String>,
    #[serde(default, skip_serializing_if = "is_zero_i32")]
    pub column: i32,
    #[serde(default, skip_serializing_if = "is_zero_i32")]
    pub row: i32,
    #[serde(default = "default_span")]
    pub column_span: i32,
    #[serde(default = "default_span")]
    pub row_span: i32,
}

impl Default for HomePageCardLayout {
    fn default() -> Self {
        Self {
            source_kind: HomePageCardSourceKind::WorkRecommended,
            raw_work_type: 0,
            raw_simple_work_type: 0,
            raw_privacy_policy: 0,
            raw_rank_option: 0,
            user_id: 0,
            entry_id: 0,
            series_id: 0,
            search_text: None,
            tag: None,
            background_color: 0,
            use_specified_ranking_date: false,
            raw_ranking_date: None,
            column: 0,
            row: 0,
            column_span: 1,
            row_span: 1,
        }
    }
}
