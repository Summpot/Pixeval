// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use pixeval_maho::MahoHttpClient;
use serde::Deserialize;

use crate::error::BooruError;
use crate::models::{
    BooruPlatform, BooruPost, BooruPostPreview, BooruSearchResult, BooruTag, normalize_rating,
};

const BASE_URL: &str = "https://sankakuapi.com";
const USER_AGENT: &str = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36";

#[derive(Deserialize, Debug)]
struct SankakuPostRaw {
    #[serde(default)]
    pub id: serde_json::Value,
    #[serde(default)]
    pub rating: Option<String>,
    #[serde(default)]
    pub author: Option<SankakuAuthorRaw>,
    #[serde(default)]
    pub preview_url: Option<String>,
    #[serde(default)]
    pub sample_url: Option<String>,
    #[serde(default)]
    pub file_url: Option<String>,
    #[serde(default)]
    pub width: Option<u32>,
    #[serde(default)]
    pub height: Option<u32>,
    #[serde(default)]
    pub file_size: Option<u64>,
    #[serde(default)]
    pub md5: Option<String>,
    #[serde(default)]
    pub parent_id: Option<serde_json::Value>,
    #[serde(default)]
    pub has_children: Option<bool>,
    #[serde(default)]
    pub total_score: Option<i32>,
    #[serde(default)]
    pub tags: Option<Vec<SankakuTagRaw>>,
}

#[derive(Deserialize, Debug)]
struct SankakuAuthorRaw {
    #[serde(default)]
    pub id: Option<serde_json::Value>,
    #[serde(default)]
    pub name: Option<String>,
}

#[derive(Deserialize, Debug)]
struct SankakuTagRaw {
    #[serde(default)]
    pub r#type: Option<i32>,
    #[serde(default)]
    pub tag_name: Option<String>,
    #[serde(default, rename = "tagName")]
    pub tag_name_camel: Option<String>,
    #[serde(default)]
    pub name: Option<String>,
}

fn id_to_string(val: &serde_json::Value) -> String {
    if let Some(i) = val.as_i64() {
        i.to_string()
    } else if let Some(s) = val.as_str() {
        s.to_string()
    } else {
        String::new()
    }
}

fn map_sankaku_post(raw: SankakuPostRaw) -> BooruPost {
    let mut tags = Vec::new();
    if let Some(raw_tags) = raw.tags {
        for t in raw_tags {
            let name = t
                .tag_name
                .or(t.tag_name_camel)
                .or(t.name)
                .unwrap_or_default()
                .replace('_', " ");
            let tag_type = match t.r#type.unwrap_or(0) {
                1 => "artist",
                2 => "copyright",
                3 => "character",
                4 => "meta",
                _ => "general",
            }
            .to_string();

            if !name.is_empty() {
                tags.push(BooruTag { tag_type, name });
            }
        }
    }

    let id = id_to_string(&raw.id);
    let original_url = raw
        .file_url
        .or_else(|| raw.sample_url.clone())
        .or_else(|| raw.preview_url.clone())
        .unwrap_or_default();

    let file_ext = original_url
        .rsplit('.')
        .next()
        .map(|s| s.to_string())
        .unwrap_or_default();

    let uploader_id = raw
        .author
        .as_ref()
        .and_then(|a| a.id.as_ref().map(id_to_string))
        .unwrap_or_default();
    let uploader_name = raw
        .author
        .and_then(|a| a.name)
        .unwrap_or_else(|| "uploader".to_string());

    let parent_id = raw.parent_id.map(|v| id_to_string(&v)).filter(|s| !s.is_empty());

    BooruPost {
        id,
        md5: raw.md5.unwrap_or_default(),
        platform: BooruPlatform::Sankaku,
        original_url,
        sample_url: raw.sample_url,
        preview_url: raw.preview_url,
        width: raw.width.unwrap_or(0),
        height: raw.height.unwrap_or(0),
        byte_size: raw.file_size.unwrap_or(0),
        file_ext,
        created_at: String::new(),
        uploader_id,
        uploader_name,
        source: None,
        rating: normalize_rating(raw.rating.as_deref().unwrap_or("general")),
        tags,
        parent_id,
        has_children: raw.has_children.unwrap_or(false),
        score: raw.total_score.unwrap_or(0),
        is_banned: false,
        is_deleted: false,
        ugoira_frame_delays: None,
    }
}

pub async fn get_post(client: &MahoHttpClient, post_id: &str) -> Result<BooruPost, BooruError> {
    let url = format!("{BASE_URL}/posts/{post_id}");
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    if !resp.status().is_success() {
        return Err(BooruError::ApiError {
            platform: "sankaku".to_string(),
            code: resp.status().as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let raw: SankakuPostRaw = resp.json().await?;
    Ok(map_sankaku_post(raw))
}

pub async fn search(
    client: &MahoHttpClient,
    tags: &str,
    page: u32,
) -> Result<BooruSearchResult, BooruError> {
    let url = format!("{BASE_URL}/posts?tags={}&page={page}", urlencoding(tags));
    let resp = client
        .get(&url)
        .header("User-Agent", USER_AGENT)
        .header("Accept", "application/json")
        .send()
        .await?;

    if !resp.status().is_success() {
        return Err(BooruError::ApiError {
            platform: "sankaku".to_string(),
            code: resp.status().as_u16(),
            message: resp.text().await.unwrap_or_default(),
        });
    }

    let posts: Vec<SankakuPostRaw> = resp.json().await?;
    let results = posts
        .into_iter()
        .map(|p| {
            let id = id_to_string(&p.id);
            let md5 = p.md5;
            let tag_string = p
                .tags
                .unwrap_or_default()
                .into_iter()
                .filter_map(|t| t.tag_name.or(t.tag_name_camel).or(t.name))
                .collect::<Vec<_>>()
                .join(" ");

            BooruPostPreview {
                id,
                md5,
                tag_string,
                is_banned: false,
                is_deleted: false,
            }
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
