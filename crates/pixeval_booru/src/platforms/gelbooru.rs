// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use pixeval_maho::MahoHttpClient;
use serde::Deserialize;

use crate::error::BooruError;
use crate::models::{
    BooruPlatform, BooruPost, BooruPostPreview, BooruSearchResult, BooruTag, normalize_rating,
};

const BASE_URL: &str = "https://gelbooru.com";

#[derive(Deserialize, Debug)]
struct GelbooruResponseRaw {
    #[serde(default)]
    pub post: Option<Vec<GelbooruPostRaw>>,
}

#[derive(Deserialize, Debug)]
struct GelbooruPostRaw {
    pub id: i64,
    #[serde(default)]
    pub created_at: Option<String>,
    #[serde(default)]
    pub score: Option<i32>,
    #[serde(default)]
    pub width: Option<u32>,
    #[serde(default)]
    pub height: Option<u32>,
    #[serde(default)]
    pub md5: Option<String>,
    #[serde(default)]
    pub rating: Option<String>,
    #[serde(default)]
    pub source: Option<String>,
    #[serde(default)]
    pub owner: Option<String>,
    #[serde(default)]
    pub creator_id: Option<i64>,
    #[serde(default)]
    pub parent_id: Option<i64>,
    #[serde(default)]
    pub tags: Option<String>,
    #[serde(default)]
    pub file_url: Option<String>,
    #[serde(default)]
    pub sample_url: Option<String>,
    #[serde(default)]
    pub preview_url: Option<String>,
}

fn map_gelbooru_post(raw: GelbooruPostRaw) -> BooruPost {
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
        platform: BooruPlatform::Gelbooru,
        original_url: file_url,
        sample_url: raw.sample_url,
        preview_url: raw.preview_url,
        width: raw.width.unwrap_or(0),
        height: raw.height.unwrap_or(0),
        byte_size: 0,
        file_ext,
        created_at: raw.created_at.unwrap_or_default(),
        uploader_id: raw.creator_id.map(|id| id.to_string()).unwrap_or_default(),
        uploader_name: raw.owner.unwrap_or_else(|| "uploader".to_string()),
        source: raw.source,
        rating: normalize_rating(raw.rating.as_deref().unwrap_or("general")),
        tags,
        parent_id,
        has_children: false,
        score: raw.score.unwrap_or(0),
        is_banned: false,
        is_deleted: false,
        ugoira_frame_delays: None,
    }
}

pub async fn get_post(client: &MahoHttpClient, post_id: &str) -> Result<BooruPost, BooruError> {
    let url = format!("{BASE_URL}/index.php?page=dapi&s=post&q=index&json=1&limit=1&id={post_id}");
    let resp = client.get(&url).header("Accept", "application/json").send().await?;

    if !resp.status().is_success() {
        return Err(BooruError::ApiError {
            platform: "gelbooru".to_string(),
            code: resp.status().as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let raw_resp: GelbooruResponseRaw = resp.json().await?;
    let post = raw_resp
        .post
        .and_then(|list| list.into_iter().next())
        .ok_or_else(|| BooruError::PostNotFound {
            platform: "gelbooru".to_string(),
            id: post_id.to_string(),
        })?;

    Ok(map_gelbooru_post(post))
}

pub async fn get_post_by_md5(
    client: &MahoHttpClient,
    md5: &str,
) -> Result<Option<BooruPost>, BooruError> {
    let url =
        format!("{BASE_URL}/index.php?page=dapi&s=post&q=index&json=1&limit=1&tags=md5:{md5}");
    let resp = client.get(&url).header("Accept", "application/json").send().await?;

    if !resp.status().is_success() {
        return Ok(None);
    }

    let raw_resp: GelbooruResponseRaw = resp.json().await?;
    Ok(raw_resp.post.and_then(|list| list.into_iter().next()).map(map_gelbooru_post))
}

pub async fn search(
    client: &MahoHttpClient,
    tags: &str,
    page: u32,
) -> Result<BooruSearchResult, BooruError> {
    let pid = if page > 0 { page - 1 } else { 0 };
    let url = format!(
        "{BASE_URL}/index.php?page=dapi&s=post&q=index&json=1&limit=20&pid={pid}&tags={}",
        urlencoding(tags)
    );
    let resp = client.get(&url).header("Accept", "application/json").send().await?;

    if !resp.status().is_success() {
        return Err(BooruError::ApiError {
            platform: "gelbooru".to_string(),
            code: resp.status().as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let raw_resp: GelbooruResponseRaw = resp.json().await?;
    let results = raw_resp
        .post
        .unwrap_or_default()
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
