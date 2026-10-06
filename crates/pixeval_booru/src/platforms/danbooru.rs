// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use pixeval_maho::MahoHttpClient;
use serde::Deserialize;

use crate::error::BooruError;
use crate::models::{
    BooruPlatform, BooruPost, BooruPostPreview, BooruSearchResult, BooruTag, normalize_rating,
};

const BASE_URL: &str = "https://danbooru.donmai.us";
const USER_AGENT: &str = "gdl/1.24.5";

#[derive(Deserialize, Debug)]
#[allow(dead_code)]
struct DanbooruPostRaw {
    pub id: i64,
    #[serde(default)]
    pub created_at: Option<String>,
    #[serde(default)]
    pub uploader_id: Option<i64>,
    #[serde(default)]
    pub uploader: Option<DanbooruUploaderRaw>,
    #[serde(default)]
    pub source: Option<String>,
    #[serde(default)]
    pub file_ext: Option<String>,
    #[serde(default)]
    pub md5: Option<String>,
    #[serde(default)]
    pub rating: Option<String>,
    #[serde(default)]
    pub image_width: Option<u32>,
    #[serde(default)]
    pub image_height: Option<u32>,
    #[serde(default)]
    pub parent_id: Option<i64>,
    #[serde(default)]
    pub file_size: Option<u64>,
    #[serde(default)]
    pub is_deleted: Option<bool>,
    #[serde(default)]
    pub is_banned: Option<bool>,
    #[serde(default)]
    pub file_url: Option<String>,
    #[serde(default)]
    pub large_file_url: Option<String>,
    #[serde(default)]
    pub preview_file_url: Option<String>,
    #[serde(default)]
    pub media_metadata: Option<DanbooruMediaMetadataRaw>,
    #[serde(default)]
    pub children: Option<Vec<DanbooruChildRaw>>,
    #[serde(default)]
    pub score: Option<i32>,
    #[serde(default)]
    pub tag_string_artist: Option<String>,
    #[serde(default)]
    pub tag_string_character: Option<String>,
    #[serde(default)]
    pub tag_string_copyright: Option<String>,
    #[serde(default)]
    pub tag_string_general: Option<String>,
    #[serde(default)]
    pub tag_string_meta: Option<String>,
    #[serde(default)]
    pub tag_string: Option<String>,
}

#[derive(Deserialize, Debug)]
#[allow(dead_code)]
struct DanbooruUploaderRaw {
    #[serde(default)]
    pub id: Option<i64>,
    #[serde(default)]
    pub name: Option<String>,
}

#[derive(Deserialize, Debug)]
#[allow(dead_code)]
struct DanbooruMediaMetadataRaw {
    #[serde(default)]
    pub metadata: Option<serde_json::Value>,
}

#[derive(Deserialize, Debug)]
#[allow(dead_code)]
struct DanbooruChildRaw {
    pub id: i64,
}

#[derive(Deserialize, Debug)]
#[allow(dead_code)]
struct DanbooruPreviewRaw {
    pub id: i64,
    #[serde(default)]
    pub md5: Option<String>,
    #[serde(default)]
    pub tag_string: Option<String>,
    #[serde(default)]
    pub is_banned: Option<bool>,
    #[serde(default)]
    pub is_deleted: Option<bool>,
}

fn map_danbooru_post(raw: DanbooruPostRaw) -> BooruPost {
    let mut tags = Vec::new();
    let collect_tags = |target: &mut Vec<BooruTag>, tag_str: Option<&str>, tag_type: &str| {
        if let Some(s) = tag_str {
            for t in s.split_whitespace() {
                if !t.is_empty() {
                    target.push(BooruTag {
                        tag_type: tag_type.to_string(),
                        name: t.replace('_', " "),
                    });
                }
            }
        }
    };

    collect_tags(&mut tags, raw.tag_string_artist.as_deref(), "artist");
    collect_tags(&mut tags, raw.tag_string_character.as_deref(), "character");
    collect_tags(&mut tags, raw.tag_string_copyright.as_deref(), "copyright");
    collect_tags(&mut tags, raw.tag_string_general.as_deref(), "general");
    collect_tags(&mut tags, raw.tag_string_meta.as_deref(), "meta");

    let uploader_name = raw
        .uploader
        .and_then(|u| u.name)
        .map(|s| s.replace('_', " "))
        .unwrap_or_else(|| "uploader".to_string());

    let file_ext = raw.file_ext.unwrap_or_default();
    let ugoira_frame_delays = if file_ext == "zip" {
        raw.media_metadata
            .and_then(|m| m.metadata)
            .and_then(|meta| meta.get("Ugoira:FrameDelays").cloned())
            .and_then(|delays| serde_json::from_value::<Vec<i32>>(delays).ok())
    } else {
        None
    };

    let original_url = raw
        .file_url
        .or_else(|| raw.large_file_url.clone())
        .or_else(|| raw.preview_file_url.clone())
        .unwrap_or_default();

    BooruPost {
        id: raw.id.to_string(),
        md5: raw.md5.unwrap_or_default(),
        platform: BooruPlatform::Danbooru,
        original_url,
        sample_url: raw.large_file_url,
        preview_url: raw.preview_file_url,
        width: raw.image_width.unwrap_or(0),
        height: raw.image_height.unwrap_or(0),
        byte_size: raw.file_size.unwrap_or(0),
        file_ext,
        created_at: raw.created_at.unwrap_or_default(),
        uploader_id: raw.uploader_id.map(|id| id.to_string()).unwrap_or_default(),
        uploader_name,
        source: raw.source,
        rating: normalize_rating(raw.rating.as_deref().unwrap_or("general")),
        tags,
        parent_id: raw.parent_id.map(|id| id.to_string()),
        has_children: raw.children.map(|c| !c.is_empty()).unwrap_or(false),
        score: raw.score.unwrap_or(0),
        is_banned: raw.is_banned.unwrap_or(false),
        is_deleted: raw.is_deleted.unwrap_or(false),
        ugoira_frame_delays,
    }
}

pub async fn get_post(client: &MahoHttpClient, post_id: &str) -> Result<BooruPost, BooruError> {
    let url = format!("{BASE_URL}/posts/{post_id}.json");
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    let status = resp.status();
    if status.as_u16() == 404 {
        return Err(BooruError::PostNotFound {
            platform: "danbooru".to_string(),
            id: post_id.to_string(),
        });
    }
    if !status.is_success() {
        return Err(BooruError::ApiError {
            platform: "danbooru".to_string(),
            code: status.as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let raw: DanbooruPostRaw = resp.json().await?;
    Ok(map_danbooru_post(raw))
}

pub async fn get_post_by_md5(
    client: &MahoHttpClient,
    md5: &str,
) -> Result<Option<BooruPost>, BooruError> {
    let url = format!("{BASE_URL}/posts.json?tags=md5:{md5}");
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    if !resp.status().is_success() {
        return Ok(None);
    }

    let list: Vec<DanbooruPostRaw> = resp.json().await?;
    Ok(list.into_iter().next().map(map_danbooru_post))
}

pub async fn search(
    client: &MahoHttpClient,
    tags: &str,
    page: u32,
) -> Result<BooruSearchResult, BooruError> {
    let url = format!(
        "{BASE_URL}/posts.json?tags={}&page={page}&only=id,md5,tag_string,is_banned,is_deleted",
        urlencoding(tags)
    );
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    if !resp.status().is_success() {
        return Err(BooruError::ApiError {
            platform: "danbooru".to_string(),
            code: resp.status().as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let list: Vec<DanbooruPreviewRaw> = resp.json().await?;
    let results = list
        .into_iter()
        .map(|p| BooruPostPreview {
            id: p.id.to_string(),
            md5: p.md5,
            tag_string: p.tag_string.unwrap_or_default(),
            is_banned: p.is_banned.unwrap_or(false),
            is_deleted: p.is_deleted.unwrap_or(false),
        })
        .collect();

    Ok(BooruSearchResult {
        results,
        search_tags: tags.to_string(),
        page_number: page,
    })
}

pub async fn post_favorite(
    client: &MahoHttpClient,
    post_id: &str,
    favorite: bool,
) -> Result<bool, BooruError> {
    if !favorite {
        return Err(BooruError::NotSupported {
            message: "Danbooru API does not support unfavorite endpoint directly".to_string(),
        });
    }
    let url = format!("{BASE_URL}/favorites.json?post_id={post_id}");
    let resp = client
        .post(&url)
        .header("User-Agent", USER_AGENT)
        .send()
        .await?;
    Ok(resp.status().is_success())
}

fn urlencoding(input: &str) -> String {
    url::form_urlencoded::byte_serialize(input.as_bytes()).collect()
}
