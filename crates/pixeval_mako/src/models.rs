// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use serde::{Deserialize, Serialize};

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct ImageUrls {
    #[serde(default)]
    pub square_medium: Option<String>,
    #[serde(default)]
    pub medium: Option<String>,
    #[serde(default)]
    pub large: Option<String>,
    #[serde(default)]
    pub original: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct ProfileImageUrls {
    #[serde(default)]
    pub px_16x16: Option<String>,
    #[serde(default)]
    pub px_50x50: Option<String>,
    #[serde(default)]
    pub px_170x170: Option<String>,
    #[serde(default)]
    pub medium: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct Tag {
    pub name: String,
    #[serde(default)]
    pub translated_name: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct Series {
    pub id: i64,
    pub title: String,
    #[serde(default)]
    pub user: Option<User>,
    #[serde(default)]
    pub mask_text: Option<String>,
    #[serde(default, rename = "url")]
    pub cover_url: Option<String>,
    #[serde(default)]
    pub published_content_count: Option<i32>,
    #[serde(default)]
    pub latest_content_id: Option<i64>,
    #[serde(default)]
    pub last_published_content_datetime: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct MetaSinglePage {
    #[serde(default)]
    pub original_image_url: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct MetaPage {
    pub image_urls: ImageUrls,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct User {
    pub id: i64,
    pub name: String,
    pub account: String,
    #[serde(default)]
    pub profile_image_urls: ProfileImageUrls,
    #[serde(default)]
    pub is_followed: bool,
    #[serde(default)]
    pub comment: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct UserProfile {
    #[serde(default, rename = "webpage")]
    pub web_page: Option<String>,
    #[serde(default)]
    pub gender: Option<String>,
    #[serde(default)]
    pub birth: Option<String>,
    #[serde(default)]
    pub birth_day: Option<String>,
    #[serde(default)]
    pub birth_year: Option<i32>,
    #[serde(default)]
    pub region: Option<String>,
    #[serde(default)]
    pub address_id: Option<i32>,
    #[serde(default)]
    pub country_code: Option<String>,
    #[serde(default)]
    pub job: Option<String>,
    #[serde(default)]
    pub job_id: Option<i32>,
    #[serde(default)]
    pub total_follow_users: Option<i64>,
    #[serde(default)]
    pub total_mypixiv_users: Option<i64>,
    #[serde(default)]
    pub total_illusts: Option<i64>,
    #[serde(default)]
    pub total_manga: Option<i64>,
    #[serde(default)]
    pub total_novels: Option<i64>,
    #[serde(default)]
    pub total_illust_bookmarks_public: Option<i64>,
    #[serde(default)]
    pub total_illust_series: Option<i64>,
    #[serde(default)]
    pub total_novel_series: Option<i64>,
    #[serde(default)]
    pub background_image_url: Option<String>,
    #[serde(default)]
    pub twitter_account: Option<String>,
    #[serde(default)]
    pub twitter_url: Option<String>,
    #[serde(default)]
    pub is_premium: bool,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct UserDetail {
    pub user: User,
    #[serde(default)]
    pub profile: UserProfile,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct Illustration {
    pub id: i64,
    pub title: String,
    #[serde(rename = "type")]
    pub illust_type: String,
    pub image_urls: ImageUrls,
    #[serde(default)]
    pub caption: String,
    #[serde(default)]
    pub restrict: i32,
    pub user: User,
    #[serde(default)]
    pub tags: Vec<Tag>,
    #[serde(default)]
    pub tools: Vec<String>,
    pub create_date: String,
    #[serde(default)]
    pub page_count: i32,
    #[serde(default)]
    pub width: i32,
    #[serde(default)]
    pub height: i32,
    #[serde(default)]
    pub sanity_level: i32,
    #[serde(default)]
    pub x_restrict: i32,
    #[serde(default)]
    pub series: Option<Series>,
    #[serde(default)]
    pub meta_single_page: MetaSinglePage,
    #[serde(default)]
    pub meta_pages: Vec<MetaPage>,
    #[serde(default)]
    pub total_view: i64,
    #[serde(default)]
    pub total_bookmarks: i64,
    #[serde(default)]
    pub is_bookmarked: bool,
    #[serde(default)]
    pub visible: bool,
    #[serde(default)]
    pub is_muted: bool,
    #[serde(default)]
    pub total_comments: Option<i64>,
    #[serde(default)]
    pub illust_ai_type: i32,
    #[serde(default)]
    pub illust_book_style: i32,
    #[serde(default)]
    pub restriction_attributes: Option<Vec<String>>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct Novel {
    pub id: i64,
    pub title: String,
    #[serde(default)]
    pub caption: String,
    #[serde(default)]
    pub restrict: i32,
    #[serde(default)]
    pub x_restrict: i32,
    #[serde(default)]
    pub is_original: bool,
    pub image_urls: ImageUrls,
    pub create_date: String,
    #[serde(default)]
    pub tags: Vec<Tag>,
    #[serde(default)]
    pub page_count: i32,
    #[serde(default)]
    pub text_length: i32,
    pub user: User,
    #[serde(default)]
    pub series: Option<Series>,
    #[serde(default)]
    pub is_bookmarked: bool,
    #[serde(default)]
    pub total_bookmarks: i64,
    #[serde(default)]
    pub total_view: i64,
    #[serde(default)]
    pub total_comments: Option<i64>,
    #[serde(default)]
    pub is_muted: bool,
    #[serde(default)]
    pub novel_ai_type: i32,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct BookmarkTag {
    pub name: String,
    #[serde(default)]
    pub count: i64,
    #[serde(default)]
    pub is_registered: bool,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct BookmarkDetail {
    #[serde(default)]
    pub is_bookmarked: bool,
    #[serde(default)]
    pub tags: Vec<BookmarkTag>,
    #[serde(default)]
    pub restrict: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct TrendingTag {
    pub tag: String,
    #[serde(default)]
    pub translated_name: Option<String>,
    pub illust: Illustration,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct TokenUser {
    pub id: String,
    pub name: String,
    pub account: String,
    #[serde(default)]
    pub mail_address: String,
    #[serde(default)]
    pub is_premium: bool,
    #[serde(default)]
    pub profile_image_urls: ProfileImageUrls,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct TokenResponse {
    pub access_token: String,
    #[serde(default)]
    pub expires_in: i64,
    #[serde(default)]
    pub token_type: String,
    #[serde(default)]
    pub refresh_token: String,
    #[serde(default)]
    pub user: Option<TokenUser>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct SingleUserResponse {
    pub user: User,
    #[serde(default)]
    pub profile: UserProfile,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq, Eq)]
pub struct BoolResult {
    pub success: bool,
}

#[derive(uniffi::Enum, Clone, Debug, PartialEq)]
pub enum WorkEntry {
    Illust { illustration: Illustration },
    NovelWork { novel: Novel },
}

#[derive(uniffi::Record, Clone, Debug)]
pub struct MakoConfigurationDto {
    pub domain_fronting_enabled: bool,
    pub cooldown_ms: u64,
    pub split_delay_ms: u64,
    pub host_ips: HashMap<String, Vec<String>>,
    pub proxy_url: Option<String>,
    pub target_filter: Option<String>,
    pub mirror_host: Option<String>,
    pub web_cookie: Option<String>,
}

impl Default for MakoConfigurationDto {
    fn default() -> Self {
        Self {
            domain_fronting_enabled: true,
            cooldown_ms: 700,
            split_delay_ms: 100,
            host_ips: HashMap::new(),
            proxy_url: None,
            target_filter: Some("for_android".to_string()),
            mirror_host: None,
            web_cookie: None,
        }
    }
}

// =========================================================================
// Internal API Response Wrappers (Serde JSON deserialization targets)
// =========================================================================

#[derive(Deserialize)]
pub struct IllustrationResponse {
    #[serde(default)]
    pub illusts: Vec<Illustration>,
    #[serde(default)]
    pub next_url: Option<String>,
}

#[derive(Deserialize)]
pub struct NovelResponse {
    #[serde(default)]
    pub novels: Vec<Novel>,
    #[serde(default)]
    pub next_url: Option<String>,
}

#[derive(Deserialize)]
pub struct SingleIllustrationResponse {
    pub illust: Illustration,
}

#[derive(Deserialize)]
pub struct SingleNovelResponse {
    pub novel: Novel,
}

#[derive(Deserialize)]
pub struct BookmarkDetailResponse {
    pub bookmark_detail: BookmarkDetail,
}

#[derive(Deserialize)]
pub struct BookmarkTagsResponse {
    #[serde(default)]
    pub bookmark_tags: Vec<BookmarkTag>,
    #[serde(default)]
    pub next_url: Option<String>,
}

#[derive(Deserialize)]
pub struct TrendingTagResponse {
    #[serde(default)]
    pub trend_tags: Vec<TrendingTag>,
}

#[derive(Deserialize)]
pub struct UserPreview {
    pub user: User,
}

#[derive(Deserialize)]
pub struct UserResponse {
    #[serde(default)]
    pub user_previews: Vec<UserPreview>,
    #[serde(default)]
    pub users: Vec<User>,
    #[serde(default)]
    pub next_url: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
pub struct AiShowSettingsResponse {
    pub show_ai: bool,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
pub struct RestrictedModeSettingsResponse {
    pub is_restricted_mode_enabled: bool,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
pub struct UgoiraFrame {
    pub file: String,
    pub delay: i32,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
pub struct UgoiraZipUrls {
    pub medium: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
pub struct UgoiraMetadata {
    pub zip_urls: UgoiraZipUrls,
    pub frames: Vec<UgoiraFrame>,
}

#[derive(Deserialize)]
pub struct UgoiraMetadataResponse {
    pub ugoira_metadata: UgoiraMetadata,
}

#[derive(Deserialize)]
pub struct SeriesResponse {
    #[serde(default)]
    pub series: Vec<Series>,
    #[serde(default)]
    pub next_url: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct SpotlightArticle {
    pub id: i64,
    pub title: String,
    #[serde(default)]
    pub pure_title: Option<String>,
    #[serde(default)]
    pub thumbnail: String,
    #[serde(default)]
    pub article_url: String,
    #[serde(default)]
    pub publish_date: String,
    #[serde(default)]
    pub category: String,
    #[serde(default)]
    pub subcategory_label: String,
}

#[derive(Deserialize, Default, Clone, Debug)]
pub struct SpotlightResponse {
    #[serde(default)]
    pub spotlight_articles: Vec<SpotlightArticle>,
    #[serde(default)]
    pub next_url: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct StampInfo {
    pub stamp_id: i64,
    #[serde(default)]
    pub stamp_url: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct CommentRecord {
    pub id: i64,
    pub comment: String,
    pub date: String,
    pub user: User,
    #[serde(default)]
    pub has_replies: bool,
    #[serde(default)]
    pub stamp: Option<StampInfo>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct CommentsResponse {
    pub comments: Vec<CommentRecord>,
    pub next_url: Option<String>,
}

#[derive(Deserialize, Default)]
pub struct AddCommentResponse {
    #[serde(default)]
    pub comment: Option<CommentRecord>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct MangaSeriesContextInfo {
    pub content_order: i32,
    pub prev_illust: Option<Illustration>,
    pub next_illust: Option<Illustration>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct MangaSeriesContextResult {
    pub series: Option<Series>,
    pub context: MangaSeriesContextInfo,
}

#[derive(Deserialize, Default)]
pub struct MangaSeriesContextResponseRaw {
    #[serde(default)]
    pub series: Option<Series>,
    #[serde(default)]
    pub illust_series_detail: Option<Series>,
    #[serde(default)]
    pub prev: Option<Illustration>,
    #[serde(default)]
    pub next: Option<Illustration>,
    #[serde(default)]
    pub content_order: Option<i32>,
}

#[derive(Deserialize, Default)]
pub struct AutocompleteResponse {
    #[serde(default)]
    pub tags: Vec<Tag>,
    #[serde(default)]
    pub search_auto_complete_keywords: Vec<String>,
}



