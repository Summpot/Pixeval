// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use serde::{Deserialize, Serialize};

#[derive(uniffi::Enum, Clone, Copy, Debug, Default, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum BooruPlatform {
    #[default]
    Danbooru,
    Gelbooru,
    Yandere,
    Sankaku,
    Rule34,
}

impl BooruPlatform {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Danbooru => "danbooru",
            Self::Gelbooru => "gelbooru",
            Self::Yandere => "yandere",
            Self::Sankaku => "sankaku",
            Self::Rule34 => "rule34",
        }
    }

    pub fn from_str_name(s: &str) -> Option<Self> {
        match s.to_ascii_lowercase().as_str() {
            "danbooru" => Some(Self::Danbooru),
            "gelbooru" => Some(Self::Gelbooru),
            "yandere" | "yande.re" => Some(Self::Yandere),
            "sankaku" | "sankakucomplex" => Some(Self::Sankaku),
            "rule34" | "rule34.xxx" => Some(Self::Rule34),
            _ => None,
        }
    }
}

#[derive(uniffi::Record, Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
pub struct BooruTag {
    pub tag_type: String,
    pub name: String,
}

#[derive(uniffi::Record, Clone, Debug, Default, Serialize, Deserialize)]
pub struct BooruPost {
    pub id: String,
    pub md5: String,
    pub platform: BooruPlatform,
    pub original_url: String,
    pub sample_url: Option<String>,
    pub preview_url: Option<String>,
    pub width: u32,
    pub height: u32,
    pub byte_size: u64,
    pub file_ext: String,
    pub created_at: String,
    pub uploader_id: String,
    pub uploader_name: String,
    pub source: Option<String>,
    pub rating: String,
    pub tags: Vec<BooruTag>,
    pub parent_id: Option<String>,
    pub has_children: bool,
    pub score: i32,
    pub is_banned: bool,
    pub is_deleted: bool,
    pub ugoira_frame_delays: Option<Vec<i32>>,
}

#[derive(uniffi::Record, Clone, Debug, Default, Serialize, Deserialize)]
pub struct BooruPostPreview {
    pub id: String,
    pub md5: Option<String>,
    pub tag_string: String,
    pub is_banned: bool,
    pub is_deleted: bool,
}

#[derive(uniffi::Record, Clone, Debug, Default, Serialize, Deserialize)]
pub struct BooruSearchResult {
    pub results: Vec<BooruPostPreview>,
    pub search_tags: String,
    pub page_number: u32,
}

#[derive(uniffi::Record, Clone, Debug, Default, Serialize, Deserialize)]
pub struct BooruFavoriteResult {
    pub success: bool,
}

pub fn normalize_rating(r: &str) -> String {
    let lower = r.to_ascii_lowercase();
    match lower.trim() {
        "s" | "general" | "safe" | "g" => "general".to_string(),
        "sensitive" => "sensitive".to_string(),
        "q" | "questionable" => "questionable".to_string(),
        "e" | "explicit" => "explicit".to_string(),
        _ => "general".to_string(),
    }
}
