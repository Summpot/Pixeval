use crate::app::state::APP_STATE;
use pixeval_saucenao::SauceNaoClient;

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
#[boltffi::data]
pub struct SauceNaoItemDto {
    pub similarity: f64,
    pub thumbnail_url: String,
    pub title: String,
    pub author_name: String,
    pub author_url: Option<String>,
    pub source_url: Option<String>,
    pub ext_urls: Vec<String>,
    pub platform: String,
    pub artwork_id: Option<String>,
    pub is_nsfw: bool,
}

pub async fn search_saucenao(
    image_bytes: Vec<u8>,
    api_key: Option<String>,
) -> Result<Vec<SauceNaoItemDto>, String> {
    let proxy = APP_STATE.current_config.read().proxy_url.clone();
    let key = api_key.unwrap_or_default();
    let client = SauceNaoClient::new(key, proxy);

    let items = client
        .search(image_bytes)
        .await
        .map_err(|e| format!("SauceNAO search failed: {e}"))?;

    Ok(items
        .into_iter()
        .map(|item| SauceNaoItemDto {
            similarity: item.similarity,
            thumbnail_url: item.thumbnail_url,
            title: item.title,
            author_name: item.author_name,
            author_url: item.author_url,
            source_url: item.source_url,
            ext_urls: item.ext_urls,
            platform: item.platform,
            artwork_id: item.artwork_id,
            is_nsfw: item.is_nsfw,
        })
        .collect())
}

pub mod ffi {

    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn search_saucenao(
        image_bytes: Vec<u8>,
        api_key: Option<String>,
    ) -> Result<Vec<crate::app::api::saucenao::SauceNaoItemDto>, String> {
        async_compat::Compat::new(async move { super::search_saucenao(image_bytes, api_key).await })
            .await
    }
}
