// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use serde::{Deserialize, Serialize};

#[derive(uniffi::Record, Clone, Debug, Default, Serialize, Deserialize)]
pub struct SauceNaoItem {
    pub similarity: f64,
    pub thumbnail_url: String,
    pub index_id: i32,
    pub index_name: String,
    pub title: String,
    pub author_name: String,
    pub author_url: Option<String>,
    pub source_url: Option<String>,
    pub ext_urls: Vec<String>,
    pub platform: String,
    pub artwork_id: Option<String>,
    pub is_nsfw: bool,
}

#[derive(uniffi::Record, Clone, Debug, Default, Serialize, Deserialize)]
pub struct SauceNaoSearchResult {
    pub status: i32,
    pub message: String,
    pub remaining_short: i32,
    pub remaining_long: i32,
    pub results: Vec<SauceNaoItem>,
}

#[derive(Deserialize, Debug)]
pub(crate) struct RawSauceNaoResponse {
    pub header: RawSauceNaoResponseHeader,
    #[serde(default)]
    pub results: Vec<RawSauceNaoResult>,
}

#[derive(Deserialize, Debug)]
pub(crate) struct RawSauceNaoResponseHeader {
    #[serde(default)]
    pub status: i32,
    #[serde(default)]
    pub message: Option<String>,
    #[serde(default)]
    pub short_remaining: Option<i32>,
    #[serde(default)]
    pub long_remaining: Option<i32>,
}

#[derive(Deserialize, Debug)]
pub(crate) struct RawSauceNaoResult {
    pub header: RawSauceNaoResultHeader,
    #[serde(default)]
    pub data: serde_json::Value,
}

#[derive(Deserialize, Debug)]
pub(crate) struct RawSauceNaoResultHeader {
    pub similarity: String,
    #[serde(default)]
    pub thumbnail: Option<String>,
    #[serde(default)]
    pub index_id: i32,
    #[serde(default)]
    pub index_name: Option<String>,
}

pub(crate) fn is_index_nsfw(index_id: i32) -> bool {
    // 0: h-mags, 1: h-anime, 2: hcg, 16: fakku, 18: h-misc (nhentai), 22: h-anime, 38: h-misc (e-hentai)
    matches!(index_id, 0 | 1 | 2 | 16 | 18 | 22 | 38)
}

fn extract_id_str(val: &serde_json::Value) -> Option<String> {
    if let Some(n) = val.as_i64() {
        return Some(n.to_string());
    }
    if let Some(n) = val.as_u64() {
        return Some(n.to_string());
    }
    if let Some(s) = val.as_str() {
        let trimmed = s.trim();
        if !trimmed.is_empty() && trimmed != "0" {
            return Some(trimmed.to_string());
        }
    }
    None
}

fn extract_string(val: &serde_json::Value) -> Option<String> {
    if let Some(s) = val.as_str() {
        let trimmed = s.trim();
        if !trimmed.is_empty() {
            return Some(trimmed.to_string());
        }
    }
    None
}

pub(crate) fn map_raw_result(raw: RawSauceNaoResult) -> SauceNaoItem {
    let similarity = raw
        .header
        .similarity
        .trim()
        .parse::<f64>()
        .unwrap_or(0.0);
    let thumbnail_url = raw.header.thumbnail.unwrap_or_default();
    let index_id = raw.header.index_id;
    let index_name = raw.header.index_name.unwrap_or_default();

    let mut ext_urls = Vec::new();
    if let Some(urls) = raw.data.get("ext_urls").and_then(|v| v.as_array()) {
        for u in urls {
            if let Some(s) = u.as_str() {
                ext_urls.push(s.to_string());
            }
        }
    }

    let mut platform = "saucenao".to_string();
    let mut artwork_id = None;

    if let Some(id) = raw.data.get("pixiv_id").and_then(extract_id_str) {
        platform = "pixiv".to_string();
        artwork_id = Some(id);
    } else if let Some(id) = raw.data.get("danbooru_id").and_then(extract_id_str) {
        platform = "danbooru".to_string();
        artwork_id = Some(id);
    } else if let Some(id) = raw.data.get("gelbooru_id").and_then(extract_id_str) {
        platform = "gelbooru".to_string();
        artwork_id = Some(id);
    } else if let Some(id) = raw.data.get("yandere_id").and_then(extract_id_str) {
        platform = "yandere".to_string();
        artwork_id = Some(id);
    } else if let Some(id) = raw.data.get("sankaku_id").and_then(extract_id_str) {
        platform = "sankaku".to_string();
        artwork_id = Some(id);
    } else {
        // Inspect ext_urls for recognizable domains
        for url in &ext_urls {
            if url.contains("pixiv.net") {
                platform = "pixiv".to_string();
                if let Some(pos) = url.find("artworks/") {
                    artwork_id = url[pos + 9..].split(['/', '?', '#']).next().map(|s| s.to_string());
                } else if let Some(pos) = url.find("illust_id=") {
                    artwork_id = url[pos + 10..].split(['&', '#']).next().map(|s| s.to_string());
                }
                break;
            } else if url.contains("danbooru.donmai.us") {
                platform = "danbooru".to_string();
                if let Some(pos) = url.find("posts/") {
                    artwork_id = url[pos + 6..].split(['/', '?', '#']).next().map(|s| s.to_string());
                }
                break;
            } else if url.contains("gelbooru.com") {
                platform = "gelbooru".to_string();
                if let Some(pos) = url.find("id=") {
                    artwork_id = url[pos + 3..].split(['&', '#']).next().map(|s| s.to_string());
                }
                break;
            } else if url.contains("yande.re") {
                platform = "yandere".to_string();
                if let Some(pos) = url.find("post/show/") {
                    artwork_id = url[pos + 10..].split(['/', '?', '#']).next().map(|s| s.to_string());
                }
                break;
            } else if url.contains("sankakucomplex.com") {
                platform = "sankaku".to_string();
                if let Some(pos) = url.find("posts/show/") {
                    artwork_id = url[pos + 11..].split(['/', '?', '#']).next().map(|s| s.to_string());
                } else if let Some(pos) = url.find("post/show/") {
                    artwork_id = url[pos + 10..].split(['/', '?', '#']).next().map(|s| s.to_string());
                }
                break;
            } else if url.contains("rule34.xxx") {
                platform = "rule34".to_string();
                if let Some(pos) = url.find("id=") {
                    artwork_id = url[pos + 3..].split(['&', '#']).next().map(|s| s.to_string());
                }
                break;
            }
        }
    }

    let title = raw
        .data
        .get("title")
        .and_then(extract_string)
        .or_else(|| raw.data.get("jp_name").and_then(extract_string))
        .or_else(|| raw.data.get("eng_name").and_then(extract_string))
        .or_else(|| raw.data.get("source").and_then(extract_string))
        .unwrap_or_else(|| index_name.clone());

    let author_name = raw
        .data
        .get("member_name")
        .and_then(extract_string)
        .or_else(|| raw.data.get("author_name").and_then(extract_string))
        .or_else(|| raw.data.get("artist").and_then(extract_string))
        .or_else(|| {
            raw.data.get("creator").and_then(|v| {
                if let Some(s) = v.as_str() {
                    Some(s.to_string())
                } else if let Some(arr) = v.as_array() {
                    let names: Vec<_> = arr.iter().filter_map(|x| x.as_str()).collect();
                    if names.is_empty() {
                        None
                    } else {
                        Some(names.join(", "))
                    }
                } else {
                    None
                }
            })
        })
        .unwrap_or_default();

    let author_url = raw
        .data
        .get("author_url")
        .and_then(extract_string)
        .or_else(|| {
            raw.data.get("member_id").and_then(extract_id_str).map(|id| {
                format!("https://www.pixiv.net/users/{id}")
            })
        });

    let source_url = raw.data.get("source").and_then(extract_string);
    let is_nsfw = is_index_nsfw(index_id);

    SauceNaoItem {
        similarity,
        thumbnail_url,
        index_id,
        index_name,
        title,
        author_name,
        author_url,
        source_url,
        ext_urls,
        platform,
        artwork_id,
        is_nsfw,
    }
}
