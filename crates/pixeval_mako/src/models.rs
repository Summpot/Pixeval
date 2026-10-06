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

// -----------------------------------------------------------------------------
// Deserializers for flexible Pixiv Webview and API payloads
// -----------------------------------------------------------------------------

pub(crate) fn deserialize_flexible_i64<'de, D>(deserializer: D) -> Result<i64, D::Error>
where
    D: serde::Deserializer<'de>,
{
    use serde::de::{Error, Visitor};
    use std::fmt;

    struct FlexibleI64Visitor;

    impl Visitor<'_> for FlexibleI64Visitor {
        type Value = i64;

        fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
            formatter.write_str("an integer or string representing an integer")
        }

        fn visit_i64<E: Error>(self, v: i64) -> Result<Self::Value, E> {
            Ok(v)
        }

        fn visit_u64<E: Error>(self, v: u64) -> Result<Self::Value, E> {
            Ok(v as i64)
        }

        fn visit_str<E: Error>(self, v: &str) -> Result<Self::Value, E> {
            v.parse::<i64>().map_err(Error::custom)
        }
    }

    deserializer.deserialize_any(FlexibleI64Visitor)
}

pub(crate) fn deserialize_optional_flexible_i64<'de, D>(deserializer: D) -> Result<Option<i64>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    use serde::de::{Error, Visitor};
    use std::fmt;

    struct FlexibleOptI64Visitor;

    impl<'de> Visitor<'de> for FlexibleOptI64Visitor {
        type Value = Option<i64>;

        fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
            formatter.write_str("an optional integer or string")
        }

        fn visit_none<E: Error>(self) -> Result<Self::Value, E> {
            Ok(None)
        }

        fn visit_unit<E: Error>(self) -> Result<Self::Value, E> {
            Ok(None)
        }

        fn visit_some<D2: serde::Deserializer<'de>>(self, deserializer: D2) -> Result<Self::Value, D2::Error> {
            deserialize_flexible_i64(deserializer).map(Some)
        }

        fn visit_i64<E: Error>(self, v: i64) -> Result<Self::Value, E> {
            Ok(Some(v))
        }

        fn visit_u64<E: Error>(self, v: u64) -> Result<Self::Value, E> {
            Ok(Some(v as i64))
        }

        fn visit_str<E: Error>(self, v: &str) -> Result<Self::Value, E> {
            if v.is_empty() {
                Ok(None)
            } else {
                v.parse::<i64>().map(Some).map_err(Error::custom)
            }
        }
    }

    deserializer.deserialize_option(FlexibleOptI64Visitor)
}

pub(crate) fn deserialize_flexible_i32<'de, D>(deserializer: D) -> Result<i32, D::Error>
where
    D: serde::Deserializer<'de>,
{
    use serde::de::{Error, Visitor};
    use std::fmt;

    struct FlexibleI32Visitor;

    impl Visitor<'_> for FlexibleI32Visitor {
        type Value = i32;

        fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
            formatter.write_str("an integer or string representing an integer")
        }

        fn visit_i64<E: Error>(self, v: i64) -> Result<Self::Value, E> {
            Ok(v as i32)
        }

        fn visit_u64<E: Error>(self, v: u64) -> Result<Self::Value, E> {
            Ok(v as i32)
        }

        fn visit_str<E: Error>(self, v: &str) -> Result<Self::Value, E> {
            v.parse::<i32>().map_err(Error::custom)
        }
    }

    deserializer.deserialize_any(FlexibleI32Visitor)
}

pub(crate) fn deserialize_dict_or_list<'de, D, T>(deserializer: D) -> Result<Vec<T>, D::Error>
where
    D: serde::Deserializer<'de>,
    T: serde::Deserialize<'de>,
{
    use serde::de::{Error, Visitor};
    use std::fmt;

    struct DictOrListVisitor<T>(std::marker::PhantomData<T>);

    impl<'de, T: serde::Deserialize<'de>> Visitor<'de> for DictOrListVisitor<T> {
        type Value = Vec<T>;

        fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
            formatter.write_str("a sequence or a map")
        }

        fn visit_seq<A>(self, mut seq: A) -> Result<Self::Value, A::Error>
        where
            A: serde::de::SeqAccess<'de>,
        {
            let mut list = Vec::new();
            while let Some(item) = seq.next_element()? {
                list.push(item);
            }
            Ok(list)
        }

        fn visit_map<M>(self, mut map: M) -> Result<Self::Value, M::Error>
        where
            M: serde::de::MapAccess<'de>,
        {
            let mut list = Vec::new();
            while let Some((_, value)) = map.next_entry::<serde_json::Value, T>()? {
                list.push(value);
            }
            Ok(list)
        }

        fn visit_none<E: Error>(self) -> Result<Self::Value, E> {
            Ok(Vec::new())
        }

        fn visit_unit<E: Error>(self) -> Result<Self::Value, E> {
            Ok(Vec::new())
        }
    }

    deserializer.deserialize_any(DictOrListVisitor(std::marker::PhantomData))
}

// -----------------------------------------------------------------------------
// Novel Content & Webview Models
// -----------------------------------------------------------------------------

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelRating {
    #[serde(default)]
    pub like: i32,
    #[serde(default)]
    pub bookmark: i32,
    #[serde(default)]
    pub view: i32,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelNavigation {
    #[serde(default, deserialize_with = "deserialize_flexible_i64")]
    pub id: i64,
    #[serde(default)]
    pub viewable: bool,
    #[serde(default, rename = "contentOrder")]
    pub content_order: String,
    #[serde(default)]
    pub title: String,
    #[serde(default, rename = "coverUrl")]
    pub cover_url: String,
    #[serde(default, rename = "viewableMessage")]
    pub viewable_message: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct SeriesNavigation {
    #[serde(default, rename = "nextNovel")]
    pub next_novel: Option<NovelNavigation>,
    #[serde(default, rename = "prevNovel")]
    pub prev_novel: Option<NovelNavigation>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelImageUrls {
    #[serde(default, rename = "240mw")]
    pub mw240: String,
    #[serde(default, rename = "480mw")]
    pub mw480: String,
    #[serde(default, rename = "1200x1200")]
    pub x1200: String,
    #[serde(default, rename = "128x128")]
    pub x128: String,
    #[serde(default)]
    pub original: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelImage {
    #[serde(default, deserialize_with = "deserialize_flexible_i64", rename = "novelImageId")]
    pub novel_image_id: i64,
    #[serde(default, deserialize_with = "deserialize_flexible_i32")]
    pub sl: i32,
    #[serde(default)]
    pub urls: NovelImageUrls,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelIllustrationUrls {
    #[serde(default)]
    pub small: Option<String>,
    #[serde(default)]
    pub medium: String,
    #[serde(default)]
    pub original: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelTagInfo {
    #[serde(default)]
    pub tag: String,
    #[serde(default, rename = "userId")]
    pub user_id: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelIllustrationInfo {
    #[serde(default)]
    pub title: String,
    #[serde(default)]
    pub description: String,
    #[serde(default)]
    pub restrict: i32,
    #[serde(default, rename = "xRestrict")]
    pub x_restrict: i32,
    #[serde(default, deserialize_with = "deserialize_flexible_i32")]
    pub sl: i32,
    #[serde(default)]
    pub tags: Vec<NovelTagInfo>,
    #[serde(default)]
    pub images: NovelIllustrationUrls,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelUserRef {
    #[serde(default, deserialize_with = "deserialize_flexible_i64")]
    pub id: i64,
    #[serde(default)]
    pub name: String,
    #[serde(default)]
    pub image: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelIllustration {
    #[serde(default)]
    pub visible: bool,
    #[serde(default, rename = "availableMessage")]
    pub available_message: Option<String>,
    #[serde(default)]
    pub illust: NovelIllustrationInfo,
    #[serde(default)]
    pub user: NovelUserRef,
    #[serde(default, deserialize_with = "deserialize_flexible_i64")]
    pub id: i64,
    #[serde(default, deserialize_with = "deserialize_flexible_i32")]
    pub page: i32,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelMarker {
    #[serde(default)]
    pub page: i32,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelContent {
    #[serde(default, deserialize_with = "deserialize_flexible_i64")]
    pub id: i64,
    #[serde(default)]
    pub title: String,
    #[serde(default, deserialize_with = "deserialize_optional_flexible_i64", rename = "seriesId")]
    pub series_id: Option<i64>,
    #[serde(default, rename = "seriesTitle")]
    pub series_title: Option<String>,
    #[serde(default, rename = "seriesIsWatched")]
    pub series_is_watched: Option<bool>,
    #[serde(default, deserialize_with = "deserialize_flexible_i64", rename = "userId")]
    pub user_id: i64,
    #[serde(default, rename = "coverUrl")]
    pub cover_url: String,
    #[serde(default)]
    pub tags: Vec<String>,
    #[serde(default)]
    pub caption: String,
    #[serde(default, rename = "cdate")]
    pub cdate: String,
    #[serde(default)]
    pub rating: Option<NovelRating>,
    #[serde(default)]
    pub text: String,
    #[serde(default)]
    pub marker: Option<NovelMarker>,
    #[serde(default, deserialize_with = "deserialize_dict_or_list")]
    pub illusts: Vec<NovelIllustration>,
    #[serde(default, deserialize_with = "deserialize_dict_or_list")]
    pub images: Vec<NovelImage>,
    #[serde(default, rename = "seriesNavigation")]
    pub series_navigation: Option<SeriesNavigation>,
    #[serde(default, rename = "aiType")]
    pub ai_type: i32,
    #[serde(default, rename = "isOriginal")]
    pub is_original: bool,
    #[serde(default)]
    pub language: String,
}

// -----------------------------------------------------------------------------
// Search Options Models
// -----------------------------------------------------------------------------

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct BookmarkRange {
    #[serde(default)]
    pub bookmark_num_min: String,
    #[serde(default)]
    pub bookmark_num_max: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct SearchOptionsLanguage {
    #[serde(default)]
    pub code: String,
    #[serde(default)]
    pub name: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct SearchOptionsGenre {
    #[serde(default)]
    pub id: i32,
    #[serde(default)]
    pub label: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct IllustrationSearchOptions {
    #[serde(default)]
    pub bookmark_ranges: Vec<BookmarkRange>,
    #[serde(default)]
    pub show_ai_condition: bool,
    #[serde(default)]
    pub languages: Vec<SearchOptionsLanguage>,
    #[serde(default)]
    pub tools: Vec<String>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelSearchOptions {
    #[serde(default)]
    pub bookmark_ranges: Vec<BookmarkRange>,
    #[serde(default)]
    pub show_ai_condition: bool,
    #[serde(default)]
    pub languages: Vec<SearchOptionsLanguage>,
    #[serde(default)]
    pub genres: Vec<SearchOptionsGenre>,
    #[serde(default)]
    pub word_count_supported_languages: String,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct SearchOptions {
    pub illust: IllustrationSearchOptions,
    pub novel: NovelSearchOptions,
}

#[derive(Deserialize, Default)]
pub(crate) struct OptionsWrapper<T> {
    #[serde(default)]
    pub options: Vec<T>,
}

#[derive(Deserialize, Default)]
pub(crate) struct IllustSearchOptionsRaw {
    #[serde(default)]
    pub bookmark_ranges: Vec<BookmarkRange>,
    #[serde(default)]
    pub show_ai_condition: bool,
    #[serde(default)]
    pub lang: OptionsWrapper<SearchOptionsLanguage>,
    #[serde(default)]
    pub tool: OptionsWrapper<String>,
}

#[derive(Deserialize, Default)]
pub(crate) struct NovelSearchOptionsRaw {
    #[serde(default)]
    pub bookmark_ranges: Vec<BookmarkRange>,
    #[serde(default)]
    pub show_ai_condition: bool,
    #[serde(default)]
    pub lang: OptionsWrapper<SearchOptionsLanguage>,
    #[serde(default)]
    pub genre: OptionsWrapper<SearchOptionsGenre>,
    #[serde(default)]
    pub word_count_supported_languages: String,
}

#[derive(Deserialize, Default)]
pub(crate) struct SearchOptionsRaw {
    #[serde(default)]
    pub illust: IllustSearchOptionsRaw,
    #[serde(default)]
    pub novel: NovelSearchOptionsRaw,
}

// -----------------------------------------------------------------------------
// Advanced Search Parameter DTOs
// -----------------------------------------------------------------------------

#[derive(uniffi::Record, Clone, Debug, Default)]
pub struct IllustrationSearchParams {
    pub word: String,
    pub search_target: Option<String>,
    pub sort: Option<String>,
    pub search_ai_type: Option<i32>,
    pub content_type: Option<String>,
    pub ratio_pattern: Option<String>,
    pub merge_plain_keyword_results: Option<bool>,
    pub include_translated_tag_results: Option<bool>,
    pub include_potential_violation_works: Option<bool>,
    pub start_date: Option<String>,
    pub end_date: Option<String>,
    pub width_min: Option<i32>,
    pub width_max: Option<i32>,
    pub height_min: Option<i32>,
    pub height_max: Option<i32>,
    pub tool: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, Default)]
pub struct NovelSearchParams {
    pub word: String,
    pub search_target: Option<String>,
    pub sort: Option<String>,
    pub search_ai_type: Option<i32>,
    pub lang: Option<String>,
    pub content_length_option: Option<String>,
    pub content_length_min: Option<i32>,
    pub content_length_max: Option<i32>,
    pub is_original_only: Option<bool>,
    pub genre: Option<i32>,
    pub is_replaceable_only: Option<bool>,
    pub merge_plain_keyword_results: Option<bool>,
    pub include_translated_tag_results: Option<bool>,
    pub include_potential_violation_works: Option<bool>,
    pub start_date: Option<String>,
    pub end_date: Option<String>,
}




