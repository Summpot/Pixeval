use crate::state::APP_STATE;
use pixeval_mako::catalog::{IllustContentType, XRestrict};
use pixeval_mako::models::Illustration;

#[derive(Clone, Debug)]
pub struct FilterMetadata {
    pub create_date_timestamp: i64,
    pub ai_type: i32,
    pub x_restrict: i32,
    pub illustration_type: i32,
    pub translated_tags: Vec<Option<String>>,
}

#[derive(Clone, Debug)]
pub struct IllustSummary {
    pub id: i64,
    pub title: String,
    pub caption: String,
    pub user_id: i64,
    pub user_name: String,
    pub user_account: String,
    pub user_avatar_url: String,
    pub preview_url: String,
    pub original_url: String,
    pub width: i32,
    pub height: i32,
    pub page_count: i32,
    pub is_bookmarked: bool,
    pub total_bookmarks: i64,
    pub total_views: i64,
    pub tags: Vec<String>,
    pub is_r18: bool,
    pub is_ugoira: bool,
    pub page_urls: Vec<String>,
    pub original_page_urls: Vec<String>,
    pub thumbnail_url: Option<String>,
    pub filter_metadata: Option<FilterMetadata>,
}

impl From<Illustration> for IllustSummary {
    fn from(i: Illustration) -> Self {
        let filter_metadata = Some(FilterMetadata {
            create_date_timestamp: chrono::DateTime::parse_from_rfc3339(&i.create_date)
                .map(|d| d.timestamp())
                .unwrap_or_default(),
            ai_type: if i.illust_ai_type == 2 { 1 } else { 0 },
            x_restrict: i.x_restrict.as_i32(),
            illustration_type: match i.illust_type {
                IllustContentType::Illustration => 0,
                IllustContentType::Manga => 1,
                IllustContentType::Ugoira => 2,
            },
            translated_tags: i.tags.iter().map(|t| t.translated_name.clone()).collect(),
        });
        let thumbnail_url = i.image_urls.square_medium.clone();
        let user_avatar_url = i
            .user
            .profile_image_urls
            .medium
            .or(i.user.profile_image_urls.px_50x50)
            .or(i.user.profile_image_urls.px_16x16)
            .unwrap_or_default();

        let preview_url = i
            .image_urls
            .large
            .clone()
            .or_else(|| i.image_urls.medium.clone())
            .unwrap_or_default();

        let original_url = i
            .meta_single_page
            .original_image_url
            .clone()
            .unwrap_or_else(|| preview_url.clone());

        let mut page_urls = Vec::new();
        if i.page_count > 1 && !i.meta_pages.is_empty() {
            for p in &i.meta_pages {
                let url = p
                    .image_urls
                    .large
                    .clone()
                    .or_else(|| p.image_urls.medium.clone())
                    .unwrap_or_default();
                if !url.is_empty() {
                    page_urls.push(url);
                }
            }
        } else {
            page_urls.push(preview_url.clone());
        }

        let mut original_page_urls = Vec::new();
        if i.page_count > 1 && !i.meta_pages.is_empty() {
            for p in &i.meta_pages {
                let orig = p
                    .image_urls
                    .original
                    .clone()
                    .or_else(|| p.image_urls.large.clone())
                    .unwrap_or_default();
                if !orig.is_empty() {
                    original_page_urls.push(orig);
                }
            }
        } else {
            original_page_urls.push(original_url.clone());
        }

        let is_r18 = i.x_restrict != XRestrict::Ordinary
            || i.tags
                .iter()
                .any(|t| t.name.to_lowercase().contains("r-18") || t.name.contains("R-18G"));

        let is_ugoira = i.illust_type == IllustContentType::Ugoira;
        let tags = i.tags.into_iter().map(|t| t.name).collect();

        Self {
            id: i.id,
            title: i.title,
            caption: i.caption,
            user_id: i.user.id,
            user_name: i.user.name,
            user_account: i.user.account,
            user_avatar_url,
            preview_url,
            original_url,
            width: i.width,
            height: i.height,
            page_count: i.page_count,
            is_bookmarked: i.is_bookmarked,
            total_bookmarks: i.total_bookmarks,
            total_views: i.total_view,
            tags,
            is_r18,
            is_ugoira,
            page_urls,
            original_page_urls,
            thumbnail_url,
            filter_metadata,
        }
    }
}

#[derive(Clone, Debug)]
pub struct FeedPageResult {
    pub items: Vec<IllustSummary>,
    pub exhausted: bool,
}

pub async fn get_recommended_feed(refresh: bool) -> Result<FeedPageResult, String> {
    let client = &APP_STATE.client;
    let feed = if refresh || APP_STATE.active_recommended_feed.read().is_none() {
        let f = client.work_recommended(false, false);
        *APP_STATE.active_recommended_feed.write() = Some(f.clone());
        f
    } else {
        APP_STATE
            .active_recommended_feed
            .read()
            .as_ref()
            .unwrap()
            .clone()
    };

    let page = feed
        .next_page()
        .await
        .map_err(|e| format!("Fetch recommended feed failed: {e}"))?;

    let items = page.items.into_iter().map(Into::into).collect();
    Ok(FeedPageResult {
        items,
        exhausted: page.exhausted,
    })
}

pub async fn get_ranking_feed(
    mode: String,
    date: Option<String>,
    refresh: bool,
) -> Result<FeedPageResult, String> {
    let client = &APP_STATE.client;
    let feed = if refresh || APP_STATE.active_ranking_feed.read().is_none() {
        let f = client.work_ranking(mode, date);
        *APP_STATE.active_ranking_feed.write() = Some(f.clone());
        f
    } else {
        APP_STATE
            .active_ranking_feed
            .read()
            .as_ref()
            .unwrap()
            .clone()
    };

    let page = feed
        .next_page()
        .await
        .map_err(|e| format!("Fetch ranking feed failed: {e}"))?;

    let items = page.items.into_iter().map(Into::into).collect();
    Ok(FeedPageResult {
        items,
        exhausted: page.exhausted,
    })
}

pub async fn get_follow_feed(restrict: String, refresh: bool) -> Result<FeedPageResult, String> {
    let client = &APP_STATE.client;
    let feed = if refresh || APP_STATE.active_follow_feed.read().is_none() {
        let f = client.work_following(restrict);
        *APP_STATE.active_follow_feed.write() = Some(f.clone());
        f
    } else {
        APP_STATE
            .active_follow_feed
            .read()
            .as_ref()
            .unwrap()
            .clone()
    };

    let page = feed
        .next_page()
        .await
        .map_err(|e| format!("Fetch follow feed failed: {e}"))?;

    let items = page.items.into_iter().map(Into::into).collect();
    Ok(FeedPageResult {
        items,
        exhausted: page.exhausted,
    })
}

pub async fn toggle_bookmark(illust_id: i64, is_bookmarked: bool) -> Result<bool, String> {
    let client = &APP_STATE.client;
    if is_bookmarked {
        client
            .remove_bookmark(false, illust_id)
            .await
            .map_err(|e| format!("Remove bookmark failed: {e}"))?;
        Ok(false)
    } else {
        client
            .post_bookmark(false, illust_id, "public".to_string(), None)
            .await
            .map_err(|e| format!("Add bookmark failed: {e}"))?;
        Ok(true)
    }
}

pub async fn get_illust_detail(illust_id: i64) -> Result<IllustSummary, String> {
    let client = &APP_STATE.client;
    let resp = client
        .get_illustration(illust_id)
        .await
        .map_err(|e| format!("Get illustration detail failed: {e}"))?;
    Ok(resp.into())
}
