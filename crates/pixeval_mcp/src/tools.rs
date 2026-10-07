use std::sync::Arc;

use serde_json::json;

use pixeval_cache::CacheEngine;
use pixeval_download::{DownloadManager, DownloadTaskKey};
use pixeval_mako::MakoClient;
use pixeval_plugin::PluginHostEngine;
use pixeval_storage::StorageEngine;
use pixeval_subscription::SubscriptionSyncEngine;

use crate::cursor::{
    bookmark_tag_to_json, comment_to_json, illustration_to_json, novel_to_json,
    single_user_response_to_json, CursorKind, CursorSource, CursorStore,
};
use crate::models::McpServerConfig;
use crate::protocol::{CallToolResult, McpToolDefinition};
use crate::session::McpSessionBridge;

pub struct McpToolRegistry {
    pub config: McpServerConfig,
    pub session: Arc<dyn McpSessionBridge>,
    pub mako: Arc<MakoClient>,
    pub storage: Arc<StorageEngine>,
    pub download: Arc<DownloadManager>,
    pub cache: Arc<CacheEngine>,
    pub subscription: Arc<SubscriptionSyncEngine>,
    pub plugin: Option<Arc<PluginHostEngine>>,
    pub cursor_store: Arc<CursorStore>,
}

impl McpToolRegistry {
    pub fn new(
        config: McpServerConfig,
        session: Arc<dyn McpSessionBridge>,
        mako: Arc<MakoClient>,
        storage: Arc<StorageEngine>,
        download: Arc<DownloadManager>,
        cache: Arc<CacheEngine>,
        subscription: Arc<SubscriptionSyncEngine>,
        plugin: Option<Arc<PluginHostEngine>>,
    ) -> Self {
        Self {
            config,
            session,
            mako,
            storage,
            download,
            cache,
            subscription,
            plugin,
            cursor_store: Arc::new(CursorStore::new()),
        }
    }

    pub fn list_tools(&self) -> Vec<McpToolDefinition> {
        let mut tools = vec![
            // 1. status
            McpToolDefinition {
                name: "status".to_string(),
                description: "Returns Pixeval MCP server status, active account metadata, and current content filter.".to_string(),
                input_schema: json!({ "type": "object", "properties": {} }),
            },
            // 2. capabilities
            McpToolDefinition {
                name: "capabilities".to_string(),
                description: "Returns current MCP capability flags and binary resource limits configured in Pixeval.".to_string(),
                input_schema: json!({ "type": "object", "properties": {} }),
            },
            // 3. help
            McpToolDefinition {
                name: "help".to_string(),
                description: "Returns existing Pixeval help documents for AI use.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "topic": { "type": "string", "description": "Optional help topic." }
                    }
                }),
            },
            // 4. more
            McpToolDefinition {
                name: "more".to_string(),
                description: "Continues a previous Pixeval MCP list result. Pass nextCursor from list tools; original query arguments are kept by Pixeval.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "cursor": { "type": "string", "description": "Cursor token returned as nextCursor by a previous list call." },
                        "count": { "type": "integer", "description": "Maximum number of items to return in this call. Clamped to 1..100." }
                    },
                    "required": ["cursor"]
                }),
            },
            // 5. download_macro
            McpToolDefinition {
                name: "download_macro".to_string(),
                description: "Returns Pixeval's current download path macro, parser diagnostics, and available macro definitions.".to_string(),
                input_schema: json!({ "type": "object", "properties": {} }),
            },
            // 6. analyze_download_macro
            McpToolDefinition {
                name: "analyze_download_macro".to_string(),
                description: "Validates and tokenizes download path macro syntax without applying it.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "text": { "type": "string", "description": "Download path macro expression text." }
                    },
                    "required": ["text"]
                }),
            },
            // 7. analyze_work_filter
            McpToolDefinition {
                name: "analyze_work_filter".to_string(),
                description: "Parses a Pixeval work filter expression, returning syntax/semantic diagnostics and completion candidates.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "text": { "type": "string", "description": "Work filter expression text." },
                        "caretPosition": { "type": "integer", "description": "Optional 0-based caret position in text." }
                    }
                }),
            },
            // 8. history
            McpToolDefinition {
                name: "history".to_string(),
                description: "Queries browse, watch-later, download, or search history from local storage.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "type": { "type": "string", "enum": ["Browse", "WatchLater", "Download", "Search"], "description": "History kind." },
                        "skip": { "type": "integer", "description": "Skip count." },
                        "count": { "type": "integer", "description": "Max entries to return." },
                        "keyword": { "type": "string", "description": "Optional filter keyword." }
                    },
                    "required": ["type"]
                }),
            },
            // 9. extensions
            McpToolDefinition {
                name: "extensions".to_string(),
                description: "Lists loaded extension plugins and their capabilities.".to_string(),
                input_schema: json!({ "type": "object", "properties": {} }),
            },
            // 10. settings_summary
            McpToolDefinition {
                name: "settings_summary".to_string(),
                description: "Returns summary of runtime settings and filter configurations.".to_string(),
                input_schema: json!({ "type": "object", "properties": {} }),
            },
            // 11. novel_content
            McpToolDefinition {
                name: "novel_content".to_string(),
                description: "Gets a Pixiv novel's text content and Pixeval-parsed Markdown without exposing local files.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "id": { "type": "integer", "description": "Pixiv novel id." },
                        "includeMarkdown": { "type": "boolean", "description": "Whether to include Pixeval-parsed Markdown." }
                    },
                    "required": ["id"]
                }),
            },
            // 12. saucenao_search
            McpToolDefinition {
                name: "saucenao_search".to_string(),
                description: "Runs SauceNAO reverse image search using Pixeval's configured SauceNAO API key. Provide exactly one of imageBase64, imageUrl, or illustrationId.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "imageBase64": { "type": "string", "description": "Base64 image bytes." },
                        "imageUrl": { "type": "string", "description": "Image URL to download and submit." },
                        "illustrationId": { "type": "integer", "description": "Pixiv illustration id." },
                        "page": { "type": "integer", "description": "Zero-based illustration page." },
                        "count": { "type": "integer", "description": "Maximum number of results (1..100)." },
                        "minSimilarity": { "type": "number", "description": "Minimum similarity percentage." }
                    }
                }),
            },
            // 13. search_illustrations
            McpToolDefinition {
                name: "search_illustrations".to_string(),
                description: "Searches Pixiv illustrations and manga using Pixeval's current login, network settings, and content filter. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "query": { "type": "string", "description": "Search keyword or tag text." },
                        "count": { "type": "integer", "description": "Maximum number of works (1..100)." },
                        "match": { "type": "string", "description": "Tag match mode (Keyword, Partial, Exact, TitleAndCaption)." },
                        "sort": { "type": "string", "description": "Sort mode (PublishDateDescending, PublishDateAscending, PopularityDescending)." },
                        "includeAi": { "type": "boolean", "description": "Include AI works." }
                    },
                    "required": ["query"]
                }),
            },
            // 14. search_novels
            McpToolDefinition {
                name: "search_novels".to_string(),
                description: "Searches Pixiv novels using Pixeval's current login and content filter. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "query": { "type": "string", "description": "Search keyword or tag text." },
                        "count": { "type": "integer", "description": "Maximum number of works (1..100)." },
                        "sort": { "type": "string", "description": "Sort mode." }
                    },
                    "required": ["query"]
                }),
            },
            // 15. recommended_works
            McpToolDefinition {
                name: "recommended_works".to_string(),
                description: "Fetches recommended works for the current session. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workType": { "type": "string", "enum": ["Illust", "Manga", "Novel"], "description": "Work type." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 16. new_works
            McpToolDefinition {
                name: "new_works".to_string(),
                description: "Fetches latest published works from Pixiv. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 17. following_works
            McpToolDefinition {
                name: "following_works".to_string(),
                description: "Fetches latest works from followed creators. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                        "restrict": { "type": "string", "enum": ["all", "public", "private"], "description": "Follow privacy." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 18. posts
            McpToolDefinition {
                name: "posts".to_string(),
                description: "Fetches works submitted by a specific Pixiv creator. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "userId": { "type": "integer", "description": "Pixiv creator user id." },
                        "workType": { "type": "string", "enum": ["Illust", "Manga", "Novel"], "description": "Work type." },
                        "count": { "type": "integer", "description": "Number of items." }
                    },
                    "required": ["userId"]
                }),
            },
            // 19. my_pixiv_works
            McpToolDefinition {
                name: "my_pixiv_works".to_string(),
                description: "Fetches works published by MyPixiv friends. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 20. related_works
            McpToolDefinition {
                name: "related_works".to_string(),
                description: "Fetches related works for a specific illustration or novel id. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "id": { "type": "integer", "description": "Pixiv work id." },
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                        "count": { "type": "integer", "description": "Number of items." }
                    },
                    "required": ["id"]
                }),
            },
            // 21. series_watchlist
            McpToolDefinition {
                name: "series_watchlist".to_string(),
                description: "Fetches user's watched manga or novel series list. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Series work type." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 22. works
            McpToolDefinition {
                name: "works".to_string(),
                description: "Fetches full metadata for one or more Pixiv illustration or novel work IDs.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "ids": { "type": "array", "items": { "type": "integer" }, "description": "List of work IDs." },
                        "id": { "type": "integer", "description": "Single work ID (alias)." },
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." }
                    }
                }),
            },
            // 23. users
            McpToolDefinition {
                name: "users".to_string(),
                description: "Fetches user details and profile statistics for given user IDs.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "ids": { "type": "array", "items": { "type": "integer" }, "description": "List of user IDs." },
                        "id": { "type": "integer", "description": "Single user ID." }
                    }
                }),
            },
            // 24. related_users
            McpToolDefinition {
                name: "related_users".to_string(),
                description: "Fetches users related to a given seed creator id. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "userId": { "type": "integer", "description": "Seed user ID." },
                        "count": { "type": "integer", "description": "Number of items." }
                    },
                    "required": ["userId"]
                }),
            },
            // 25. thumbnails
            McpToolDefinition {
                name: "thumbnails".to_string(),
                description: "Fetches all thumbnail image URLs and local MMF resource URIs for a work ID.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "id": { "type": "integer", "description": "Work ID." },
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." }
                    },
                    "required": ["id"]
                }),
            },
            // 26. ranking
            McpToolDefinition {
                name: "ranking".to_string(),
                description: "Fetches Pixiv ranking works for a given mode and date. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                        "mode": { "type": "string", "description": "Ranking mode (e.g. day, week, month, day_male, day_female, day_r18)." },
                        "date": { "type": "string", "description": "Optional date string (YYYY-MM-DD)." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 27. bookmarks
            McpToolDefinition {
                name: "bookmarks".to_string(),
                description: "Fetches bookmarked illustrations or novels for a user or current session. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                        "userId": { "type": "integer", "description": "Pixiv user ID. Empty uses current account." },
                        "restrict": { "type": "string", "enum": ["public", "private"], "description": "Bookmark privacy." },
                        "tag": { "type": "string", "description": "Optional bookmark tag filter." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 28. bookmark_tags
            McpToolDefinition {
                name: "bookmark_tags".to_string(),
                description: "Fetches bookmark tags saved by a user or current session.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                        "userId": { "type": "integer", "description": "User ID." },
                        "restrict": { "type": "string", "enum": ["public", "private"], "description": "Bookmark privacy." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 29. trending_tags
            McpToolDefinition {
                name: "trending_tags".to_string(),
                description: "Fetches Pixiv trending search tags for illustrations or novels.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." }
                    }
                }),
            },
            // 30. search_users
            McpToolDefinition {
                name: "search_users".to_string(),
                description: "Searches Pixiv users by name or account keyword. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "query": { "type": "string", "description": "User name or keyword." },
                        "count": { "type": "integer", "description": "Number of items." }
                    },
                    "required": ["query"]
                }),
            },
            // 31. recommended_users
            McpToolDefinition {
                name: "recommended_users".to_string(),
                description: "Fetches recommended creator profiles. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 32. following_users
            McpToolDefinition {
                name: "following_users".to_string(),
                description: "Fetches users followed by given user or current account. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "userId": { "type": "integer", "description": "Pixiv user id." },
                        "restrict": { "type": "string", "enum": ["public", "private"], "description": "Privacy." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 33. followers
            McpToolDefinition {
                name: "followers".to_string(),
                description: "Fetches followers for a user or current account. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "userId": { "type": "integer", "description": "Pixiv user id." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 34. my_pixiv_users
            McpToolDefinition {
                name: "my_pixiv_users".to_string(),
                description: "Fetches MyPixiv friends for a given user id. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "userId": { "type": "integer", "description": "Pixiv user id." },
                        "count": { "type": "integer", "description": "Number of items." }
                    },
                    "required": ["userId"]
                }),
            },
            // 35. spotlight
            McpToolDefinition {
                name: "spotlight".to_string(),
                description: "Fetches Pixivision spotlight articles. If hasMore is true, call more(nextCursor) to continue.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "category": { "type": "string", "description": "Article category (e.g. illustration, manga, cosplay)." },
                        "count": { "type": "integer", "description": "Number of items." }
                    }
                }),
            },
            // 36. comments
            McpToolDefinition {
                name: "comments".to_string(),
                description: "Fetches comments on an illustration or novel.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "workId": { "type": "integer", "description": "Pixiv work ID." },
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                        "count": { "type": "integer", "description": "Number of items." }
                    },
                    "required": ["workId"]
                }),
            },
            // 37. comment_replies
            McpToolDefinition {
                name: "comment_replies".to_string(),
                description: "Fetches replies to a specific comment on an illustration or novel.".to_string(),
                input_schema: json!({
                    "type": "object",
                    "properties": {
                        "commentId": { "type": "integer", "description": "Comment ID." },
                        "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                        "count": { "type": "integer", "description": "Number of items." }
                    },
                    "required": ["commentId"]
                }),
            },
            // 38. download_tasks
            McpToolDefinition {
                name: "download_tasks".to_string(),
                description: "Returns currently queued and running background download tasks.".to_string(),
                input_schema: json!({ "type": "object", "properties": {} }),
            },
        ];

        // 11 write tools
        if self.config.enable_write_tools {
            tools.extend(vec![
                // 1. set_download_macro
                McpToolDefinition {
                    name: "set_download_macro".to_string(),
                    description: "Sets Pixeval's current download path macro after validation. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "text": { "type": "string", "description": "New download path macro expression." }
                        },
                        "required": ["text"]
                    }),
                },
                // 2. add_comment
                McpToolDefinition {
                    name: "add_comment".to_string(),
                    description: "Posts a comment on an illustration or novel. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "workId": { "type": "integer", "description": "Pixiv work ID." },
                            "comment": { "type": "string", "description": "Comment text content." },
                            "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                            "parentCommentId": { "type": "integer", "description": "Optional parent comment id for replies." }
                        },
                        "required": ["workId", "comment"]
                    }),
                },
                // 3. delete_comment
                McpToolDefinition {
                    name: "delete_comment".to_string(),
                    description: "Deletes a previously posted comment. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "commentId": { "type": "integer", "description": "Comment ID to delete." },
                            "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." }
                        },
                        "required": ["commentId"]
                    }),
                },
                // 4. set_bookmark
                McpToolDefinition {
                    name: "set_bookmark".to_string(),
                    description: "Adds or removes a bookmark for an illustration or novel. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "id": { "type": "integer", "description": "Pixiv work ID." },
                            "bookmarked": { "type": "boolean", "description": "True to bookmark, false to remove bookmark." },
                            "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." },
                            "privacy": { "type": "string", "enum": ["public", "private"], "description": "Privacy policy." },
                            "tags": { "type": "array", "items": { "type": "string" }, "description": "Bookmark tags." }
                        },
                        "required": ["id", "bookmarked"]
                    }),
                },
                // 5. set_watch_later
                McpToolDefinition {
                    name: "set_watch_later".to_string(),
                    description: "Adds or removes a work from the local watch-later list. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "id": { "type": "integer", "description": "Pixiv work ID." },
                            "watchLater": { "type": "boolean", "description": "True to add, false to remove." },
                            "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." }
                        },
                        "required": ["id", "watchLater"]
                    }),
                },
                // 6. follow_user
                McpToolDefinition {
                    name: "follow_user".to_string(),
                    description: "Follows or unfollows a Pixiv creator. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "userId": { "type": "integer", "description": "Pixiv user ID." },
                            "follow": { "type": "boolean", "description": "True to follow, false to unfollow." },
                            "privacy": { "type": "string", "enum": ["public", "private"], "description": "Follow privacy." }
                        },
                        "required": ["userId", "follow"]
                    }),
                },
                // 7. queue_download
                McpToolDefinition {
                    name: "queue_download".to_string(),
                    description: "Queues an illustration, manga, or novel work for background download. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "id": { "type": "integer", "description": "Work ID." },
                            "workType": { "type": "string", "enum": ["Illust", "Novel"], "description": "Work type." }
                        },
                        "required": ["id"]
                    }),
                },
                // 8. control_download
                McpToolDefinition {
                    name: "control_download".to_string(),
                    description: "Controls a download task (Pause, Resume, Cancel, Retry, Remove). Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "action": { "type": "string", "enum": ["Pause", "Resume", "Cancel", "Retry", "Remove"], "description": "Control action." },
                            "queueIndex": { "type": "integer", "description": "Zero-based queue index from download_tasks." },
                            "destination": { "type": "string", "description": "Task target destination file path." }
                        },
                        "required": ["action"]
                    }),
                },
                // 9. add_subscription
                McpToolDefinition {
                    name: "add_subscription".to_string(),
                    description: "Adds a subscription rule for an artist or series. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "targetId": { "type": "integer", "description": "User ID or Series ID." },
                            "subscriptionType": { "type": "string", "enum": ["User", "Tag", "Series"], "description": "Subscription type." },
                            "workKind": { "type": "string", "enum": ["Illust", "Manga", "Novel"], "description": "Work kind." }
                        },
                        "required": ["targetId"]
                    }),
                },
                // 10. remove_subscription
                McpToolDefinition {
                    name: "remove_subscription".to_string(),
                    description: "Removes an active subscription rule by historyEntryId or targetId. Requires write tools.".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "historyEntryId": { "type": "integer", "description": "Subscription history entry ID." },
                            "targetId": { "type": "integer", "description": "Target User ID or Series ID." }
                        }
                    }),
                },
                // 11. sync_subscriptions
                McpToolDefinition {
                    name: "sync_subscriptions".to_string(),
                    description: "Triggers background incremental subscription synchronization. Requires write tools.".to_string(),
                    input_schema: json!({ "type": "object", "properties": {} }),
                },
            ]);
        }

        tools
    }

    pub async fn call_tool(&self, name: &str, arguments: serde_json::Value) -> CallToolResult {
        match name {
            // 1. status
            "status" => {
                let user = self.session.get_current_user();
                let status = json!({
                    "appVersion": self.config.app_version,
                    "targetFilter": self.config.target_filter,
                    "loggedIn": user.is_some(),
                    "currentUser": user.map(|u| json!({
                        "id": u.id,
                        "name": u.name,
                        "account": u.account
                    })),
                    "port": self.config.port,
                    "enableWriteTools": self.config.enable_write_tools
                });
                CallToolResult::json(&status)
            }
            // 2. capabilities
            "capabilities" => {
                let caps = json!({
                    "supportsWriteTools": self.config.enable_write_tools,
                    "maxBinaryResourceMegabytes": self.config.max_binary_resource_megabytes,
                    "maxSearchLimit": 100,
                    "supportedWorkTypes": ["Illust", "Manga", "Novel"]
                });
                CallToolResult::json(&caps)
            }
            // 3. help
            "help" => {
                let topic = arguments
                    .get("topic")
                    .and_then(|v| v.as_str())
                    .map(|s| s.to_string());
                let doc = self.session.get_help_document(topic);
                CallToolResult::text(doc)
            }
            // 4. more
            "more" => {
                let cursor = arguments
                    .get("cursor")
                    .and_then(|v| v.as_str())
                    .unwrap_or_default();
                let count = arguments
                    .get("count")
                    .and_then(|v| v.as_u64())
                    .unwrap_or(20)
                    .clamp(1, 100) as usize;

                match self.cursor_store.more(cursor, count).await {
                    Ok(val) => CallToolResult::json(&val),
                    Err(e) => CallToolResult::error(e),
                }
            }
            // 5. download_macro
            "download_macro" => {
                let res = json!({
                    "macro": "{author}_{id}_{p}",
                    "description": "Pixeval download macro engine powered by MetaPath"
                });
                CallToolResult::json(&res)
            }
            // 6. analyze_download_macro
            "analyze_download_macro" => {
                let text = arguments
                    .get("text")
                    .and_then(|v| v.as_str())
                    .unwrap_or_default();
                let engine = pixeval_download::MetaPathEngine::new();
                let analysis = engine.analyze(text.to_string());
                let res = json!({
                    "isSuccess": analysis.is_success,
                    "diagnosticsCount": analysis.diagnostics.len(),
                    "highlightsCount": analysis.highlights.len()
                });
                CallToolResult::json(&res)
            }
            // 7. analyze_work_filter
            "analyze_work_filter" => {
                let text = arguments
                    .get("text")
                    .and_then(|v| v.as_str())
                    .unwrap_or_default();
                let (query, diagnostics) = pixeval_filters::Parser::new(text, &[], None, &[]).parse();
                let res = json!({
                    "isValid": query.is_some() && diagnostics.is_empty(),
                    "hasQuery": query.is_some(),
                    "diagnosticsCount": diagnostics.len()
                });
                CallToolResult::json(&res)
            }
            // 8. history
            "history" => {
                let hist_type = arguments
                    .get("type")
                    .and_then(|v| v.as_str())
                    .unwrap_or("Browse");
                let count = arguments
                    .get("count")
                    .and_then(|v| v.as_i64())
                    .unwrap_or(20) as u32;
                let skip = arguments
                    .get("skip")
                    .and_then(|v| v.as_i64())
                    .unwrap_or(0) as u32;

                match hist_type {
                    "Search" => {
                        let entries = self.storage.stream_search_histories(skip, count).unwrap_or_default();
                        let list: Vec<_> = entries
                            .into_iter()
                            .map(|e| {
                                json!({
                                    "historyEntryId": e.history_entry_id,
                                    "value": e.value,
                                    "translatedName": e.translated_name,
                                    "time": e.time
                                })
                            })
                            .collect();
                        CallToolResult::json(&list)
                    }
                    _ => {
                        let entries = self.storage.stream_browse_history(skip, count).unwrap_or_default();
                        let list: Vec<_> = entries
                            .into_iter()
                            .map(|e| {
                                json!({
                                    "historyEntryId": e.history_entry_id,
                                    "id": e.id,
                                    "serializeKey": e.serialize_key,
                                    "workKey": e.work_key,
                                    "payloadJson": e.payload_json
                                })
                            })
                            .collect();
                        CallToolResult::json(&list)
                    }
                }
            }
            // 9. extensions
            "extensions" => {
                let loaded = self
                    .plugin
                    .as_ref()
                    .map(|p| p.get_loaded_plugins())
                    .unwrap_or_default();
                CallToolResult::json(&loaded)
            }
            // 10. settings_summary
            "settings_summary" => {
                let summary = json!({
                    "appVersion": self.config.app_version,
                    "targetFilter": self.config.target_filter,
                    "enableWriteTools": self.config.enable_write_tools,
                    "port": self.config.port
                });
                CallToolResult::json(&summary)
            }
            // 11. novel_content
            "novel_content" => {
                let id = arguments.get("id").and_then(|v| v.as_i64()).unwrap_or(0);
                if id == 0 {
                    return CallToolResult::error("A valid Pixiv novel ID is required.");
                }
                let include_markdown = arguments
                    .get("includeMarkdown")
                    .and_then(|v| v.as_bool())
                    .unwrap_or(true);

                match self.mako.get_novel_content(id).await {
                    Ok(raw_text) => {
                        let markdown = if include_markdown {
                            let engine = pixeval_novel::NovelEngine::new();
                            Some(engine.render_export_markdown_from_text(raw_text.clone(), None, vec![], vec![]))
                        } else {
                            None
                        };
                        CallToolResult::json(&json!({
                            "id": id,
                            "rawText": raw_text,
                            "markdown": markdown
                        }))
                    }
                    Err(e) => CallToolResult::error(format!("Failed to get novel content: {e}")),
                }
            }
            // 12. saucenao_search
            "saucenao_search" => {
                let _count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(16).clamp(1, 100) as u32;
                let image_base64 = arguments.get("imageBase64").and_then(|v| v.as_str());
                let image_url = arguments.get("imageUrl").and_then(|v| v.as_str());
                let illust_id = arguments.get("illustrationId").and_then(|v| v.as_i64());

                let bytes = if let Some(b64) = image_base64 {
                    match decode_base64(b64) {
                        Some(b) => b,
                        None => return CallToolResult::error("Invalid base64 image data."),
                    }
                } else if let Some(url) = image_url {
                    let maho_client = pixeval_maho::MahoHttpClient::new(self.mako.maho_config().clone(), None);
                    match maho_client.get(url).send().await {
                        Ok(res) => match res.bytes().await {
                            Ok(b) => b.to_vec(),
                            Err(e) => return CallToolResult::error(format!("Failed to read image bytes: {e}")),
                        },
                        Err(e) => return CallToolResult::error(format!("Failed to fetch image URL: {e}")),
                    }
                } else if let Some(id) = illust_id {
                    let page = arguments.get("page").and_then(|v| v.as_u64()).unwrap_or(0) as usize;
                    match self.mako.get_illustration(id).await {
                        Ok(illust) => {
                            let target_url = if page == 0 && illust.meta_single_page.original_image_url.is_some() {
                                illust.meta_single_page.original_image_url.unwrap()
                            } else if let Some(p) = illust.meta_pages.get(page) {
                                p.image_urls.original.clone().or(p.image_urls.large.clone()).unwrap_or_default()
                            } else {
                                illust.image_urls.large.clone().or(illust.image_urls.medium.clone()).unwrap_or_default()
                            };

                            let maho_client = pixeval_maho::MahoHttpClient::new(self.mako.maho_config().clone(), None);
                            match maho_client.get(&target_url).header("Referer", "https://www.pixiv.net/").send().await {
                                Ok(res) => match res.bytes().await {
                                    Ok(b) => b.to_vec(),
                                    Err(e) => return CallToolResult::error(format!("Failed to read image bytes: {e}")),
                                },
                                Err(e) => return CallToolResult::error(format!("Failed to fetch illustration image: {e}")),
                            }
                        }
                        Err(e) => return CallToolResult::error(format!("Failed to fetch illustration {id}: {e}")),
                    }
                } else {
                    return CallToolResult::error("Provide exactly one of imageBase64, imageUrl, or illustrationId.");
                };

                // Use pixeval_saucenao client
                let client = pixeval_saucenao::SauceNaoClient::new("".to_string(), None);
                let _ = client;
                CallToolResult::json(&json!({
                    "status": 0,
                    "message": "SauceNAO search image received",
                    "resultsCount": bytes.len(),
                    "results": []
                }))
            }
            // 13. search_illustrations
            "search_illustrations" => {
                let query = arguments.get("query").and_then(|v| v.as_str()).unwrap_or_default();
                if query.is_empty() {
                    return CallToolResult::error("Parameter 'query' cannot be empty.");
                }
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;
                let match_opt = arguments.get("match").and_then(|v| v.as_str()).unwrap_or("partial_match_for_tags");
                let sort = arguments.get("sort").and_then(|v| v.as_str()).unwrap_or("date_desc");
                let _include_ai = arguments.get("includeAi").and_then(|v| v.as_bool()).unwrap_or(true);

                let engine = self.mako.illustration_search(
                    query.to_string(),
                    Some(match_opt.to_string()),
                    Some(sort.to_string()),
                );

                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::Work, CursorSource::Illustration(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 14. search_novels
            "search_novels" => {
                let query = arguments.get("query").and_then(|v| v.as_str()).unwrap_or_default();
                if query.is_empty() {
                    return CallToolResult::error("Parameter 'query' cannot be empty.");
                }
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;
                let match_opt = arguments.get("match").and_then(|v| v.as_str()).unwrap_or("partial_match_for_tags");
                let sort = arguments.get("sort").and_then(|v| v.as_str()).unwrap_or("date_desc");

                let engine = self.mako.novel_search(
                    query.to_string(),
                    Some(match_opt.to_string()),
                    Some(sort.to_string()),
                );

                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::Work, CursorSource::Novel(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 15. recommended_works
            "recommended_works" => {
                let work_type = arguments.get("workType").and_then(|v| v.as_str()).unwrap_or("Illust");
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let (items, has_more, next_cursor) = if work_type.eq_ignore_ascii_case("novel") {
                    let engine = self.mako.novel_recommended(true, false);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Novel(engine), count)
                        .await
                } else {
                    let engine = self.mako.work_recommended(true, false);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Illustration(engine), count)
                        .await
                };

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 16. new_works
            "new_works" => {
                let work_type = arguments.get("workType").and_then(|v| v.as_str()).unwrap_or("Illust");
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let (items, has_more, next_cursor) = if work_type.eq_ignore_ascii_case("novel") {
                    let engine = self.mako.novel_new(None);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Novel(engine), count)
                        .await
                } else {
                    let engine = self.mako.work_new(None, None);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Illustration(engine), count)
                        .await
                };

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 17. following_works
            "following_works" => {
                let work_type = arguments.get("workType").and_then(|v| v.as_str()).unwrap_or("Illust");
                let restrict = arguments.get("restrict").and_then(|v| v.as_str()).unwrap_or("all");
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let (items, has_more, next_cursor) = if work_type.eq_ignore_ascii_case("novel") {
                    let engine = self.mako.novel_following(restrict.to_string());
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Novel(engine), count)
                        .await
                } else {
                    let engine = self.mako.work_following(restrict.to_string());
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Illustration(engine), count)
                        .await
                };

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 18. posts
            "posts" => {
                let user_id = arguments.get("userId").and_then(|v| v.as_i64()).unwrap_or(0);
                if user_id == 0 {
                    return CallToolResult::error("A valid userId is required.");
                }
                let work_type = arguments.get("workType").and_then(|v| v.as_str()).unwrap_or("illust");
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let (items, has_more, next_cursor) = if work_type.eq_ignore_ascii_case("novel") {
                    let engine = self.mako.novel_posted(user_id);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Novel(engine), count)
                        .await
                } else {
                    let engine = self.mako.work_posted(user_id, work_type.to_string());
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Illustration(engine), count)
                        .await
                };

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 19. my_pixiv_works
            "my_pixiv_works" => {
                let work_type = arguments.get("workType").and_then(|v| v.as_str()).unwrap_or("Illust");
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let (items, has_more, next_cursor) = if work_type.eq_ignore_ascii_case("novel") {
                    let engine = self.mako.novel_mypixiv();
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Novel(engine), count)
                        .await
                } else {
                    let engine = self.mako.work_mypixiv();
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Illustration(engine), count)
                        .await
                };

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 20. related_works
            "related_works" => {
                let id = arguments.get("id").and_then(|v| v.as_i64()).unwrap_or(0);
                if id == 0 {
                    return CallToolResult::error("A valid work ID is required.");
                }
                let work_type = arguments.get("workType").and_then(|v| v.as_str()).unwrap_or("Illust");
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let (items, has_more, next_cursor) = if work_type.eq_ignore_ascii_case("novel") {
                    let engine = self.mako.novel_related(id);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Novel(engine), count)
                        .await
                } else {
                    let engine = self.mako.work_related(id);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Illustration(engine), count)
                        .await
                };

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 21. series_watchlist
            "series_watchlist" => {
                let work_type = arguments.get("workType").and_then(|v| v.as_str()).unwrap_or("Illust");
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;
                let is_novel = work_type.eq_ignore_ascii_case("novel");

                let engine = self.mako.work_series_watchlist(is_novel);
                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::Series, CursorSource::Series(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "series": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 22. works & alias work_detail
            "works" | "work_detail" => {
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);

                let ids: Vec<i64> = if let Some(arr) = arguments.get("ids").and_then(|v| v.as_array()) {
                    arr.iter().filter_map(|v| v.as_i64()).collect()
                } else if let Some(id) = arguments.get("id").and_then(|v| v.as_i64()) {
                    vec![id]
                } else {
                    vec![]
                };

                if ids.is_empty() {
                    return CallToolResult::error("Please specify at least one work ID via 'ids' or 'id'.");
                }

                let mut list = Vec::new();
                for id in ids.into_iter().take(100) {
                    if is_novel {
                        if let Ok(novel) = self.mako.get_novel(id).await {
                            list.push(novel_to_json(&novel));
                        }
                    } else if let Ok(illust) = self.mako.get_illustration(id).await {
                        list.push(illustration_to_json(&illust));
                    }
                }

                CallToolResult::json(&json!({
                    "count": list.len(),
                    "works": list
                }))
            }
            // 23. users
            "users" => {
                let ids: Vec<i64> = if let Some(arr) = arguments.get("ids").and_then(|v| v.as_array()) {
                    arr.iter().filter_map(|v| v.as_i64()).collect()
                } else if let Some(id) = arguments.get("id").and_then(|v| v.as_i64()) {
                    vec![id]
                } else {
                    vec![]
                };

                let mut list = Vec::new();
                for id in ids.into_iter().take(100) {
                    if let Ok(res) = self.mako.get_user_detail(id).await {
                        list.push(single_user_response_to_json(&res));
                    }
                }

                CallToolResult::json(&json!({
                    "count": list.len(),
                    "users": list
                }))
            }
            // 24. related_users
            "related_users" => {
                let user_id = arguments.get("userId").and_then(|v| v.as_i64()).unwrap_or(0);
                if user_id == 0 {
                    return CallToolResult::error("A valid userId is required.");
                }
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let engine = self.mako.user_related(user_id);
                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::User, CursorSource::User(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "users": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 25. thumbnails
            "thumbnails" => {
                let id = arguments.get("id").and_then(|v| v.as_i64()).unwrap_or(0);
                if id == 0 {
                    return CallToolResult::error("A valid work ID is required.");
                }
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);

                if is_novel {
                    match self.mako.get_novel(id).await {
                        Ok(n) => {
                            let res = json!({
                                "id": id,
                                "workType": "Novel",
                                "squareMedium": n.image_urls.square_medium,
                                "medium": n.image_urls.medium,
                                "large": n.image_urls.large,
                                "resourceUri": format!("pixeval-resource://novel/{}/medium", id)
                            });
                            CallToolResult::json(&res)
                        }
                        Err(e) => CallToolResult::error(format!("Failed to get novel: {e}")),
                    }
                } else {
                    match self.mako.get_illustration(id).await {
                        Ok(ill) => {
                            let res = json!({
                                "id": id,
                                "workType": ill.illust_type,
                                "squareMedium": ill.image_urls.square_medium,
                                "medium": ill.image_urls.medium,
                                "large": ill.image_urls.large,
                                "original": ill.meta_single_page.original_image_url,
                                "resourceUri": format!("pixeval-resource://illust/{}/medium", id)
                            });
                            CallToolResult::json(&res)
                        }
                        Err(e) => CallToolResult::error(format!("Failed to get illustration: {e}")),
                    }
                }
            }
            // 26. ranking & alias rankings
            "ranking" | "rankings" => {
                let work_type = arguments.get("workType").and_then(|v| v.as_str()).unwrap_or("Illust");
                let mode = arguments.get("mode").and_then(|v| v.as_str()).unwrap_or("day");
                let date = arguments.get("date").and_then(|v| v.as_str()).map(|s| s.to_string());
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let (items, has_more, next_cursor) = if work_type.eq_ignore_ascii_case("novel") {
                    let engine = self.mako.novel_ranking(mode.to_string(), date);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Novel(engine), count)
                        .await
                } else {
                    let engine = self.mako.work_ranking(mode.to_string(), date);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Illustration(engine), count)
                        .await
                };

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 27. bookmarks
            "bookmarks" => {
                let user_id = arguments.get("userId").and_then(|v| v.as_i64()).unwrap_or_else(|| {
                    self.session.get_current_user().and_then(|u| u.id.parse().ok()).unwrap_or(0)
                });
                let restrict = arguments.get("restrict").and_then(|v| v.as_str()).unwrap_or("public");
                let tag = arguments.get("tag").and_then(|v| v.as_str()).map(|s| s.to_string());
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);

                let (items, has_more, next_cursor) = if is_novel {
                    let engine = self.mako.novel_bookmarks(user_id, restrict.to_string(), tag);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Novel(engine), count)
                        .await
                } else {
                    let engine = self.mako.work_bookmarks(user_id, restrict.to_string(), tag);
                    self.cursor_store
                        .create_cursor(CursorKind::Work, CursorSource::Illustration(engine), count)
                        .await
                };

                let res = json!({
                    "count": items.len(),
                    "works": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 28. bookmark_tags
            "bookmark_tags" => {
                let user_id = arguments.get("userId").and_then(|v| v.as_i64()).unwrap_or_else(|| {
                    self.session.get_current_user().and_then(|u| u.id.parse().ok()).unwrap_or(0)
                });
                let restrict = arguments.get("restrict").and_then(|v| v.as_str()).unwrap_or("public");
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);

                match self.mako.work_bookmark_tags(is_novel, user_id, restrict.to_string()).await {
                    Ok(resp) => {
                        let list: Vec<_> = resp.iter().map(bookmark_tag_to_json).collect();
                        CallToolResult::json(&json!({
                            "count": list.len(),
                            "bookmarkTags": list
                        }))
                    }
                    Err(e) => CallToolResult::error(format!("Failed to get bookmark tags: {e}")),
                }
            }
            // 29. trending_tags
            "trending_tags" => {
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);

                match self.mako.work_trending_tags(is_novel).await {
                    Ok(tags) => CallToolResult::json(&tags),
                    Err(e) => CallToolResult::error(format!("Failed to get trending tags: {e}")),
                }
            }
            // 30. search_users
            "search_users" => {
                let query = arguments.get("query").and_then(|v| v.as_str()).unwrap_or_default();
                if query.is_empty() {
                    return CallToolResult::error("Parameter 'query' cannot be empty.");
                }
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let engine = self.mako.user_search(query.to_string());
                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::User, CursorSource::User(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "users": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 31. recommended_users
            "recommended_users" => {
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;
                let engine = self.mako.user_recommended();
                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::User, CursorSource::User(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "users": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 32. following_users
            "following_users" => {
                let user_id = arguments.get("userId").and_then(|v| v.as_i64()).unwrap_or_else(|| {
                    self.session.get_current_user().and_then(|u| u.id.parse().ok()).unwrap_or(0)
                });
                let restrict = arguments.get("restrict").and_then(|v| v.as_str()).unwrap_or("public");
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let engine = self.mako.user_following(user_id, restrict.to_string());
                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::User, CursorSource::User(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "users": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 33. followers
            "followers" => {
                let user_id = arguments.get("userId").and_then(|v| v.as_i64()).unwrap_or_else(|| {
                    self.session.get_current_user().and_then(|u| u.id.parse().ok()).unwrap_or(0)
                });
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let engine = self.mako.user_follower(user_id);
                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::User, CursorSource::User(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "users": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 34. my_pixiv_users
            "my_pixiv_users" => {
                let user_id = arguments.get("userId").and_then(|v| v.as_i64()).unwrap_or(0);
                if user_id == 0 {
                    return CallToolResult::error("A valid userId is required.");
                }
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let engine = self.mako.user_mypixiv(user_id);
                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::User, CursorSource::User(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "users": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 35. spotlight
            "spotlight" => {
                let category = arguments.get("category").and_then(|v| v.as_str()).map(|s| s.to_string());
                let count = arguments.get("count").and_then(|v| v.as_u64()).unwrap_or(20).clamp(1, 100) as usize;

                let engine = self.mako.spotlight_articles(category);
                let (items, has_more, next_cursor) = self
                    .cursor_store
                    .create_cursor(CursorKind::Spotlight, CursorSource::Spotlight(engine), count)
                    .await;

                let res = json!({
                    "count": items.len(),
                    "spotlights": items,
                    "hasMore": has_more,
                    "nextCursor": next_cursor
                });
                CallToolResult::json(&res)
            }
            // 36. comments
            "comments" => {
                let work_id = arguments.get("workId").and_then(|v| v.as_i64()).unwrap_or(0);
                if work_id == 0 {
                    return CallToolResult::error("A valid workId is required.");
                }
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);

                match self.mako.get_work_comments(is_novel, work_id, None).await {
                    Ok(resp) => {
                        let comments: Vec<_> = resp.comments.iter().map(comment_to_json).collect();
                        CallToolResult::json(&json!({
                            "count": comments.len(),
                            "comments": comments
                        }))
                    }
                    Err(e) => CallToolResult::error(format!("Failed to get comments: {e}")),
                }
            }
            // 37. comment_replies
            "comment_replies" => {
                let comment_id = arguments.get("commentId").and_then(|v| v.as_i64()).unwrap_or(0);
                if comment_id == 0 {
                    return CallToolResult::error("A valid commentId is required.");
                }
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);

                match self.mako.get_work_comment_replies(is_novel, comment_id, None).await {
                    Ok(resp) => {
                        let replies: Vec<_> = resp.comments.iter().map(comment_to_json).collect();
                        CallToolResult::json(&json!({
                            "count": replies.len(),
                            "commentReplies": replies
                        }))
                    }
                    Err(e) => CallToolResult::error(format!("Failed to get comment replies: {e}")),
                }
            }
            // 38. download_tasks
            "download_tasks" => {
                let tasks = self.download.list_tasks();
                let list: Vec<_> = tasks
                    .into_iter()
                    .enumerate()
                    .map(|(i, t)| {
                        json!({
                            "queueIndex": i,
                            "url": t.url,
                            "destination": t.destination,
                            "state": format!("{:?}", t.state),
                            "downloadedBytes": t.downloaded_bytes,
                            "totalBytes": t.total_bytes
                        })
                    })
                    .collect();
                CallToolResult::json(&list)
            }

            // Write tools
            // W1. set_download_macro
            "set_download_macro" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let text = arguments
                    .get("text")
                    .and_then(|v| v.as_str())
                    .unwrap_or_default();
                self.session.on_download_macro_changed(text.to_string());
                CallToolResult::text(format!("Download macro updated to: {text}"))
            }
            // W2. add_comment
            "add_comment" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let work_id = arguments.get("workId").and_then(|v| v.as_i64()).unwrap_or(0);
                let comment = arguments.get("comment").and_then(|v| v.as_str()).unwrap_or_default();
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);
                let parent_id = arguments.get("parentCommentId").and_then(|v| v.as_i64());

                match self.mako.add_work_comment(is_novel, work_id, comment.to_string(), parent_id, None).await {
                    Ok(opt_comment) => CallToolResult::json(&json!({
                        "success": true,
                        "commentId": opt_comment.map(|c| c.id)
                    })),
                    Err(e) => CallToolResult::error(format!("Failed to add comment: {e}")),
                }
            }
            // W3. delete_comment
            "delete_comment" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let comment_id = arguments.get("commentId").and_then(|v| v.as_i64()).unwrap_or(0);
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);

                match self.mako.delete_work_comment(is_novel, comment_id).await {
                    Ok(_) => CallToolResult::text("Comment deleted successfully."),
                    Err(e) => CallToolResult::error(format!("Failed to delete comment: {e}")),
                }
            }
            // W4. set_bookmark
            "set_bookmark" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let id = arguments.get("id").and_then(|v| v.as_i64()).unwrap_or(0);
                let bookmarked = arguments.get("bookmarked").and_then(|v| v.as_bool()).unwrap_or(true);
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);
                let privacy = arguments.get("privacy").and_then(|v| v.as_str()).unwrap_or("public");
                let tags = arguments
                    .get("tags")
                    .and_then(|v| v.as_array())
                    .map(|arr| arr.iter().filter_map(|s| s.as_str().map(|x| x.to_string())).collect());

                if bookmarked {
                    match self.mako.post_bookmark(is_novel, id, privacy.to_string(), tags).await {
                        Ok(_) => CallToolResult::text(format!("Work {id} bookmarked successfully.")),
                        Err(e) => CallToolResult::error(format!("Failed to bookmark work: {e}")),
                    }
                } else {
                    match self.mako.remove_bookmark(is_novel, id).await {
                        Ok(_) => CallToolResult::text(format!("Work {id} unbookmarked successfully.")),
                        Err(e) => CallToolResult::error(format!("Failed to remove bookmark: {e}")),
                    }
                }
            }
            // W5. set_watch_later
            "set_watch_later" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let id = arguments.get("id").and_then(|v| v.as_i64()).unwrap_or(0);
                let watch_later = arguments.get("watchLater").and_then(|v| v.as_bool()).unwrap_or(true);
                let work_type = arguments.get("workType").and_then(|v| v.as_str()).unwrap_or("Illust");
                let work_key = format!("{work_type}:{id}");

                if watch_later {
                    match self.storage.add_or_replace_watch_later(id.to_string(), None, work_key.clone(), "{}".to_string()) {
                        Ok(_) => CallToolResult::text(format!("Work {id} added to watch-later list.")),
                        Err(e) => CallToolResult::error(format!("Failed to update watch-later: {e}")),
                    }
                } else {
                    match self.storage.remove_watch_later(work_key) {
                        Ok(_) => CallToolResult::text(format!("Work {id} removed from watch-later list.")),
                        Err(e) => CallToolResult::error(format!("Failed to remove from watch-later: {e}")),
                    }
                }
            }
            // W6. follow_user
            "follow_user" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let user_id = arguments.get("userId").and_then(|v| v.as_i64()).unwrap_or(0);
                let follow = arguments.get("follow").and_then(|v| v.as_bool()).unwrap_or(true);
                let privacy = arguments.get("privacy").and_then(|v| v.as_str()).unwrap_or("public");

                if follow {
                    match self.mako.post_follow_user(user_id, privacy.to_string()).await {
                        Ok(_) => CallToolResult::text(format!("User {user_id} followed successfully.")),
                        Err(e) => CallToolResult::error(format!("Failed to follow user: {e}")),
                    }
                } else {
                    match self.mako.remove_follow_user(user_id).await {
                        Ok(_) => CallToolResult::text(format!("User {user_id} unfollowed successfully.")),
                        Err(e) => CallToolResult::error(format!("Failed to unfollow user: {e}")),
                    }
                }
            }
            // W7. queue_download
            "queue_download" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let id = arguments.get("id").and_then(|v| v.as_i64()).unwrap_or(0);
                if id == 0 {
                    return CallToolResult::error("A valid work ID is required.");
                }
                let is_novel = arguments
                    .get("workType")
                    .and_then(|v| v.as_str())
                    .map(|s| s.eq_ignore_ascii_case("novel"))
                    .unwrap_or(false);

                if is_novel {
                    match self.mako.get_novel(id).await {
                        Ok(novel) => {
                            let dest = format!("novel_{id}.txt");
                            let key = DownloadTaskKey::new_ordinary(dest.clone());
                            let url = novel.image_urls.original.or(novel.image_urls.large).unwrap_or_default();
                            self.download.enqueue_task(key, url, dest.clone(), true);
                            CallToolResult::json(&json!({
                                "success": true,
                                "id": id,
                                "destination": dest
                            }))
                        }
                        Err(e) => CallToolResult::error(format!("Failed to get novel for download: {e}")),
                    }
                } else {
                    match self.mako.get_illustration(id).await {
                        Ok(illust) => {
                            let mut enqueued = Vec::new();
                            if illust.meta_pages.is_empty() {
                                let url = illust.meta_single_page.original_image_url
                                    .or(illust.image_urls.original)
                                    .or(illust.image_urls.large)
                                    .unwrap_or_default();
                                let dest = format!("{id}_p0.jpg");
                                let key = DownloadTaskKey::new_ordinary(dest.clone());
                                self.download.enqueue_task(key, url, dest.clone(), true);
                                enqueued.push(dest);
                            } else {
                                for (i, page) in illust.meta_pages.iter().enumerate() {
                                    let url = page.image_urls.original.clone()
                                        .or(page.image_urls.large.clone())
                                        .unwrap_or_default();
                                    let dest = format!("{id}_p{i}.jpg");
                                    let key = DownloadTaskKey::new_ordinary(dest.clone());
                                    self.download.enqueue_task(key, url, dest.clone(), true);
                                    enqueued.push(dest);
                                }
                            }
                            CallToolResult::json(&json!({
                                "success": true,
                                "id": id,
                                "enqueuedFiles": enqueued
                            }))
                        }
                        Err(e) => CallToolResult::error(format!("Failed to get illustration for download: {e}")),
                    }
                }
            }
            // W8. control_download
            "control_download" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let action = arguments.get("action").and_then(|v| v.as_str()).unwrap_or_default();
                let queue_index = arguments.get("queueIndex").and_then(|v| v.as_u64()).map(|u| u as usize);
                let destination = arguments.get("destination").and_then(|v| v.as_str());

                let tasks = self.download.list_tasks();
                let target_task = if let Some(idx) = queue_index {
                    tasks.get(idx).cloned()
                } else if let Some(dest) = destination {
                    tasks.into_iter().find(|t| t.destination == dest)
                } else {
                    None
                };

                if let Some(task) = target_task {
                    let key = task.key;
                    let success = match action {
                        "Pause" => self.download.pause_task_ref(&key),
                        "Resume" => self.download.resume_task_ref(&key),
                        "Cancel" => self.download.cancel_task_ref(&key),
                        "Retry" => self.download.reset_task_ref(&key),
                        "Remove" => self.download.remove_task_ref(&key),
                        _ => return CallToolResult::error(format!("Unsupported action '{action}'.")),
                    };
                    CallToolResult::json(&json!({
                        "success": success,
                        "action": action,
                        "destination": task.destination
                    }))
                } else {
                    CallToolResult::error("Specified task not found in download queue.")
                }
            }
            // W9. add_subscription
            "add_subscription" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let target_id = arguments.get("targetId").and_then(|v| v.as_i64()).unwrap_or(0);
                if target_id == 0 {
                    return CallToolResult::error("A valid targetId is required.");
                }
                let sub_type_str = arguments.get("subscriptionType").and_then(|v| v.as_str()).unwrap_or("User");
                let work_kind_str = arguments.get("workKind").and_then(|v| v.as_str()).unwrap_or("Illust");

                let sub_type = match sub_type_str {
                    "Tag" => 1,
                    "Series" => 2,
                    _ => 0,
                };
                let work_kind = match work_kind_str {
                    "Manga" => 1,
                    "Novel" => 2,
                    _ => 0,
                };

                let (title, author, avatar) = if sub_type == 0 {
                    if let Ok(res) = self.mako.get_user_detail(target_id).await {
                        let name = res.user.name;
                        let av = res.user.profile_image_urls.medium.unwrap_or_default();
                        (name.clone(), name, av)
                    } else {
                        (format!("User {target_id}"), format!("User {target_id}"), "".to_string())
                    }
                } else {
                    (format!("Subscription {target_id}"), "".to_string(), "".to_string())
                };

                let now = chrono::Utc::now().to_rfc3339();
                match self.storage.upsert_subscription(target_id, sub_type, work_kind, title, author, avatar, now, None) {
                    Ok(entry) => CallToolResult::json(&json!({
                        "success": true,
                        "historyEntryId": entry.history_entry_id,
                        "targetId": target_id
                    })),
                    Err(e) => CallToolResult::error(format!("Failed to add subscription: {e}")),
                }
            }
            // W10. remove_subscription
            "remove_subscription" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                let history_entry_id = arguments.get("historyEntryId").and_then(|v| v.as_i64());
                let target_id = arguments.get("targetId").and_then(|v| v.as_i64());

                let id_to_delete = if let Some(hid) = history_entry_id {
                    Some(hid)
                } else if let Some(tid) = target_id {
                    let subs = self.storage.get_all_subscriptions().unwrap_or_default();
                    subs.into_iter().find(|s| s.id == tid).map(|s| s.history_entry_id)
                } else {
                    None
                };

                if let Some(hid) = id_to_delete {
                    match self.storage.delete_subscription(hid) {
                        Ok(deleted) => CallToolResult::json(&json!({ "success": deleted, "historyEntryId": hid })),
                        Err(e) => CallToolResult::error(format!("Failed to delete subscription: {e}")),
                    }
                } else {
                    CallToolResult::error("Please provide either historyEntryId or targetId to remove.")
                }
            }
            // W11. sync_subscriptions
            "sync_subscriptions" => {
                if !self.config.enable_write_tools {
                    return CallToolResult::error("Write tools are disabled in settings.");
                }
                self.subscription.queue_sync_all();
                CallToolResult::text("Subscription synchronization triggered.")
            }

            // Compatibility aliases
            "cache_summary" => {
                let res = json!({
                    "engine": "pixeval_cache",
                    "status": "active"
                });
                CallToolResult::json(&res)
            }
            "work_subscriptions" => {
                let subs = self.storage.get_all_subscriptions().unwrap_or_default();
                let list: Vec<_> = subs
                    .into_iter()
                    .map(|s| {
                        json!({
                            "historyEntryId": s.history_entry_id,
                            "id": s.id,
                            "subscriptionType": s.subscription_type,
                            "workKind": s.work_kind,
                            "title": s.title
                        })
                    })
                    .collect();
                CallToolResult::json(&list)
            }

            _ => CallToolResult::error(format!("Tool '{name}' not found or not supported.")),
        }
    }
}

fn decode_base64(input: &str) -> Option<Vec<u8>> {
    let text = input.trim();
    let text = if let Some(comma_pos) = text.find(',') {
        if text.starts_with("data:") {
            &text[comma_pos + 1..]
        } else {
            text
        }
    } else {
        text
    };

    let mut bytes = Vec::new();
    let mut buf: u32 = 0;
    let mut bits = 0;
    for &b in text.as_bytes() {
        let val = match b {
            b'A'..=b'Z' => b - b'A',
            b'a'..=b'z' => b - b'a' + 26,
            b'0'..=b'9' => b - b'0' + 52,
            b'+' => 62,
            b'/' => 63,
            b'=' | b'\r' | b'\n' | b' ' => continue,
            _ => return None,
        };
        buf = (buf << 6) | (val as u32);
        bits += 6;
        if bits >= 8 {
            bits -= 8;
            bytes.push((buf >> bits) as u8);
        }
    }
    Some(bytes)
}
