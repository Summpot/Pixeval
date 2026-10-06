// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::sync::Arc;

use pixeval_maho::{DnsResolver, MahoConfig, MahoHttpClient};

use crate::error::BooruError;
use crate::models::{BooruFavoriteResult, BooruPlatform, BooruPost, BooruSearchResult};
use crate::platforms::{danbooru, gelbooru, rule34, sankaku, yandere};

#[derive(uniffi::Object)]
pub struct BooruClient {
    http_client: MahoHttpClient,
}

#[uniffi::export(async_runtime = "tokio")]
impl BooruClient {
    #[uniffi::constructor]
    pub fn new(proxy_url: Option<String>) -> Arc<Self> {
        let maho_config = Arc::new(MahoConfig {
            enabled: false,
            split_delay_ms: 0,
            dns_resolver: DnsResolver::new(),
        });
        let clean_proxy = proxy_url
            .as_deref()
            .map(|s| s.trim())
            .filter(|s| !s.is_empty())
            .map(|s| s.to_string());
        let http_client = MahoHttpClient::new(maho_config, clean_proxy);

        Arc::new(Self { http_client })
    }

    pub async fn get_post(
        &self,
        platform: BooruPlatform,
        post_id: String,
    ) -> Result<BooruPost, BooruError> {
        let trimmed_id = post_id.trim();
        match platform {
            BooruPlatform::Danbooru => danbooru::get_post(&self.http_client, trimmed_id).await,
            BooruPlatform::Gelbooru => gelbooru::get_post(&self.http_client, trimmed_id).await,
            BooruPlatform::Yandere => yandere::get_post(&self.http_client, trimmed_id).await,
            BooruPlatform::Rule34 => rule34::get_post(&self.http_client, trimmed_id).await,
            BooruPlatform::Sankaku => sankaku::get_post(&self.http_client, trimmed_id).await,
        }
    }

    pub async fn get_post_by_md5(
        &self,
        platform: BooruPlatform,
        md5: String,
    ) -> Result<Option<BooruPost>, BooruError> {
        let trimmed_md5 = md5.trim();
        match platform {
            BooruPlatform::Danbooru => danbooru::get_post_by_md5(&self.http_client, trimmed_md5).await,
            BooruPlatform::Gelbooru => gelbooru::get_post_by_md5(&self.http_client, trimmed_md5).await,
            BooruPlatform::Yandere => yandere::get_post_by_md5(&self.http_client, trimmed_md5).await,
            BooruPlatform::Rule34 => rule34::get_post_by_md5(&self.http_client, trimmed_md5).await,
            BooruPlatform::Sankaku => {
                // Sankaku search with md5 tag
                let search_res = sankaku::search(&self.http_client, &format!("md5:{trimmed_md5}"), 1).await?;
                if let Some(first) = search_res.results.into_iter().next() {
                    let post = sankaku::get_post(&self.http_client, &first.id).await?;
                    Ok(Some(post))
                } else {
                    Ok(None)
                }
            }
        }
    }

    pub async fn search(
        &self,
        platform: BooruPlatform,
        tags: String,
        page: u32,
    ) -> Result<BooruSearchResult, BooruError> {
        let trimmed_tags = tags.trim();
        let target_page = if page == 0 { 1 } else { page };
        match platform {
            BooruPlatform::Danbooru => danbooru::search(&self.http_client, trimmed_tags, target_page).await,
            BooruPlatform::Gelbooru => gelbooru::search(&self.http_client, trimmed_tags, target_page).await,
            BooruPlatform::Yandere => yandere::search(&self.http_client, trimmed_tags, target_page).await,
            BooruPlatform::Rule34 => rule34::search(&self.http_client, trimmed_tags, target_page).await,
            BooruPlatform::Sankaku => sankaku::search(&self.http_client, trimmed_tags, target_page).await,
        }
    }

    pub async fn post_favorite(
        &self,
        platform: BooruPlatform,
        post_id: String,
        favorite: bool,
    ) -> Result<BooruFavoriteResult, BooruError> {
        let trimmed_id = post_id.trim();
        match platform {
            BooruPlatform::Danbooru => {
                let success = danbooru::post_favorite(&self.http_client, trimmed_id, favorite).await?;
                Ok(BooruFavoriteResult { success })
            }
            _ => Err(BooruError::NotSupported {
                message: format!(
                    "Posting favorites is not supported for platform: {}",
                    platform.as_str()
                ),
            }),
        }
    }
}
