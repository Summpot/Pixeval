use crate::app::state::APP_STATE;
use futures_util::{StreamExt, TryStreamExt, stream};
use pixeval_booru::{BooruClient, BooruPlatform};

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
#[boltffi::data]
pub struct BooruPostSummaryDto {
    pub id: String,
    pub md5: Option<String>,
    pub tag_string: String,
    pub preview_url: Option<String>,
    pub sample_url: Option<String>,
    pub original_url: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
#[boltffi::data]
pub struct BooruSearchResultDto {
    pub platform: String,
    pub results: Vec<BooruPostSummaryDto>,
    pub search_tags: String,
    pub page_number: u32,
}

pub async fn search_booru(
    platform_name: String,
    tags: String,
    page: u32,
) -> Result<BooruSearchResultDto, String> {
    let proxy = APP_STATE.current_config.read().proxy_url.clone();
    let client = BooruClient::new(proxy);

    let platform = match platform_name.to_lowercase().as_str() {
        "danbooru" => BooruPlatform::Danbooru,
        "gelbooru" => BooruPlatform::Gelbooru,
        "yandere" => BooruPlatform::Yandere,
        "rule34" => BooruPlatform::Rule34,
        "sankaku" => BooruPlatform::Sankaku,
        _ => BooruPlatform::Yandere,
    };

    let res = client
        .search(platform, tags.clone(), page)
        .await
        .map_err(|e| format!("Booru search failed: {e}"))?;

    // Search previews do not contain media URLs. Resolve actual detail data
    // rather than guessing a CDN layout or forcing every original to JPEG.
    let summaries = stream::iter(res.results.into_iter().map(|preview| {
        let client = client.clone();
        async move {
            let post = client
                .get_post(platform, preview.id)
                .await
                .map_err(|e| format!("Booru media lookup failed: {e}"))?;
            Ok::<_, String>(summary_from_post(post, preview.tag_string))
        }
    }))
    .buffered(4)
    .try_collect()
    .await?;

    Ok(BooruSearchResultDto {
        platform: platform.as_str().to_string(),
        results: summaries,
        search_tags: tags,
        page_number: res.page_number,
    })
}

fn summary_from_post(post: pixeval_booru::BooruPost, tag_string: String) -> BooruPostSummaryDto {
    BooruPostSummaryDto {
        id: post.id,
        md5: Some(post.md5),
        tag_string,
        preview_url: post.preview_url.filter(|s| !s.is_empty()),
        sample_url: post.sample_url.filter(|s| !s.is_empty()),
        original_url: Some(post.original_url).filter(|s| !s.is_empty()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn media_urls_are_taken_from_the_server_including_non_jpeg_originals() {
        let post = pixeval_booru::BooruPost {
            original_url: "https://cdn.example/custom/path/image.png?key=signed".into(),
            preview_url: Some("https://cdn.example/thumb.webp".into()),
            sample_url: Some("https://cdn.example/sample.png".into()),
            file_ext: "png".into(),
            ..Default::default()
        };
        let summary = summary_from_post(post.clone(), "cat".into());
        assert_eq!(summary.original_url, Some(post.original_url));
        assert_eq!(summary.preview_url, post.preview_url);
        assert_eq!(summary.sample_url, post.sample_url);
    }
}

pub mod ffi {

    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn search_booru(
        platform_name: String,
        tags: String,
        page: u32,
    ) -> Result<crate::app::api::booru::BooruSearchResultDto, String> {
        async_compat::Compat::new(
            async move { super::search_booru(platform_name, tags, page).await },
        )
        .await
    }
}
