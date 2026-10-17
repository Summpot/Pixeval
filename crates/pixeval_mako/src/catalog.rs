// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use chrono::Datelike;
use serde::{Deserialize, Deserializer, Serialize, Serializer};

use crate::client::{APP_API_BASE_URL, MakoError};

pub(crate) fn url_encode(input: &str) -> String {
    url::form_urlencoded::byte_serialize(input.as_bytes()).collect()
}

mod protocol_serde {
    use serde::de::{self, Visitor};
    use serde::{Deserializer, Serializer};
    use std::fmt;

    pub fn serialize_str<S: Serializer>(value: &str, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.serialize_str(value)
    }

    pub fn deserialize<'de, D, T>(
        deserializer: D,
        from_str: fn(&str) -> T,
        from_int: fn(i64) -> T,
    ) -> Result<T, D::Error>
    where
        D: Deserializer<'de>,
    {
        struct ProtocolVisitor<T> {
            from_str: fn(&str) -> T,
            from_int: fn(i64) -> T,
        }

        impl<'de, T> Visitor<'de> for ProtocolVisitor<T> {
            type Value = T;

            fn expecting(&self, f: &mut fmt::Formatter) -> fmt::Result {
                f.write_str("a protocol enum as string or integer")
            }

            fn visit_str<E: de::Error>(self, v: &str) -> Result<T, E> {
                Ok((self.from_str)(v))
            }

            fn visit_string<E: de::Error>(self, v: String) -> Result<T, E> {
                Ok((self.from_str)(&v))
            }

            fn visit_i64<E: de::Error>(self, v: i64) -> Result<T, E> {
                Ok((self.from_int)(v))
            }

            fn visit_u64<E: de::Error>(self, v: u64) -> Result<T, E> {
                Ok((self.from_int)(v as i64))
            }
        }

        deserializer.deserialize_any(ProtocolVisitor { from_str, from_int })
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum WorkKind {
    #[default]
    Illustration,
    Manga,
    Novel,
}

impl WorkKind {
    pub fn is_novel(self) -> bool {
        matches!(self, Self::Novel)
    }

    pub fn as_content_type(self) -> Option<&'static str> {
        match self {
            Self::Illustration => Some("illust"),
            Self::Manga => Some("manga"),
            Self::Novel => None,
        }
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum SimpleWorkKind {
    #[default]
    Illustration,
    Novel,
}

impl SimpleWorkKind {
    pub fn is_novel(self) -> bool {
        matches!(self, Self::Novel)
    }
}

impl From<WorkKind> for SimpleWorkKind {
    fn from(kind: WorkKind) -> Self {
        match kind {
            WorkKind::Novel => Self::Novel,
            WorkKind::Illustration | WorkKind::Manga => Self::Illustration,
        }
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum PrivacyRestrict {
    #[default]
    Public,
    Private,
}

impl PrivacyRestrict {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::Public => "public",
            Self::Private => "private",
        }
    }

    pub fn from_protocol(value: &str) -> Self {
        if value.eq_ignore_ascii_case("private") {
            Self::Private
        } else {
            Self::Public
        }
    }

    fn from_int(value: i64) -> Self {
        if value == 1 { Self::Private } else { Self::Public }
    }
}

impl Serialize for PrivacyRestrict {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        protocol_serde::serialize_str(self.as_str(), serializer)
    }
}

impl<'de> Deserialize<'de> for PrivacyRestrict {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        protocol_serde::deserialize(deserializer, Self::from_protocol, Self::from_int)
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum RankMode {
    #[default]
    Day,
    Week,
    Month,
    DayMale,
    DayFemale,
    DayManga,
    WeekManga,
    MonthManga,
    WeekOriginal,
    WeekRookie,
    DayR18,
    DayMaleR18,
    DayFemaleR18,
    WeekR18,
    WeekR18G,
    DayAi,
    DayR18Ai,
    WeekAi,
    WeekAiR18,
}

impl RankMode {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::Day => "day",
            Self::Week => "week",
            Self::Month => "month",
            Self::DayMale => "day_male",
            Self::DayFemale => "day_female",
            Self::DayManga => "day_manga",
            Self::WeekManga => "week_manga",
            Self::MonthManga => "month_manga",
            Self::WeekOriginal => "week_original",
            Self::WeekRookie => "week_rookie",
            Self::DayR18 => "day_r18",
            Self::DayMaleR18 => "day_male_r18",
            Self::DayFemaleR18 => "day_female_r18",
            Self::WeekR18 => "week_r18",
            Self::WeekR18G => "week_r18g",
            Self::DayAi => "day_ai",
            Self::DayR18Ai => "day_r18_ai",
            Self::WeekAi => "week_ai",
            Self::WeekAiR18 => "week_ai_r18",
        }
    }

    pub fn supported_for(kind: SimpleWorkKind) -> Vec<Self> {
        match kind {
            SimpleWorkKind::Illustration => vec![
                Self::Day,
                Self::Week,
                Self::Month,
                Self::DayMale,
                Self::DayFemale,
                Self::DayManga,
                Self::WeekManga,
                Self::MonthManga,
                Self::WeekOriginal,
                Self::WeekRookie,
                Self::DayR18,
                Self::DayMaleR18,
                Self::DayFemaleR18,
                Self::WeekR18,
                Self::WeekR18G,
                Self::DayAi,
                Self::DayR18Ai,
            ],
            SimpleWorkKind::Novel => vec![
                Self::Day,
                Self::Week,
                Self::DayMale,
                Self::DayFemale,
                Self::WeekRookie,
                Self::DayR18,
                Self::DayMaleR18,
                Self::DayFemaleR18,
                Self::WeekR18,
                Self::WeekR18G,
                Self::WeekAi,
                Self::WeekAiR18,
            ],
        }
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum SortOrder {
    #[default]
    DateDesc,
    DateAsc,
    PopularDesc,
}

impl SortOrder {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::DateDesc => "date_desc",
            Self::DateAsc => "date_asc",
            Self::PopularDesc => "popular_desc",
        }
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum IllustSearchTarget {
    #[default]
    PartialMatchForTags,
    ExactMatchForTags,
    TitleAndCaption,
    Keyword,
}

impl IllustSearchTarget {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::PartialMatchForTags => "partial_match_for_tags",
            Self::ExactMatchForTags => "exact_match_for_tags",
            Self::TitleAndCaption => "title_and_caption",
            Self::Keyword => "keyword",
        }
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum NovelSearchTarget {
    #[default]
    PartialMatchForTags,
    ExactMatchForTags,
    Text,
    Keyword,
}

impl NovelSearchTarget {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::PartialMatchForTags => "partial_match_for_tags",
            Self::ExactMatchForTags => "exact_match_for_tags",
            Self::Text => "text",
            Self::Keyword => "keyword",
        }
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum IllustContentType {
    #[default]
    Illustration,
    Manga,
    Ugoira,
}

impl IllustContentType {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::Illustration => "illust",
            Self::Manga => "manga",
            Self::Ugoira => "ugoira",
        }
    }

    pub fn is_ugoira(self) -> bool {
        matches!(self, Self::Ugoira)
    }

    pub fn from_protocol(value: &str) -> Self {
        if value.eq_ignore_ascii_case("manga") {
            Self::Manga
        } else if value.eq_ignore_ascii_case("ugoira") {
            Self::Ugoira
        } else {
            Self::Illustration
        }
    }

    fn from_int(value: i64) -> Self {
        match value {
            1 => Self::Manga,
            2 => Self::Ugoira,
            _ => Self::Illustration,
        }
    }
}

impl Serialize for IllustContentType {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        protocol_serde::serialize_str(self.as_str(), serializer)
    }
}

impl<'de> Deserialize<'de> for IllustContentType {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        protocol_serde::deserialize(deserializer, Self::from_protocol, Self::from_int)
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq)]
pub enum RatioPattern {
    Landscape,
    Portrait,
    Square,
}

impl RatioPattern {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::Landscape => "horizontal",
            Self::Portrait => "vertical",
            Self::Square => "square",
        }
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq)]
pub enum NovelContentLengthOption {
    TextLength,
    WordCount,
    ReadingTime,
}

impl NovelContentLengthOption {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::TextLength => "text_length",
            Self::WordCount => "word_count",
            Self::ReadingTime => "reading_time",
        }
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum SpotlightCategory {
    #[default]
    All,
    Spotlight,
    Tutorial,
    Inspiration,
}

impl SpotlightCategory {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::All => "all",
            Self::Spotlight => "spotlight",
            Self::Tutorial => "tutorial",
            Self::Inspiration => "inspiration",
        }
    }

    pub fn from_protocol(value: &str) -> Self {
        if value.eq_ignore_ascii_case("all") {
            Self::All
        } else if value.eq_ignore_ascii_case("tutorial") {
            Self::Tutorial
        } else if value.eq_ignore_ascii_case("inspiration") {
            Self::Inspiration
        } else {
            Self::Spotlight
        }
    }

    fn from_int(value: i64) -> Self {
        match value {
            0 => Self::All,
            2 => Self::Tutorial,
            3 => Self::Inspiration,
            _ => Self::Spotlight,
        }
    }
}

impl Serialize for SpotlightCategory {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        protocol_serde::serialize_str(self.as_str(), serializer)
    }
}

impl<'de> Deserialize<'de> for SpotlightCategory {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        protocol_serde::deserialize(deserializer, Self::from_protocol, Self::from_int)
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum XRestrict {
    #[default]
    Ordinary,
    R18,
    R18G,
}

impl XRestrict {
    pub fn as_i32(self) -> i32 {
        match self {
            Self::Ordinary => 0,
            Self::R18 => 1,
            Self::R18G => 2,
        }
    }

    pub fn is_r18(self) -> bool {
        !matches!(self, Self::Ordinary)
    }

    pub fn is_r18g(self) -> bool {
        matches!(self, Self::R18G)
    }

    pub fn from_protocol(value: &str) -> Self {
        if value.eq_ignore_ascii_case("r18g") || value == "2" {
            Self::R18G
        } else if value.eq_ignore_ascii_case("r18") || value == "1" {
            Self::R18
        } else {
            Self::Ordinary
        }
    }

    fn from_int(value: i64) -> Self {
        match value {
            1 => Self::R18,
            2 => Self::R18G,
            _ => Self::Ordinary,
        }
    }
}

impl Serialize for XRestrict {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.serialize_i32(self.as_i32())
    }
}

impl<'de> Deserialize<'de> for XRestrict {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        protocol_serde::deserialize(deserializer, Self::from_protocol, Self::from_int)
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq)]
pub enum RestrictionFlag {
    R18,
    R18G,
    Sensitive,
}

impl RestrictionFlag {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::R18 => "r18",
            Self::R18G => "r18g",
            Self::Sensitive => "sensitive",
        }
    }

    pub fn from_protocol(value: &str) -> Option<Self> {
        if value.eq_ignore_ascii_case("r18g") {
            Some(Self::R18G)
        } else if value.eq_ignore_ascii_case("r18") {
            Some(Self::R18)
        } else if value.eq_ignore_ascii_case("sensitive") {
            Some(Self::Sensitive)
        } else {
            None
        }
    }
}

impl Serialize for RestrictionFlag {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        protocol_serde::serialize_str(self.as_str(), serializer)
    }
}

impl<'de> Deserialize<'de> for RestrictionFlag {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        let raw = String::deserialize(deserializer)?;
        Self::from_protocol(&raw).ok_or_else(|| serde::de::Error::custom("unknown restriction attribute"))
    }
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq)]
pub enum SafeRatingKind {
    NotSpecified,
    General,
    Sensitive,
    Questionable,
    Explicit,
    Guro,
}

#[uniffi::export]
pub fn get_safe_rating(x_restrict: XRestrict, attributes: Option<Vec<RestrictionFlag>>, sanity_level: i32) -> SafeRatingKind {
    safe_rating_kind(x_restrict, attributes.as_deref(), sanity_level)
}

pub fn safe_rating_kind(
    x_restrict: XRestrict,
    attributes: Option<&[RestrictionFlag]>,
    sanity_level: i32,
) -> SafeRatingKind {
    let attrs = attributes.unwrap_or(&[]);
    if x_restrict.is_r18g() || attrs.contains(&RestrictionFlag::R18G) {
        SafeRatingKind::Guro
    } else if x_restrict.is_r18() || attrs.contains(&RestrictionFlag::R18) {
        SafeRatingKind::Explicit
    } else if attrs.contains(&RestrictionFlag::Sensitive) {
        SafeRatingKind::Questionable
    } else {
        match sanity_level {
            6 => SafeRatingKind::Questionable,
            2 => SafeRatingKind::General,
            _ => SafeRatingKind::NotSpecified,
        }
    }
}

pub fn deserialize_restriction_attributes<'de, D: Deserializer<'de>>(
    deserializer: D,
) -> Result<Option<Vec<RestrictionFlag>>, D::Error> {
    let raw = Option::<Vec<String>>::deserialize(deserializer)?;
    Ok(raw.map(|items| {
        items
            .into_iter()
            .filter_map(|item| RestrictionFlag::from_protocol(&item))
            .collect()
    }))
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct QueryDate {
    pub year: i32,
    pub month: i32,
    pub day: i32,
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug, PartialEq)]
pub struct IllustrationSearchParams {
    pub word: String,
    pub search_target: IllustSearchTarget,
    pub sort: SortOrder,
    pub search_ai_type: Option<i32>,
    pub content_type: Option<IllustContentType>,
    pub ratio_pattern: Option<RatioPattern>,
    pub merge_plain_keyword_results: Option<bool>,
    pub include_translated_tag_results: Option<bool>,
    pub include_potential_violation_works: Option<bool>,
    pub start_date: Option<QueryDate>,
    pub end_date: Option<QueryDate>,
    pub width_min: Option<i32>,
    pub width_max: Option<i32>,
    pub height_min: Option<i32>,
    pub height_max: Option<i32>,
    pub tool: Option<String>,
}

impl Default for IllustrationSearchParams {
    fn default() -> Self {
        Self {
            word: String::new(),
            search_target: IllustSearchTarget::PartialMatchForTags,
            sort: SortOrder::DateDesc,
            search_ai_type: None,
            content_type: None,
            ratio_pattern: None,
            merge_plain_keyword_results: None,
            include_translated_tag_results: None,
            include_potential_violation_works: None,
            start_date: None,
            end_date: None,
            width_min: None,
            width_max: None,
            height_min: None,
            height_max: None,
            tool: None,
        }
    }
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug, PartialEq)]
pub struct NovelSearchParams {
    pub word: String,
    pub search_target: NovelSearchTarget,
    pub sort: SortOrder,
    pub search_ai_type: Option<i32>,
    pub lang: Option<String>,
    pub content_length_option: Option<NovelContentLengthOption>,
    pub content_length_min: Option<i32>,
    pub content_length_max: Option<i32>,
    pub is_original_only: Option<bool>,
    pub genre: Option<i32>,
    pub is_replaceable_only: Option<bool>,
    pub merge_plain_keyword_results: Option<bool>,
    pub include_translated_tag_results: Option<bool>,
    pub include_potential_violation_works: Option<bool>,
    pub start_date: Option<QueryDate>,
    pub end_date: Option<QueryDate>,
}

impl Default for NovelSearchParams {
    fn default() -> Self {
        Self {
            word: String::new(),
            search_target: NovelSearchTarget::PartialMatchForTags,
            sort: SortOrder::DateDesc,
            search_ai_type: None,
            lang: None,
            content_length_option: None,
            content_length_min: None,
            content_length_max: None,
            is_original_only: None,
            genre: None,
            is_replaceable_only: None,
            merge_plain_keyword_results: None,
            include_translated_tag_results: None,
            include_potential_violation_works: None,
            start_date: None,
            end_date: None,
        }
    }
}

impl QueryDate {
    pub fn format(&self) -> Result<String, MakoError> {
        let date = chrono::NaiveDate::from_ymd_opt(self.year, self.month as u32, self.day as u32).ok_or_else(
            || MakoError::InvalidParameter {
                message: format!(
                    "Invalid date: {:04}-{:02}-{:02}",
                    self.year, self.month, self.day
                ),
            },
        )?;
        Ok(date.format("%Y-%m-%d").to_string())
    }
}

pub fn ranking_max_date() -> QueryDate {
    let yesterday = chrono::Utc::now() - chrono::Duration::days(1);
    QueryDate {
        year: yesterday.year(),
        month: yesterday.month() as i32,
        day: yesterday.day() as i32,
    }
}

pub fn illustration_search_url(filter: &str, params: &IllustrationSearchParams) -> String {
    let mut url = format!(
        "{APP_API_BASE_URL}/v1/search/illust?filter={filter}&word={}&search_target={}&sort={}",
        url_encode(&params.word),
        url_encode(params.search_target.as_str()),
        url_encode(params.sort.as_str())
    );
    append_search_common(&mut url, params.search_ai_type, params.merge_plain_keyword_results, params.include_translated_tag_results, params.include_potential_violation_works, params.start_date.as_ref(), params.end_date.as_ref());
    if let Some(ct) = params.content_type {
        url.push_str(&format!("&content_type={}", url_encode(ct.as_str())));
    }
    if let Some(rp) = params.ratio_pattern {
        url.push_str(&format!("&ratio_pattern={}", url_encode(rp.as_str())));
    }
    if let Some(w_min) = params.width_min {
        url.push_str(&format!("&width_min={w_min}"));
    }
    if let Some(w_max) = params.width_max {
        url.push_str(&format!("&width_max={w_max}"));
    }
    if let Some(h_min) = params.height_min {
        url.push_str(&format!("&height_min={h_min}"));
    }
    if let Some(h_max) = params.height_max {
        url.push_str(&format!("&height_max={h_max}"));
    }
    if let Some(ref tool) = params.tool {
        url.push_str(&format!("&tool={}", url_encode(tool)));
    }
    url
}

pub fn novel_search_url(filter: &str, params: &NovelSearchParams) -> String {
    let mut url = format!(
        "{APP_API_BASE_URL}/v1/search/novel?filter={filter}&word={}&search_target={}&sort={}",
        url_encode(&params.word),
        url_encode(params.search_target.as_str()),
        url_encode(params.sort.as_str())
    );
    append_search_common(&mut url, params.search_ai_type, params.merge_plain_keyword_results, params.include_translated_tag_results, params.include_potential_violation_works, params.start_date.as_ref(), params.end_date.as_ref());
    if let Some(ref lang) = params.lang {
        url.push_str(&format!("&lang={}", url_encode(lang)));
    }
    if let Some(opt) = params.content_length_option {
        let key = opt.as_str();
        if let Some(c_min) = params.content_length_min {
            url.push_str(&format!("&{key}_min={c_min}"));
        }
        if let Some(c_max) = params.content_length_max {
            url.push_str(&format!("&{key}_max={c_max}"));
        }
    }
    if let Some(orig) = params.is_original_only {
        url.push_str(&format!("&is_original_only={orig}"));
    }
    if let Some(genre) = params.genre {
        url.push_str(&format!("&genre={genre}"));
    }
    if let Some(rep) = params.is_replaceable_only {
        url.push_str(&format!("&is_replaceable_only={rep}"));
    }
    url
}

fn append_search_common(
    url: &mut String,
    search_ai_type: Option<i32>,
    merge_plain_keyword_results: Option<bool>,
    include_translated_tag_results: Option<bool>,
    include_potential_violation_works: Option<bool>,
    start_date: Option<&QueryDate>,
    end_date: Option<&QueryDate>,
) {
    if let Some(ai) = search_ai_type {
        url.push_str(&format!("&search_ai_type={ai}"));
    }
    if let Some(v) = merge_plain_keyword_results {
        url.push_str(&format!("&merge_plain_keyword_results={v}"));
    }
    if let Some(v) = include_translated_tag_results {
        url.push_str(&format!("&include_translated_tag_results={v}"));
    }
    if let Some(v) = include_potential_violation_works {
        url.push_str(&format!("&include_potential_violation_works={v}"));
    }
    if let Some(sd) = start_date {
        if let Ok(formatted) = sd.format() {
            url.push_str(&format!("&start_date={}", url_encode(&formatted)));
        }
    }
    if let Some(ed) = end_date {
        if let Ok(formatted) = ed.format() {
            url.push_str(&format!("&end_date={}", url_encode(&formatted)));
        }
    }
}

pub fn work_recommended_url(filter: &str, kind: WorkKind) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v1/novel/recommended?include_ranking_novels=true&include_privacy_policy=true&filter={filter}")
    } else {
        let content_type = if matches!(kind, WorkKind::Manga) {
            "&content_type=manga"
        } else {
            ""
        };
        format!("{APP_API_BASE_URL}/v1/illust/recommended?include_ranking_illusts=true&include_privacy_policy=true&filter={filter}{content_type}")
    }
}

pub fn work_new_url(filter: &str, kind: WorkKind) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v1/novel/new?filter={filter}")
    } else {
        let content_type = kind.as_content_type().unwrap_or("illust");
        format!("{APP_API_BASE_URL}/v1/illust/new?content_type={content_type}&filter={filter}")
    }
}

pub fn work_ranking_url(
    filter: &str,
    kind: SimpleWorkKind,
    mode: RankMode,
    date: Option<&QueryDate>,
) -> Result<String, MakoError> {
    let mode_str = url_encode(mode.as_str());
    let path = if kind.is_novel() {
        "/v1/novel/ranking"
    } else {
        "/v1/illust/ranking"
    };
    let mut url = format!("{APP_API_BASE_URL}{path}?mode={mode_str}&filter={filter}");
    if let Some(d) = date {
        url.push_str(&format!("&date={}", url_encode(&d.format()?)));
    }
    Ok(url)
}

pub fn work_bookmarks_url(
    kind: SimpleWorkKind,
    user_id: i64,
    restrict: PrivacyRestrict,
    tag: Option<&str>,
) -> String {
    let path = if kind.is_novel() {
        "/v1/user/bookmarks/novel"
    } else {
        "/v1/user/bookmarks/illust"
    };
    let mut url = format!(
        "{APP_API_BASE_URL}{path}?user_id={user_id}&restrict={}",
        url_encode(restrict.as_str())
    );
    if let Some(t) = tag.filter(|t| !t.is_empty()) {
        url.push_str(&format!("&tag={}", url_encode(t)));
    }
    url
}

pub fn work_posted_url(filter: &str, kind: WorkKind, user_id: i64) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v1/user/novels?user_id={user_id}&filter={filter}")
    } else {
        let content_type = kind.as_content_type().unwrap_or("illust");
        format!("{APP_API_BASE_URL}/v1/user/illusts?user_id={user_id}&type={content_type}&filter={filter}")
    }
}

pub fn work_following_url(kind: SimpleWorkKind, restrict: PrivacyRestrict) -> String {
    let restrict_str = url_encode(restrict.as_str());
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v1/novel/follow?restrict={restrict_str}")
    } else {
        format!("{APP_API_BASE_URL}/v2/illust/follow?restrict={restrict_str}")
    }
}

pub fn work_related_url(filter: &str, kind: SimpleWorkKind, id: i64) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v1/novel/related?novel_id={id}&filter={filter}")
    } else {
        format!("{APP_API_BASE_URL}/v2/illust/related?illust_id={id}&filter={filter}")
    }
}

pub fn work_mypixiv_url(kind: SimpleWorkKind) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v1/novel/mypixiv")
    } else {
        format!("{APP_API_BASE_URL}/v1/illust/mypixiv")
    }
}

pub fn work_series_url(filter: &str, kind: SimpleWorkKind, series_id: i64) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v2/novel/series?series_id={series_id}&filter={filter}")
    } else {
        format!("{APP_API_BASE_URL}/v1/illust/series?illust_series_id={series_id}&filter={filter}")
    }
}

pub fn work_comments_url(filter: &str, kind: SimpleWorkKind, work_id: i64) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v3/novel/comments?novel_id={work_id}&filter={filter}")
    } else {
        format!("{APP_API_BASE_URL}/v3/illust/comments?illust_id={work_id}&filter={filter}")
    }
}

pub fn work_comment_replies_url(filter: &str, kind: SimpleWorkKind, comment_id: i64) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v2/novel/comment/replies?comment_id={comment_id}&filter={filter}")
    } else {
        format!("{APP_API_BASE_URL}/v2/illust/comment/replies?comment_id={comment_id}&filter={filter}")
    }
}

pub fn spotlight_url(filter: &str, category: SpotlightCategory) -> String {
    format!(
        "{APP_API_BASE_URL}/v1/spotlight/articles?category={}&filter={filter}",
        url_encode(category.as_str())
    )
}

pub fn browsing_history_url(kind: SimpleWorkKind) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v1/novel/browsing-history")
    } else {
        format!("{APP_API_BASE_URL}/v1/illust/browsing-history")
    }
}

pub fn search_bookmark_works_url(
    kind: SimpleWorkKind,
    restrict: PrivacyRestrict,
    bookmark_tag: Option<&str>,
    work_tag: Option<&str>,
    bookmark_period: Option<&str>,
    order: Option<&str>,
) -> String {
    let mut query = vec![format!("restrict={}", url_encode(restrict.as_str()))];
    if let Some(bt) = bookmark_tag.filter(|t| !t.is_empty()) {
        query.push(format!("tag={}", url_encode(bt)));
    }
    if let Some(wt) = work_tag.filter(|t| !t.is_empty()) {
        query.push(format!("word={}", url_encode(wt)));
    }
    if let Some(bp) = bookmark_period.filter(|t| !t.is_empty()) {
        query.push(format!("bookmark_period={bp}"));
    }
    if let Some(ord) = order.filter(|t| !t.is_empty()) {
        query.push(format!("order={ord}"));
    }
    let qs = query.join("&");
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v1/novel/bookmarks_ranges?{qs}")
    } else {
        format!("{APP_API_BASE_URL}/v1/illust/bookmarks_ranges?{qs}")
    }
}

pub fn user_following_url(user_id: i64, restrict: PrivacyRestrict) -> String {
    format!(
        "{APP_API_BASE_URL}/v1/user/following?user_id={user_id}&restrict={}",
        url_encode(restrict.as_str())
    )
}

pub fn work_bookmark_tags_url(
    filter: &str,
    kind: SimpleWorkKind,
    user_id: i64,
    restrict: PrivacyRestrict,
) -> String {
    let path = if kind.is_novel() { "novel" } else { "illust" };
    format!(
        "{APP_API_BASE_URL}/v1/user/bookmark-tags/{path}?user_id={user_id}&restrict={}&filter={filter}",
        url_encode(restrict.as_str())
    )
}

pub fn work_trending_tags_url(filter: &str, kind: SimpleWorkKind) -> String {
    let path = if kind.is_novel() { "novel" } else { "illust" };
    format!("{APP_API_BASE_URL}/v1/trending-tags/{path}?filter={filter}")
}

pub fn work_series_watchlist_url(kind: SimpleWorkKind) -> String {
    let path = if kind.is_novel() { "novel" } else { "manga" };
    format!("{APP_API_BASE_URL}/v1/watchlist/{path}")
}

pub fn bookmark_detail_url(kind: SimpleWorkKind, id: i64) -> String {
    if kind.is_novel() {
        format!("{APP_API_BASE_URL}/v2/novel/bookmark/detail?novel_id={id}")
    } else {
        format!("{APP_API_BASE_URL}/v2/illust/bookmark/detail?illust_id={id}")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn illustration_search_url_uses_typed_protocol_fields() {
        let params = IllustrationSearchParams {
            word: "cat".into(),
            search_target: IllustSearchTarget::ExactMatchForTags,
            sort: SortOrder::DateAsc,
            content_type: Some(IllustContentType::Manga),
            ratio_pattern: Some(RatioPattern::Portrait),
            start_date: Some(QueryDate { year: 2026, month: 10, day: 1 }),
            ..IllustrationSearchParams::default()
        };
        let url = illustration_search_url("for_android", &params);
        assert!(url.contains("/v1/search/illust?"));
        assert!(url.contains("word=cat"));
        assert!(url.contains("search_target=exact_match_for_tags"));
        assert!(url.contains("sort=date_asc"));
        assert!(url.contains("content_type=manga"));
        assert!(url.contains("ratio_pattern=vertical"));
        assert!(url.contains("start_date=2026-10-01"));
        assert!(!url.contains("yyyy-MM-dd"));
    }

    #[test]
    fn novel_search_url_encodes_content_length_option() {
        let params = NovelSearchParams {
            word: "test".into(),
            search_target: NovelSearchTarget::Text,
            sort: SortOrder::PopularDesc,
            content_length_option: Some(NovelContentLengthOption::WordCount),
            content_length_min: Some(100),
            content_length_max: Some(500),
            ..NovelSearchParams::default()
        };
        let url = novel_search_url("for_android", &params);
        assert!(url.contains("/v1/search/novel?"));
        assert!(url.contains("search_target=text"));
        assert!(url.contains("sort=popular_desc"));
        assert!(url.contains("word_count_min=100"));
        assert!(url.contains("word_count_max=500"));
    }

    #[test]
    fn work_kind_dispatch_picks_illust_or_novel_paths() {
        let rec_illust = work_recommended_url("for_android", WorkKind::Illustration);
        assert!(rec_illust.contains("/v1/illust/recommended?"));
        assert!(!rec_illust.contains("content_type=manga"));

        let rec_manga = work_recommended_url("for_android", WorkKind::Manga);
        assert!(rec_manga.contains("content_type=manga"));

        let rec_novel = work_recommended_url("for_android", WorkKind::Novel);
        assert!(rec_novel.contains("/v1/novel/recommended?"));

        let posted_illust = work_posted_url("for_android", WorkKind::Illustration, 42);
        assert!(posted_illust.contains("/v1/user/illusts?"));
        assert!(posted_illust.contains("type=illust"));

        let posted_manga = work_posted_url("for_android", WorkKind::Manga, 42);
        assert!(posted_manga.contains("type=manga"));

        let posted_novel = work_posted_url("for_android", WorkKind::Novel, 42);
        assert!(posted_novel.contains("/v1/user/novels?"));
    }

    #[test]
    fn ranking_url_formats_query_date_and_mode() {
        let url = work_ranking_url(
            "for_android",
            SimpleWorkKind::Illustration,
            RankMode::DayMaleR18,
            Some(&QueryDate { year: 2026, month: 10, day: 9 }),
        )
        .unwrap();
        assert!(url.contains("/v1/illust/ranking?"));
        assert!(url.contains("mode=day_male_r18"));
        assert!(url.contains("date=2026-10-09"));
    }

    #[test]
    fn ranking_rejects_invalid_query_date() {
        let err = work_ranking_url(
            "for_android",
            SimpleWorkKind::Novel,
            RankMode::Day,
            Some(&QueryDate { year: 2026, month: 13, day: 1 }),
        )
        .unwrap_err();
        match err {
            MakoError::InvalidParameter { message } => assert!(message.contains("2026-13-01")),
            other => panic!("unexpected error: {other:?}"),
        }
    }

    #[test]
    fn bookmarks_and_following_use_privacy_restrict() {
        let bookmarks = work_bookmarks_url(SimpleWorkKind::Novel, 7, PrivacyRestrict::Private, Some("tag"));
        assert!(bookmarks.contains("/v1/user/bookmarks/novel?"));
        assert!(bookmarks.contains("restrict=private"));
        assert!(bookmarks.contains("tag=tag"));

        let following = work_following_url(SimpleWorkKind::Illustration, PrivacyRestrict::Public);
        assert!(following.contains("/v2/illust/follow?"));
        assert!(following.contains("restrict=public"));
    }

    #[test]
    fn rank_modes_for_matches_legacy_support_lists() {
        let illust = RankMode::supported_for(SimpleWorkKind::Illustration);
        assert!(!illust.contains(&RankMode::WeekAi));
        assert!(!illust.contains(&RankMode::WeekAiR18));
        assert!(illust.contains(&RankMode::Month));
        assert!(illust.contains(&RankMode::DayAi));

        let novel = RankMode::supported_for(SimpleWorkKind::Novel);
        assert!(novel.contains(&RankMode::WeekAi));
        assert!(!novel.contains(&RankMode::Month));
        assert!(!novel.contains(&RankMode::DayManga));
        assert!(!novel.contains(&RankMode::DayAi));
    }

    #[test]
    fn leftover_catalog_urls_use_typed_kind_and_restrict() {
        let tags = work_bookmark_tags_url(
            "for_android",
            SimpleWorkKind::Novel,
            7,
            PrivacyRestrict::Private,
        );
        assert!(tags.contains("/v1/user/bookmark-tags/novel?"));
        assert!(tags.contains("restrict=private"));

        let following = user_following_url(9, PrivacyRestrict::Public);
        assert!(following.contains("/v1/user/following?"));
        assert!(following.contains("restrict=public"));

        let trending = work_trending_tags_url("for_android", SimpleWorkKind::Illustration);
        assert!(trending.contains("/v1/trending-tags/illust?"));

        let watchlist = work_series_watchlist_url(SimpleWorkKind::Novel);
        assert!(watchlist.contains("/v1/watchlist/novel"));

        let detail = bookmark_detail_url(SimpleWorkKind::Illustration, 11);
        assert!(detail.contains("/v2/illust/bookmark/detail?illust_id=11"));
    }

    #[test]
    fn ranking_max_date_is_calendar_fields() {
        let date = ranking_max_date();
        assert!(date.format().is_ok());
        let formatted = date.format().unwrap();
        assert_eq!(formatted.len(), 10);
        assert_eq!(&formatted[4..5], "-");
        assert_eq!(&formatted[7..8], "-");
    }

    #[test]
    fn response_protocol_strings_decode_to_catalog_enums() {
        assert_eq!(PrivacyRestrict::from_protocol("private"), PrivacyRestrict::Private);
        assert_eq!(PrivacyRestrict::from_protocol("PUBLIC"), PrivacyRestrict::Public);
        assert_eq!(IllustContentType::from_protocol("ugoira"), IllustContentType::Ugoira);
        assert_eq!(IllustContentType::from_protocol("manga"), IllustContentType::Manga);
        assert_eq!(IllustContentType::from_protocol("illust"), IllustContentType::Illustration);
        assert_eq!(SpotlightCategory::from_protocol("tutorial"), SpotlightCategory::Tutorial);
        assert_eq!(SpotlightCategory::from_protocol("all"), SpotlightCategory::All);
        assert_eq!(SpotlightCategory::from_protocol("unknown"), SpotlightCategory::Spotlight);

        let restrict: PrivacyRestrict = serde_json::from_str("\"private\"").unwrap();
        assert_eq!(restrict, PrivacyRestrict::Private);
        let kind: IllustContentType = serde_json::from_str("1").unwrap();
        assert_eq!(kind, IllustContentType::Manga);
        assert_eq!(
            serde_json::to_string(&IllustContentType::Ugoira).unwrap(),
            "\"ugoira\""
        );
    }

    #[test]
    fn x_restrict_and_attributes_decode_to_safe_rating() {
        assert_eq!(XRestrict::from_int(1), XRestrict::R18);
        assert_eq!(XRestrict::from_protocol("r18g"), XRestrict::R18G);
        let parsed: XRestrict = serde_json::from_str("2").unwrap();
        assert_eq!(parsed, XRestrict::R18G);
        assert_eq!(serde_json::to_string(&XRestrict::R18).unwrap(), "1");

        let flags = deserialize_restriction_attributes(serde_json::Value::Array(vec![
            serde_json::Value::String("r18".into()),
            serde_json::Value::String("unknown".into()),
            serde_json::Value::String("sensitive".into()),
        ]))
        .unwrap()
        .unwrap();
        assert_eq!(
            flags,
            vec![RestrictionFlag::R18, RestrictionFlag::Sensitive]
        );

        assert_eq!(
            safe_rating_kind(XRestrict::Ordinary, Some(&[RestrictionFlag::R18G]), 0),
            SafeRatingKind::Guro
        );
        assert_eq!(
            safe_rating_kind(XRestrict::Ordinary, None, 6),
            SafeRatingKind::Questionable
        );
        assert_eq!(
            safe_rating_kind(XRestrict::Ordinary, None, 2),
            SafeRatingKind::General
        );
    }
}

#[uniffi::export]
pub fn supported_rank_modes(kind: SimpleWorkKind) -> Vec<RankMode> {
    RankMode::supported_for(kind)
}
