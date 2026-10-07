// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use pixeval_maho::MahoHttpClient;
use serde::Deserialize;

use crate::error::BooruError;
use crate::models::{
    BooruPlatform, BooruPost, BooruPostPreview, BooruSearchResult, BooruTag, normalize_rating,
};

const BASE_URL: &str = "https://api.rule34.xxx";
const USER_AGENT: &str = "Pixeval/1.0 (Windows NT 10.0; Win64; x64)";

#[derive(Deserialize, Debug)]
struct Rule34PostRaw {
    pub id: i64,
    #[serde(default)]
    pub hash: Option<String>,
    #[serde(default)]
    pub file_url: Option<String>,
    #[serde(default)]
    pub sample_url: Option<String>,
    #[serde(default)]
    pub preview_url: Option<String>,
    #[serde(default)]
    pub width: Option<u32>,
    #[serde(default)]
    pub height: Option<u32>,
    #[serde(default)]
    pub owner: Option<String>,
    #[serde(default)]
    pub source: Option<String>,
    #[serde(default)]
    pub rating: Option<String>,
    #[serde(default)]
    pub tags: Option<String>,
    #[serde(default)]
    pub tag_info: Option<Vec<Rule34TagInfoRaw>>,
    #[serde(default)]
    pub parent_id: Option<i64>,
    #[serde(default)]
    pub score: Option<i32>,
}

#[derive(Deserialize, Debug)]
struct Rule34TagInfoRaw {
    #[serde(default)]
    pub r#type: String,
    #[serde(default)]
    pub tag: String,
}

fn map_rule34_post(raw: Rule34PostRaw) -> BooruPost {
    let mut tags = Vec::new();
    if let Some(infos) = raw.tag_info {
        for info in infos {
            tags.push(BooruTag {
                tag_type: info.r#type,
                name: info.tag.replace('_', " "),
            });
        }
    } else if let Some(t_str) = raw.tags.as_deref() {
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
        md5: raw.hash.unwrap_or_default(),
        platform: BooruPlatform::Rule34,
        original_url: file_url,
        sample_url: raw.sample_url,
        preview_url: raw.preview_url,
        width: raw.width.unwrap_or(0),
        height: raw.height.unwrap_or(0),
        byte_size: 0,
        file_ext,
        created_at: String::new(),
        uploader_id: String::new(),
        uploader_name: raw.owner.unwrap_or_else(|| "uploader".to_string()),
        source: raw.source,
        rating: normalize_rating(raw.rating.as_deref().unwrap_or("explicit")),
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
    let url = format!(
        "{BASE_URL}/index.php?page=dapi&s=post&q=index&json=1&limit=1&id={post_id}&fields=tag_info"
    );
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    if !resp.status().is_success() {
        return Err(BooruError::ApiError {
            platform: "rule34".to_string(),
            code: resp.status().as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let posts: Vec<Rule34PostRaw> = resp.json().await?;
    let post = posts.into_iter().next().ok_or_else(|| BooruError::PostNotFound {
        platform: "rule34".to_string(),
        id: post_id.to_string(),
    })?;

    Ok(map_rule34_post(post))
}

pub async fn get_post_by_md5(
    client: &MahoHttpClient,
    md5: &str,
) -> Result<Option<BooruPost>, BooruError> {
    let url = format!(
        "{BASE_URL}/index.php?page=dapi&s=post&q=index&json=1&limit=1&tags=md5:{md5}&fields=tag_info"
    );
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    if !resp.status().is_success() {
        return Ok(None);
    }

    let posts: Vec<Rule34PostRaw> = resp.json().await?;
    Ok(posts.into_iter().next().map(map_rule34_post))
}

pub async fn search(
    client: &MahoHttpClient,
    tags: &str,
    page: u32,
) -> Result<BooruSearchResult, BooruError> {
    let pid = if page > 0 { page - 1 } else { 0 };
    let url = format!(
        "{BASE_URL}/index.php?page=dapi&s=post&q=index&json=1&limit=20&pid={pid}&tags={}&fields=tag_info",
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
            platform: "rule34".to_string(),
            code: resp.status().as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let posts: Vec<Rule34PostRaw> = resp.json().await?;
    let results = posts
        .into_iter()
        .map(|p| BooruPostPreview {
            id: p.id.to_string(),
            md5: p.hash,
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
