// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::{HashMap, VecDeque};
use std::time::{Duration, Instant};

use parking_lot::RwLock;
use pixeval_mako::models::{BookmarkTag, CommentRecord, Illustration, Novel, Series, SpotlightArticle, User};
use pixeval_mako::stream::{
    IllustrationFetchEngine, NovelFetchEngine, SeriesFetchEngine, SpotlightFetchEngine,
    UserFetchEngine,
};
use serde_json::json;
use uuid::Uuid;

const CURSOR_PREFIX: &str = "pixeval";
const MAX_ACTIVE_CURSORS: usize = 64;
const CURSOR_TTL: Duration = Duration::from_secs(10 * 60);

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CursorKind {
    Work,
    User,
    Series,
    Spotlight,
    Comment,
    BookmarkTag,
}

impl CursorKind {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Work => "work",
            Self::User => "user",
            Self::Series => "series",
            Self::Spotlight => "spotlight",
            Self::Comment => "comment",
            Self::BookmarkTag => "bookmark_tag",
        }
    }

    pub fn from_str(s: &str) -> Option<Self> {
        match s {
            "work" => Some(Self::Work),
            "user" => Some(Self::User),
            "series" => Some(Self::Series),
            "spotlight" => Some(Self::Spotlight),
            "comment" => Some(Self::Comment),
            "bookmark_tag" => Some(Self::BookmarkTag),
            _ => None,
        }
    }
}

pub enum CursorSource {
    Illustration(std::sync::Arc<IllustrationFetchEngine>),
    Novel(std::sync::Arc<NovelFetchEngine>),
    User(std::sync::Arc<UserFetchEngine>),
    Series(std::sync::Arc<SeriesFetchEngine>),
    Spotlight(std::sync::Arc<SpotlightFetchEngine>),
    JsonItems(CursorKind, VecDeque<serde_json::Value>),
}

pub struct CursorEntry {
    pub id: String,
    pub kind: CursorKind,
    pub source: CursorSource,
    pub buffer: VecDeque<serde_json::Value>,
    pub is_exhausted: bool,
    pub last_access: Instant,
}

impl CursorEntry {
    pub async fn fetch_page(&mut self, count: usize) -> (Vec<serde_json::Value>, bool) {
        self.last_access = Instant::now();
        let target_plus_one = count + 1;

        while self.buffer.len() < target_plus_one && !self.is_exhausted {
            match &self.source {
                CursorSource::Illustration(engine) => {
                    if let Some(item) = engine.next().await {
                        self.buffer.push_back(illustration_to_json(&item));
                    } else {
                        self.is_exhausted = true;
                    }
                }
                CursorSource::Novel(engine) => {
                    if let Some(item) = engine.next().await {
                        self.buffer.push_back(novel_to_json(&item));
                    } else {
                        self.is_exhausted = true;
                    }
                }
                CursorSource::User(engine) => {
                    if let Some(item) = engine.next().await {
                        self.buffer.push_back(user_to_json(&item));
                    } else {
                        self.is_exhausted = true;
                    }
                }
                CursorSource::Series(engine) => {
                    if let Some(item) = engine.next().await {
                        self.buffer.push_back(series_to_json(&item));
                    } else {
                        self.is_exhausted = true;
                    }
                }
                CursorSource::Spotlight(engine) => {
                    if let Some(item) = engine.next().await {
                        self.buffer.push_back(spotlight_to_json(&item));
                    } else {
                        self.is_exhausted = true;
                    }
                }
                CursorSource::JsonItems(_, queue) => {
                    let mut q = queue.clone();
                    if let Some(item) = q.pop_front() {
                        self.buffer.push_back(item);
                        self.source = CursorSource::JsonItems(self.kind, q);
                    } else {
                        self.is_exhausted = true;
                    }
                }
            }
        }

        let has_more = self.buffer.len() > count;
        let mut result = Vec::with_capacity(count);
        for _ in 0..count {
            if let Some(item) = self.buffer.pop_front() {
                result.push(item);
            } else {
                break;
            }
        }

        (result, has_more)
    }
}

pub struct CursorStore {
    entries: RwLock<HashMap<String, CursorEntry>>,
}

impl Default for CursorStore {
    fn default() -> Self {
        Self::new()
    }
}

impl CursorStore {
    pub fn new() -> Self {
        Self {
            entries: RwLock::new(HashMap::new()),
        }
    }

    pub fn cleanup(&self) {
        let mut entries = self.entries.write();
        let now = Instant::now();
        entries.retain(|_, entry| now.duration_since(entry.last_access) < CURSOR_TTL);

        if entries.len() > MAX_ACTIVE_CURSORS {
            let mut items: Vec<(String, Instant)> = entries
                .iter()
                .map(|(k, v)| (k.clone(), v.last_access))
                .collect();
            items.sort_by_key(|(_, t)| *t);
            let to_remove = entries.len() - MAX_ACTIVE_CURSORS;
            for (k, _) in items.into_iter().take(to_remove) {
                entries.remove(&k);
            }
        }
    }

    pub async fn create_cursor(
        &self,
        kind: CursorKind,
        source: CursorSource,
        count: usize,
    ) -> (Vec<serde_json::Value>, bool, Option<String>) {
        self.cleanup();
        let id = Uuid::new_v4().to_string();
        let mut entry = CursorEntry {
            id: id.clone(),
            kind,
            source,
            buffer: VecDeque::new(),
            is_exhausted: false,
            last_access: Instant::now(),
        };

        let (items, has_more) = entry.fetch_page(count).await;

        if has_more {
            let cursor_token = format!("{CURSOR_PREFIX}:{}:{id}", kind.as_str());
            self.entries.write().insert(id, entry);
            (items, true, Some(cursor_token))
        } else {
            (items, false, None)
        }
    }

    pub async fn more(
        &self,
        cursor: &str,
        count: usize,
    ) -> Result<serde_json::Value, String> {
        self.cleanup();

        let parts: Vec<&str> = cursor.split(':').collect();
        if parts.len() != 3 || parts[0] != CURSOR_PREFIX {
            return Err("The MCP cursor is invalid.".to_string());
        }

        let kind_str = parts[1];
        let id = parts[2];

        let kind = CursorKind::from_str(kind_str)
            .ok_or_else(|| "Unknown cursor kind in token.".to_string())?;

        let mut entry = {
            let mut entries = self.entries.write();
            entries
                .remove(id)
                .ok_or_else(|| "The MCP cursor is expired, completed, or belongs to another result type.".to_string())?
        };

        if entry.kind != kind {
            return Err("The MCP cursor kind does not match.".to_string());
        }

        let (items, has_more) = entry.fetch_page(count).await;

        let next_cursor = if has_more {
            let token = format!("{CURSOR_PREFIX}:{}:{id}", kind.as_str());
            self.entries.write().insert(id.to_string(), entry);
            Some(token)
        } else {
            None
        };

        let count_ret = items.len();
        let res = match kind {
            CursorKind::Work => json!({
                "kind": "Work",
                "count": count_ret,
                "hasMore": has_more,
                "nextCursor": next_cursor,
                "works": items
            }),
            CursorKind::User => json!({
                "kind": "User",
                "count": count_ret,
                "hasMore": has_more,
                "nextCursor": next_cursor,
                "users": items
            }),
            CursorKind::Series => json!({
                "kind": "Series",
                "count": count_ret,
                "hasMore": has_more,
                "nextCursor": next_cursor,
                "series": items
            }),
            CursorKind::Spotlight => json!({
                "kind": "Spotlight",
                "count": count_ret,
                "hasMore": has_more,
                "nextCursor": next_cursor,
                "spotlights": items
            }),
            CursorKind::Comment => json!({
                "kind": "Comment",
                "count": count_ret,
                "hasMore": has_more,
                "nextCursor": next_cursor,
                "comments": items
            }),
            CursorKind::BookmarkTag => json!({
                "kind": "BookmarkTag",
                "count": count_ret,
                "hasMore": has_more,
                "nextCursor": next_cursor,
                "bookmarkTags": items
            }),
        };

        Ok(res)
    }
}

// Helpers to serialize Mako domain models into standard MCP DTO format

pub fn illustration_to_json(illust: &Illustration) -> serde_json::Value {
    let aspect_ratio = if illust.width > 0 && illust.height > 0 {
        Some(illust.width as f64 / illust.height as f64)
    } else {
        None
    };

    let avatar_url = illust
        .user
        .profile_image_urls
        .medium
        .as_deref()
        .or(illust.user.profile_image_urls.px_50x50.as_deref())
        .unwrap_or_default();

    json!({
        "id": illust.id,
        "type": illust.illust_type,
        "title": illust.title,
        "description": illust.caption,
        "author": {
            "id": illust.user.id,
            "name": illust.user.name,
            "account": illust.user.account,
            "avatarUrl": avatar_url,
            "websiteUrl": format!("https://www.pixiv.net/users/{}", illust.user.id),
            "pixevalUri": format!("pixeval://user/{}", illust.user.id)
        },
        "tags": illust.tags.iter().map(|t| json!({
            "name": t.name,
            "translatedName": t.translated_name
        })).collect::<Vec<_>>(),
        "thumbnailUrl": illust.image_urls.square_medium.as_deref().unwrap_or_default(),
        "thumbnailResourceUri": format!("pixeval-resource://illust/{}/medium", illust.id),
        "websiteUrl": format!("https://www.pixiv.net/artworks/{}", illust.id),
        "pixevalUri": format!("pixeval://illust/{}", illust.id),
        "xRestrict": illust.x_restrict,
        "isBookmarked": illust.is_bookmarked,
        "isAiGenerated": illust.illust_ai_type == 2,
        "totalBookmarks": illust.total_bookmarks,
        "totalViews": illust.total_view,
        "pageCount": illust.page_count,
        "width": illust.width,
        "height": illust.height,
        "createDate": illust.create_date,
        "safeRating": if illust.x_restrict > 0 { "R18" } else { "General" },
        "imageType": if illust.illust_type == "ugoira" { "SingleAnimatedImage" } else { "SingleStaticImage" },
        "aspectRatio": aspect_ratio,
        "isAnimated": illust.illust_type == "ugoira"
    })
}

pub fn novel_to_json(novel: &Novel) -> serde_json::Value {
    let avatar_url = novel
        .user
        .profile_image_urls
        .medium
        .as_deref()
        .or(novel.user.profile_image_urls.px_50x50.as_deref())
        .unwrap_or_default();

    json!({
        "id": novel.id,
        "type": "Novel",
        "title": novel.title,
        "description": novel.caption,
        "author": {
            "id": novel.user.id,
            "name": novel.user.name,
            "account": novel.user.account,
            "avatarUrl": avatar_url,
            "websiteUrl": format!("https://www.pixiv.net/users/{}", novel.user.id),
            "pixevalUri": format!("pixeval://user/{}", novel.user.id)
        },
        "tags": novel.tags.iter().map(|t| json!({
            "name": t.name,
            "translatedName": t.translated_name
        })).collect::<Vec<_>>(),
        "thumbnailUrl": novel.image_urls.square_medium.as_deref().unwrap_or_default(),
        "thumbnailResourceUri": format!("pixeval-resource://novel/{}/medium", novel.id),
        "websiteUrl": format!("https://www.pixiv.net/novel/show.php?id={}", novel.id),
        "pixevalUri": format!("pixeval://novel/{}", novel.id),
        "xRestrict": novel.x_restrict,
        "isBookmarked": novel.is_bookmarked,
        "isAiGenerated": novel.novel_ai_type == 2,
        "totalBookmarks": novel.total_bookmarks,
        "totalViews": novel.total_view,
        "pageCount": novel.page_count,
        "width": serde_json::Value::Null,
        "height": serde_json::Value::Null,
        "createDate": novel.create_date,
        "safeRating": if novel.x_restrict > 0 { "R18" } else { "General" },
        "imageType": "None",
        "aspectRatio": serde_json::Value::Null,
        "isAnimated": false,
        "textLength": novel.text_length
    })
}

pub fn user_to_json(user: &User) -> serde_json::Value {
    let avatar_url = user
        .profile_image_urls
        .medium
        .as_deref()
        .or(user.profile_image_urls.px_50x50.as_deref())
        .unwrap_or_default();

    json!({
        "id": user.id,
        "name": user.name,
        "account": user.account,
        "avatarUrl": avatar_url,
        "description": user.comment,
        "isFollowed": user.is_followed,
        "websiteUrl": format!("https://www.pixiv.net/users/{}", user.id),
        "pixevalUri": format!("pixeval://user/{}", user.id)
    })
}

pub fn single_user_response_to_json(res: &pixeval_mako::models::SingleUserResponse) -> serde_json::Value {
    let profile = &res.profile;
    let avatar_url = res
        .user
        .profile_image_urls
        .medium
        .as_deref()
        .or(res.user.profile_image_urls.px_50x50.as_deref())
        .unwrap_or_default();

    json!({
        "id": res.user.id,
        "name": res.user.name,
        "account": res.user.account,
        "avatarUrl": avatar_url,
        "description": res.user.comment,
        "isFollowed": res.user.is_followed,
        "websiteUrl": format!("https://www.pixiv.net/users/{}", res.user.id),
        "pixevalUri": format!("pixeval://user/{}", res.user.id),
        "profile": {
            "totalFollowUsers": profile.total_follow_users,
            "totalIllustrations": profile.total_illusts,
            "totalManga": profile.total_manga,
            "totalNovels": profile.total_novels,
            "totalIllustrationBookmarksPublic": profile.total_illust_bookmarks_public,
            "webpage": profile.web_page,
            "twitterUrl": profile.twitter_url,
            "backgroundImageUrl": profile.background_image_url
        }
    })
}

pub fn series_to_json(series: &Series) -> serde_json::Value {
    let author_json = series.user.as_ref().map(|u| {
        let avatar_url = u
            .profile_image_urls
            .medium
            .as_deref()
            .or(u.profile_image_urls.px_50x50.as_deref())
            .unwrap_or_default();

        json!({
            "id": u.id,
            "name": u.name,
            "account": u.account,
            "avatarUrl": avatar_url
        })
    });

    json!({
        "id": series.id,
        "title": series.title,
        "coverUrl": series.cover_url,
        "publishedContentCount": series.published_content_count,
        "latestContentId": series.latest_content_id,
        "lastPublishedContentDatetime": series.last_published_content_datetime,
        "author": author_json
    })
}

pub fn spotlight_to_json(spotlight: &SpotlightArticle) -> serde_json::Value {
    json!({
        "id": spotlight.id,
        "title": spotlight.title,
        "pureTitle": spotlight.pure_title,
        "thumbnailUrl": spotlight.thumbnail,
        "articleUrl": spotlight.article_url,
        "publishDate": spotlight.publish_date,
        "category": spotlight.category,
        "subcategoryLabel": spotlight.subcategory_label
    })
}

pub fn comment_to_json(comment: &CommentRecord) -> serde_json::Value {
    let avatar_url = comment
        .user
        .profile_image_urls
        .medium
        .as_deref()
        .or(comment.user.profile_image_urls.px_50x50.as_deref())
        .unwrap_or_default();

    json!({
        "id": comment.id,
        "comment": comment.comment,
        "date": comment.date,
        "hasReplies": comment.has_replies,
        "stamp": comment.stamp.as_ref().map(|s| &s.stamp_url),
        "author": {
            "id": comment.user.id,
            "name": comment.user.name,
            "account": comment.user.account,
            "avatarUrl": avatar_url
        }
    })
}

pub fn bookmark_tag_to_json(tag: &BookmarkTag) -> serde_json::Value {
    json!({
        "name": tag.name,
        "count": tag.count,
        "isRegistered": tag.is_registered
    })
}
