use super::state::APP_STATE;
use pixeval_mako::catalog::{IllustSearchTarget, IllustrationSearchParams, SortOrder};
use pixeval_mako::stream::IllustrationFeed;
use std::sync::Arc;

/// A frontend owns each feed independently. Cancelling one search never
/// removes or replaces another frontend's pagination state.
#[derive(uniffi::Object)]
pub struct AppIllustrationFeed {
    inner: Arc<IllustrationFeed>,
    filter_query: Option<String>,
}

#[uniffi::export(async_runtime = "tokio")]
#[boltffi::export]
impl AppIllustrationFeed {
    #[uniffi::constructor]
    pub fn recommended() -> Self {
        Self {
            inner: APP_STATE.client.work_recommended(false, false),
            filter_query: None,
        }
    }
    #[uniffi::constructor]
    pub fn ranking(mode: String, date: Option<String>) -> Self {
        Self {
            inner: APP_STATE.client.work_ranking(mode, date),
            filter_query: None,
        }
    }
    #[uniffi::constructor]
    pub fn following(restrict: String) -> Self {
        Self {
            inner: APP_STATE.client.work_following(restrict),
            filter_query: None,
        }
    }
    #[uniffi::constructor]
    pub fn search(
        word: String,
        sort: Option<String>,
        search_target: Option<String>,
    ) -> Result<Self, String> {
        let plan = super::api::filters::plan_search_query(&word)?;
        let sort = match sort.as_deref() {
            Some("popular_desc") => SortOrder::PopularDesc,
            Some("date_asc") => SortOrder::DateAsc,
            _ => SortOrder::DateDesc,
        };
        let target_name = plan.remote_target.as_deref().or(search_target.as_deref());
        let search_target = match target_name {
            Some("exact_match_for_tags") => IllustSearchTarget::ExactMatchForTags,
            Some("title_and_caption") => IllustSearchTarget::TitleAndCaption,
            Some("keyword") => IllustSearchTarget::Keyword,
            _ => IllustSearchTarget::PartialMatchForTags,
        };
        let inner = if plan.remote_word.is_empty() {
            APP_STATE.client.work_recommended(false, false)
        } else {
            APP_STATE
                .client
                .illustration_search_advanced(IllustrationSearchParams {
                    word: plan.remote_word,
                    sort,
                    search_target,
                    ..Default::default()
                })
        };
        Ok(Self {
            inner,
            filter_query: plan.filter_query,
        })
    }
    pub async fn next_page(&self) -> Result<crate::IllustrationFeedPage, String> {
        async_compat::Compat::new(async { self.inner.next_page().await.map_err(|e| e.to_string()) })
            .await
    }
    pub fn cancel(&self) {
        self.inner.cancel();
    }
    pub fn filter_query(&self) -> Option<String> {
        self.filter_query.clone()
    }
}

impl Drop for AppIllustrationFeed {
    fn drop(&mut self) {
        self.inner.cancel();
    }
}
