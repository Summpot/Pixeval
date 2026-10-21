use crate::api::feed::{FeedPageResult, IllustSummary};
use crate::state::APP_STATE;
use pixeval_mako::catalog::{IllustSearchTarget, IllustrationSearchParams, SortOrder};
use pixeval_mako::stream::IllustrationFeed;
use std::sync::Arc;

pub(crate) struct SearchFeedState {
    signature: (String, Option<String>, Option<String>),
    feed: Arc<IllustrationFeed>,
}

pub fn release_search_session(session_id: String) {
    APP_STATE.search_feeds.write().remove(&session_id);
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AutocompleteTagDto {
    pub name: String,
    pub translated_name: Option<String>,
}

pub async fn get_search_autocomplete(word: String) -> Result<Vec<AutocompleteTagDto>, String> {
    let client = &APP_STATE.client;
    let tags = client
        .search_autocomplete(word)
        .await
        .map_err(|e| format!("Autocomplete failed: {e}"))?;

    Ok(tags
        .into_iter()
        .map(|t| AutocompleteTagDto {
            name: t.name,
            translated_name: t.translated_name,
        })
        .collect())
}

pub async fn search_illustrations(
    word: String,
    sort: Option<String>,
    search_target: Option<String>,
    refresh: bool,
    session_id: Option<String>,
) -> Result<FeedPageResult, String> {
    let client = &APP_STATE.client;

    let sort_order = match sort.as_deref() {
        Some("popular_desc") => SortOrder::PopularDesc,
        Some("date_asc") => SortOrder::DateAsc,
        _ => SortOrder::DateDesc,
    };

    let plan = crate::api::filters::plan_search_query(&word)?;
    let target_name = plan.remote_target.as_deref().or(search_target.as_deref());
    let target = match target_name {
        Some("exact_match_for_tags") => IllustSearchTarget::ExactMatchForTags,
        Some("title_and_caption") => IllustSearchTarget::TitleAndCaption,
        Some("keyword") => IllustSearchTarget::Keyword,
        _ => IllustSearchTarget::PartialMatchForTags,
    };

    let params = IllustrationSearchParams {
        word: plan.remote_word.clone(),
        sort: sort_order,
        search_target: target,
        ..Default::default()
    };

    let key = session_id.unwrap_or_else(|| "legacy".to_string());
    let signature = (word.clone(), sort, search_target);
    let feed = {
        let mut sessions = APP_STATE.search_feeds.write();
        if let Some(existing) = sessions
            .get(&key)
            .filter(|s| !refresh && s.signature == signature)
        {
            existing.feed.clone()
        } else {
            let feed = if plan.remote_word.is_empty() {
                client.work_recommended(false, false)
            } else {
                client.illustration_search_advanced(params)
            };
            sessions.insert(
                key,
                SearchFeedState {
                    signature,
                    feed: feed.clone(),
                },
            );
            feed
        }
    };

    let page = feed
        .next_page()
        .await
        .map_err(|e| format!("Search error: {e}"))?;

    let mut items: Vec<IllustSummary> = page.items.into_iter().map(Into::into).collect();
    if let Some(query) = plan.filter_query {
        items = crate::api::filters::filter_artworks_by_query(query, items)?;
    }

    Ok(FeedPageResult {
        items,
        exhausted: page.exhausted,
    })
}
