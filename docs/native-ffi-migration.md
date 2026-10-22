# Flutter API migration inventory

Native application services preserve existing behavior while bindings move to the shared UniFFI/BoltFFI contract. Event subscriptions and feed sessions are replaced with explicit owned objects.

| Module | Existing API | New native contract |
| --- | --- | --- |
| auth | `login_by_code` | `pixeval_native` application service |
| auth | `login_by_refresh_token` | `pixeval_native` application service |
| auth | `refresh_session` | `pixeval_native` application service |
| auth | `get_current_user` | `pixeval_native` application service |
| auth | `get_refresh_token` | `pixeval_native` application service |
| auth | `logout` | `pixeval_native` application service |
| auth | `get_stored_refresh_token` | `pixeval_native` application service |
| auth | `create_pkce_auth_params` | `pixeval_native` application service |
| booru | `search_booru` | `pixeval_native` application service |
| deeplink | `parse_pixiv_url` | `pixeval_native` application service |
| download | `create_download_event_stream` | `pixeval_native` application service |
| download | `cancel_download_event_stream` | `pixeval_native` application service |
| download | `init_download_manager` | `pixeval_native` application service |
| download | `start_download` | `pixeval_native` application service |
| download | `pause_download` | `pixeval_native` application service |
| download | `resume_download` | `pixeval_native` application service |
| download | `cancel_download` | `pixeval_native` application service |
| download | `remove_download` | `pixeval_native` application service |
| download | `clear_all_downloads` | `pixeval_native` application service |
| events | `create_session_event_stream` | `pixeval_native` application service |
| events | `cancel_session_event_stream` | `pixeval_native` application service |
| feed | `get_recommended_feed` | `pixeval_native` application service |
| feed | `get_ranking_feed` | `pixeval_native` application service |
| feed | `get_follow_feed` | `pixeval_native` application service |
| feed | `toggle_bookmark` | `pixeval_native` application service |
| feed | `get_illust_detail` | `pixeval_native` application service |
| filters | `analyze_filter_query` | `pixeval_native` application service |
| filters | `filter_artworks_by_query` | `pixeval_native` application service |
| image | `init_image_cache` | `pixeval_native` application service |
| image | `fetch_image` | `pixeval_native` application service |
| image | `get_cached_image` | `pixeval_native` application service |
| image | `clear_image_cache` | `pixeval_native` application service |
| image | `get_cache_stats` | `pixeval_native` application service |
| network | `get_network_config` | `pixeval_native` application service |
| network | `update_network_config` | `pixeval_native` application service |
| network | `test_connectivity` | `pixeval_native` application service |
| novel | `get_novel_detail` | `pixeval_native` application service |
| novel | `get_novel_content_rendered` | `pixeval_native` application service |
| saucenao | `search_saucenao` | `pixeval_native` application service |
| search | `release_search_session` | `pixeval_native` application service |
| search | `get_search_autocomplete` | `pixeval_native` application service |
| search | `search_illustrations` | `pixeval_native` application service |
| simple | `init_app` | `pixeval_native` application service |
| simple | `version` | `pixeval_native` application service |
| simple | `ping` | `pixeval_native` application service |
| simple | `async_ping` | `pixeval_native` application service |
| storage | `init_storage` | `pixeval_native` application service |
| storage | `add_search_history` | `pixeval_native` application service |
| storage | `get_search_history` | `pixeval_native` application service |
| storage | `delete_search_history_item` | `pixeval_native` application service |
| storage | `clear_all_search_history` | `pixeval_native` application service |
| storage | `record_browse_history` | `pixeval_native` application service |
| storage | `get_browse_history` | `pixeval_native` application service |
| storage | `clear_all_browse_history` | `pixeval_native` application service |
| ugoira | `get_ugoira_metadata` | `pixeval_native` application service |
| ugoira | `extract_ugoira_frames_from_zip_bytes` | `pixeval_native` application service |
| ugoira | `export_ugoira` | `pixeval_native` application service |
