// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use chrono::DateTime;
use pixeval_maho::MahoHttpClient;
use serde::Deserialize;

use crate::error::BooruError;
use crate::models::{
    BooruPlatform, BooruPost, BooruPostPreview, BooruSearchResult, BooruTag, normalize_rating,
};

const BASE_URL: &str = "https://yande.re";
const USER_AGENT: &str = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36";

#[derive(Deserialize, Debug)]
struct YanderePostRaw {
    pub id: i64,
    #[serde(default)]
    pub tags: Option<String>,
    #[serde(default)]
    pub created_at: Option<i64>,
    #[serde(default)]
    pub creator_id: Option<i64>,
    #[serde(default)]
    pub author: Option<String>,
    #[serde(default)]
    pub source: Option<String>,
    #[serde(default)]
    pub md5: Option<String>,
    #[serde(default)]
    pub file_size: Option<u64>,
    #[serde(default)]
    pub file_url: Option<String>,
    #[serde(default)]
    pub sample_url: Option<String>,
    #[serde(default)]
    pub preview_url: Option<String>,
    #[serde(default)]
    pub rating: Option<String>,
    #[serde(default)]
    pub parent_id: Option<i64>,
    #[serde(default)]
    pub has_children: Option<bool>,
    #[serde(default)]
    pub width: Option<u32>,
    #[serde(default)]
    pub height: Option<u32>,
    #[serde(default)]
    pub score: Option<i32>,
}

fn map_yandere_post(raw: YanderePostRaw) -> BooruPost {
    let mut tags = Vec::new();
    if let Some(t_str) = raw.tags.as_deref() {
        for t in t_str.split_whitespace() {
            if !t.is_empty() {
                tags.push(BooruTag {
                    tag_type: "general".to_string(),
                    name: t.replace('_', " "),
                });
            }
        }
    }

    let created_at = raw
        .created_at
        .and_then(|ts| DateTime::from_timestamp(ts, 0))
        .map(|dt| dt.to_rfc3339())
        .unwrap_or_default();

    let file_url = raw
        .file_url
        .or_else(|| raw.sample_url.clone())
        .or_else(|| raw.preview_url.clone())
        .unwrap_or_default();

    let file_ext = file_url
        .rsplit('.')
        .next()
        .map(|s| s.to_string())
        .unwrap_or_default();

    let parent_id = match raw.parent_id {
        Some(p) if p > 0 => Some(p.to_string()),
        _ => None,
    };

    BooruPost {
        id: raw.id.to_string(),
        md5: raw.md5.unwrap_or_default(),
        platform: BooruPlatform::Yandere,
        original_url: file_url,
        sample_url: raw.sample_url,
        preview_url: raw.preview_url,
        width: raw.width.unwrap_or(0),
        height: raw.height.unwrap_or(0),
        byte_size: raw.file_size.unwrap_or(0),
        file_ext,
        created_at,
        uploader_id: raw.creator_id.map(|id| id.to_string()).unwrap_or_default(),
        uploader_name: raw.author.unwrap_or_else(|| "uploader".to_string()),
        source: raw.source,
        rating: normalize_rating(raw.rating.as_deref().unwrap_or("general")),
        tags,
        parent_id,
        has_children: raw.has_children.unwrap_or(false),
        score: raw.score.unwrap_or(0),
        is_banned: false,
        is_deleted: false,
        ugoira_frame_delays: None,
    }
}

pub async fn get_post(client: &MahoHttpClient, post_id: &str) -> Result<BooruPost, BooruError> {
    let url = format!("{BASE_URL}/post/index.json?tags=id:{post_id}");
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    if !resp.status().is_success() {
        return Err(BooruError::ApiError {
            platform: "yandere".to_string(),
            code: resp.status().as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let posts: Vec<YanderePostRaw> = resp.json().await?;
    let post = posts.into_iter().next().ok_or_else(|| BooruError::PostNotFound {
        platform: "yandere".to_string(),
        id: post_id.to_string(),
    })?;

    Ok(map_yandere_post(post))
}

pub async fn get_post_by_md5(
    client: &MahoHttpClient,
    md5: &str,
) -> Result<Option<BooruPost>, BooruError> {
    let url = format!("{BASE_URL}/post.json?tags=md5:{md5}%20holds:all");
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    if !resp.status().is_success() {
        return Ok(None);
    }

    let posts: Vec<YanderePostRaw> = resp.json().await?;
    Ok(posts.into_iter().next().map(map_yandere_post))
}

pub async fn search(
    client: &MahoHttpClient,
    tags: &str,
    page: u32,
) -> Result<BooruSearchResult, BooruError> {
    let url = format!("{BASE_URL}/post.json?tags={}&page={page}", urlencoding(tags));
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    if !resp.status().is_success() {
        return Err(BooruError::ApiError {
            platform: "yandere".to_string(),
            code: resp.status().as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let posts: Vec<YanderePostRaw> = resp.json().await?;
    let results = posts
        .into_iter()
        .map(|p| BooruPostPreview {
            id: p.id.to_string(),
            md5: p.md5,
            tag_string: p.tags.unwrap_or_default(),
            is_banned: false,
            is_deleted: false,
        })
        .collect();

    Ok(BooruSearchResult {
        results,
        search_tags: tags.to_string(),
        page_number: page,
    })
}

fn urlencoding(input: &str) -> String {
    url::form_urlencoded::byte_serialize(input.as_bytes()).collect()
}
