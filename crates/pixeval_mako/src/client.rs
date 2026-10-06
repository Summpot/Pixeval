// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::future::Future;
use std::net::IpAddr;
use std::pin::Pin;
use std::sync::Arc;

use pixeval_maho::{DnsResolver, MahoConfig, MahoHttpClient, MahoResponse};

use crate::auth::{AuthError, OAuthManager};
use crate::models::*;
use crate::stream::{
    IllustrationFetchEngine, MakoFetchEngine, NovelFetchEngine, PageFetchResult, PageFetcher,
    SeriesFetchEngine, SpotlightFetchEngine, UserFetchEngine, WorkFetchEngine,
};
use crate::throttle::RequestThrottler;

pub const APP_API_BASE_URL: &str = "https://app-api.pixiv.net";

#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum MakoError {
    #[error("Network error: {message}")]
    Network { message: String },
    #[error("Authentication error: {message}")]
    Auth { message: String },
    #[error("JSON deserialization error: {message}")]
    Json { message: String },
    #[error("API status error ({code}): {message}")]
    ApiStatus { code: u16, message: String },
    #[error("Unauthorized")]
    Unauthorized,
    #[error("Invalid parameter: {message}")]
    InvalidParameter { message: String },
    #[error("Rate limited: retry after {retry_after_secs}s")]
    RateLimited { retry_after_secs: u64 },
}

#[uniffi::export(callback_interface)]
pub trait MakoSessionCallback: Send + Sync {
    fn on_auth_invalidated(&self, reason: String);
    fn on_rate_limit_encountered(&self, retry_after_seconds: u64);
}

impl From<pixeval_maho::MahoError> for MakoError {
    fn from(err: pixeval_maho::MahoError) -> Self {
        Self::Network {
            message: err.to_string(),
        }
    }
}

impl From<AuthError> for MakoError {
    fn from(err: AuthError) -> Self {
        match err {
            AuthError::Network(e) => Self::Network {
                message: e.to_string(),
            },
            AuthError::AuthFailed(_code, msg) => Self::Auth { message: msg },
            other => Self::Auth {
                message: other.to_string(),
            },
        }
    }
}

impl From<serde_json::Error> for MakoError {
    fn from(err: serde_json::Error) -> Self {
        Self::Json {
            message: err.to_string(),
        }
    }
}

fn url_encode(input: &str) -> String {
    url::form_urlencoded::byte_serialize(input.as_bytes()).collect()
}

#[derive(uniffi::Object, Clone)]
pub struct MakoClient {
    http_client: Arc<parking_lot::RwLock<MahoHttpClient>>,
    oauth: Arc<OAuthManager>,
    throttler: Arc<RequestThrottler>,
    maho_config: Arc<MahoConfig>,
    target_filter: Arc<parking_lot::RwLock<String>>,
    mirror_host: Arc<parking_lot::RwLock<Option<String>>>,
    web_cookie: Arc<parking_lot::RwLock<Option<String>>>,
    session_callback: Arc<parking_lot::RwLock<Option<Box<dyn MakoSessionCallback>>>>,
}

#[uniffi::export(async_runtime = "tokio")]
impl MakoClient {
    #[uniffi::constructor]
    pub fn new(config: MakoConfigurationDto) -> Result<Arc<Self>, MakoError> {
        let http_client = Self::build_client(&config)?;
        let maho_config = http_client.config().clone();
        let target_filter = config
            .target_filter
            .unwrap_or_else(|| "for_android".to_string());
        Ok(Arc::new(Self {
            http_client: Arc::new(parking_lot::RwLock::new(http_client)),
            oauth: Arc::new(OAuthManager::new()),
            throttler: Arc::new(RequestThrottler::new(config.cooldown_ms)),
            maho_config,
            target_filter: Arc::new(parking_lot::RwLock::new(target_filter)),
            mirror_host: Arc::new(parking_lot::RwLock::new(config.mirror_host)),
            web_cookie: Arc::new(parking_lot::RwLock::new(config.web_cookie)),
            session_callback: Arc::new(parking_lot::RwLock::new(None)),
        }))
    }

    pub fn set_session_callback(&self, callback: Option<Box<dyn MakoSessionCallback>>) {
        *self.session_callback.write() = callback;
    }

    pub fn notify_auth_invalidated(&self, reason: &str) {
        if let Some(ref cb) = *self.session_callback.read() {
            cb.on_auth_invalidated(reason.to_string());
        }
    }

    pub fn notify_rate_limit_encountered(&self, retry_after_seconds: u64) {
        if let Some(ref cb) = *self.session_callback.read() {
            cb.on_rate_limit_encountered(retry_after_seconds);
        }
    }

    pub fn update_configuration(&self, config: MakoConfigurationDto) -> Result<(), MakoError> {
        let new_client = Self::build_client(&config)?;
        self.throttler.set_cooldown_ms(config.cooldown_ms);
        *self.http_client.write() = new_client;
        if let Some(tf) = config.target_filter {
            *self.target_filter.write() = tf;
        }
        *self.mirror_host.write() = config.mirror_host;
        *self.web_cookie.write() = config.web_cookie;
        Ok(())
    }

    pub fn set_refresh_token(&self, refresh_token: String) {
        self.oauth.set_refresh_token(refresh_token);
    }

    pub fn clear_token(&self) {
        self.oauth.clear();
    }

    pub fn get_user(&self) -> Option<TokenUser> {
        self.oauth.get_user()
    }

    pub fn set_user(&self, user: TokenUser) {
        self.oauth.set_user(user);
    }

    pub fn get_refresh_token(&self) -> Option<String> {
        self.oauth.get_refresh_token()
    }

    pub fn get_token_response(&self) -> Option<TokenResponse> {
        self.oauth.get_token_response()
    }

    pub async fn identify_token(&self) -> Result<BoolResult, MakoError> {
        match self.refresh_token().await {
            Ok(_) => Ok(BoolResult { success: true }),
            Err(
                MakoError::Auth { .. }
                | MakoError::Unauthorized
                | MakoError::ApiStatus {
                    code: 400..=403, ..
                },
            ) => Ok(BoolResult { success: false }),
            Err(e) => Err(e),
        }
    }

    pub async fn refresh_token(&self) -> Result<TokenResponse, MakoError> {
        let client = self.http_client.read().clone();
        match self.oauth.refresh(&client).await {
            Ok(resp) => Ok(resp),
            Err(e) => {
                if matches!(&e, crate::auth::AuthError::NoRefreshToken | crate::auth::AuthError::AuthFailed(..)) {
                    self.oauth.clear();
                    self.notify_auth_invalidated(&e.to_string());
                }
                Err(e.into())
            }
        }
    }

    pub async fn exchange_code(
        &self,
        code: String,
        code_verifier: String,
    ) -> Result<TokenResponse, MakoError> {
        let client = self.http_client.read().clone();
        Ok(self
            .oauth
            .exchange_code(&client, &code, &code_verifier)
            .await?)
    }

    pub async fn get_illustration(&self, id: i64) -> Result<Illustration, MakoError> {
        let filter = self.target_filter.read().clone();
        let url = format!("{APP_API_BASE_URL}/v1/illust/detail?illust_id={id}&filter={filter}");
        let resp = self.request_get(&url).await?;
        let single: SingleIllustrationResponse = resp.json().await?;
        Ok(single.illust)
    }

    pub async fn get_novel(&self, id: i64) -> Result<Novel, MakoError> {
        let filter = self.target_filter.read().clone();
        let url = format!("{APP_API_BASE_URL}/v2/novel/detail?novel_id={id}&filter={filter}");
        let resp = self.request_get(&url).await?;
        let single: SingleNovelResponse = resp.json().await?;
        Ok(single.novel)
    }

    pub async fn get_user_detail(&self, id: i64) -> Result<SingleUserResponse, MakoError> {
        if id <= 0 {
            return Err(MakoError::InvalidParameter {
                message: "user_id must be greater than 0".to_string(),
            });
        }
        let filter = self.target_filter.read().clone();
        let url = format!("{APP_API_BASE_URL}/v1/user/detail?user_id={id}&filter={filter}");
        let resp = self.request_get(&url).await?;
        let single: SingleUserResponse = resp.json().await?;
        Ok(single)
    }

    pub async fn get_bookmark_detail(
        &self,
        is_novel: bool,
        id: i64,
    ) -> Result<BookmarkDetail, MakoError> {
        let path = if is_novel {
            "/v2/novel/bookmark/detail?novel_id="
        } else {
            "/v2/illust/bookmark/detail?illust_id="
        };
        let url = format!("{APP_API_BASE_URL}{path}{id}");
        let resp = self.request_get(&url).await?;
        let res: BookmarkDetailResponse = resp.json().await?;
        Ok(res.bookmark_detail)
    }

    pub async fn post_bookmark(
        &self,
        is_novel: bool,
        id: i64,
        restrict: String,
        tags: Option<Vec<String>>,
    ) -> Result<BoolResult, MakoError> {
        let path = if is_novel {
            "/v2/novel/bookmark/add"
        } else {
            "/v2/illust/bookmark/add"
        };
        let id_param_name = if is_novel { "novel_id" } else { "illust_id" };
        let url = format!("{APP_API_BASE_URL}{path}");
        let id_str = id.to_string();

        let mut params: Vec<(&str, &str)> = vec![
            (id_param_name, id_str.as_str()),
            ("restrict", restrict.as_str()),
        ];
        if let Some(ref t_list) = tags {
            for tag in t_list {
                if !tag.trim().is_empty() {
                    params.push(("tags[]", tag.as_str()));
                }
            }
        }

        let resp = self.request_post_form(&url, &params).await?;
        Ok(BoolResult {
            success: resp.status().is_success(),
        })
    }

    pub async fn remove_bookmark(&self, is_novel: bool, id: i64) -> Result<BoolResult, MakoError> {
        let path = if is_novel {
            "/v1/novel/bookmark/delete"
        } else {
            "/v1/illust/bookmark/delete"
        };
        let id_param_name = if is_novel { "novel_id" } else { "illust_id" };
        let url = format!("{APP_API_BASE_URL}{path}");
        let id_str = id.to_string();
        let params = [(id_param_name, id_str.as_str())];

        let resp = self.request_post_form(&url, &params).await?;
        Ok(BoolResult {
            success: resp.status().is_success(),
        })
    }

    pub fn work_recommended(
        &self,
        include_ranking: bool,
        include_privacy: bool,
    ) -> Arc<IllustrationFetchEngine> {
        let filter = self.target_filter.read().clone();
        let initial_url = format!(
            "{APP_API_BASE_URL}/v1/illust/recommended?filter={filter}&include_ranking_illusts={include_ranking}&include_privacy_policy={include_privacy}"
        );
        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub fn novel_recommended(
        &self,
        include_ranking: bool,
        include_privacy: bool,
    ) -> Arc<NovelFetchEngine> {
        let filter = self.target_filter.read().clone();
        let initial_url = format!(
            "{APP_API_BASE_URL}/v1/novel/recommended?filter={filter}&include_ranking_novels={include_ranking}&include_privacy_policy={include_privacy}"
        );
        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn work_ranking(&self, mode: String, date: Option<String>) -> Arc<IllustrationFetchEngine> {
        let filter = self.target_filter.read().clone();
        let mut initial_url = format!(
            "{APP_API_BASE_URL}/v1/illust/ranking?filter={filter}&mode={}",
            url_encode(&mode)
        );
        if let Some(d) = date {
            initial_url.push_str(&format!("&date={}", url_encode(&d)));
        }
        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub fn work_bookmarks(
        &self,
        user_id: i64,
        restrict: String,
        tag: Option<String>,
    ) -> Arc<IllustrationFetchEngine> {
        let initial_url = if user_id <= 0 {
            String::new()
        } else {
            let mut url = format!(
                "{APP_API_BASE_URL}/v1/user/bookmarks/illust?user_id={user_id}&restrict={}",
                url_encode(&restrict)
            );
            if let Some(t) = tag {
                url.push_str(&format!("&tag={}", url_encode(&t)));
            }
            url
        };
        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub fn work_new(
        &self,
        content_type: Option<String>,
        max_illust_id: Option<i64>,
    ) -> Arc<IllustrationFetchEngine> {
        let filter = self.target_filter.read().clone();
        let mut initial_url = format!("{APP_API_BASE_URL}/v1/illust/new?filter={filter}");
        if let Some(ref ct) = content_type {
            initial_url.push_str(&format!("&content_type={}", url_encode(ct)));
        }
        if let Some(id) = max_illust_id {
            initial_url.push_str(&format!("&max_illust_id={id}"));
        }
        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub fn novel_new(&self, max_novel_id: Option<i64>) -> Arc<NovelFetchEngine> {
        let filter = self.target_filter.read().clone();
        let mut initial_url = format!("{APP_API_BASE_URL}/v1/novel/new?filter={filter}");
        if let Some(id) = max_novel_id {
            initial_url.push_str(&format!("&max_novel_id={id}"));
        }
        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn illustration_search_advanced(
        &self,
        params: IllustrationSearchParams,
    ) -> Arc<IllustrationFetchEngine> {
        let filter = self.target_filter.read().clone();
        let target = params
            .search_target
            .unwrap_or_else(|| "partial_match_for_tags".to_string());
        let sort_order = params.sort.unwrap_or_else(|| "date_desc".to_string());
        let mut initial_url = format!(
            "{APP_API_BASE_URL}/v1/search/illust?filter={filter}&word={}&search_target={}&sort={}",
            url_encode(&params.word),
            url_encode(&target),
            url_encode(&sort_order)
        );
        if let Some(ai) = params.search_ai_type {
            initial_url.push_str(&format!("&search_ai_type={ai}"));
        }
        if let Some(ct) = params.content_type {
            initial_url.push_str(&format!("&content_type={}", url_encode(&ct)));
        }
        if let Some(rp) = params.ratio_pattern {
            initial_url.push_str(&format!("&ratio_pattern={}", url_encode(&rp)));
        }
        if let Some(v) = params.merge_plain_keyword_results {
            initial_url.push_str(&format!("&merge_plain_keyword_results={v}"));
        }
        if let Some(v) = params.include_translated_tag_results {
            initial_url.push_str(&format!("&include_translated_tag_results={v}"));
        }
        if let Some(v) = params.include_potential_violation_works {
            initial_url.push_str(&format!("&include_potential_violation_works={v}"));
        }
        if let Some(sd) = params.start_date {
            initial_url.push_str(&format!("&start_date={}", url_encode(&sd)));
        }
        if let Some(ed) = params.end_date {
            initial_url.push_str(&format!("&end_date={}", url_encode(&ed)));
        }
        if let Some(w_min) = params.width_min {
            initial_url.push_str(&format!("&width_min={w_min}"));
        }
        if let Some(w_max) = params.width_max {
            initial_url.push_str(&format!("&width_max={w_max}"));
        }
        if let Some(h_min) = params.height_min {
            initial_url.push_str(&format!("&height_min={h_min}"));
        }
        if let Some(h_max) = params.height_max {
            initial_url.push_str(&format!("&height_max={h_max}"));
        }
        if let Some(tool) = params.tool {
            initial_url.push_str(&format!("&tool={}", url_encode(&tool)));
        }

        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub fn illustration_search(
        &self,
        word: String,
        search_target: Option<String>,
        sort: Option<String>,
    ) -> Arc<IllustrationFetchEngine> {
        self.illustration_search_advanced(IllustrationSearchParams {
            word,
            search_target,
            sort,
            ..Default::default()
        })
    }

    pub fn novel_search_advanced(&self, params: NovelSearchParams) -> Arc<NovelFetchEngine> {
        let filter = self.target_filter.read().clone();
        let target = params
            .search_target
            .unwrap_or_else(|| "partial_match_for_tags".to_string());
        let sort_order = params.sort.unwrap_or_else(|| "date_desc".to_string());
        let mut initial_url = format!(
            "{APP_API_BASE_URL}/v1/search/novel?filter={filter}&word={}&search_target={}&sort={}",
            url_encode(&params.word),
            url_encode(&target),
            url_encode(&sort_order)
        );
        if let Some(ai) = params.search_ai_type {
            initial_url.push_str(&format!("&search_ai_type={ai}"));
        }
        if let Some(lang) = params.lang {
            initial_url.push_str(&format!("&lang={}", url_encode(&lang)));
        }
        if let Some(opt) = params.content_length_option {
            if let Some(c_min) = params.content_length_min {
                initial_url.push_str(&format!("&{opt}_min={c_min}"));
            }
            if let Some(c_max) = params.content_length_max {
                initial_url.push_str(&format!("&{opt}_max={c_max}"));
            }
        }
        if let Some(orig) = params.is_original_only {
            initial_url.push_str(&format!("&is_original_only={orig}"));
        }
        if let Some(genre) = params.genre {
            initial_url.push_str(&format!("&genre={genre}"));
        }
        if let Some(rep) = params.is_replaceable_only {
            initial_url.push_str(&format!("&is_replaceable_only={rep}"));
        }
        if let Some(v) = params.merge_plain_keyword_results {
            initial_url.push_str(&format!("&merge_plain_keyword_results={v}"));
        }
        if let Some(v) = params.include_translated_tag_results {
            initial_url.push_str(&format!("&include_translated_tag_results={v}"));
        }
        if let Some(v) = params.include_potential_violation_works {
            initial_url.push_str(&format!("&include_potential_violation_works={v}"));
        }
        if let Some(sd) = params.start_date {
            initial_url.push_str(&format!("&start_date={}", url_encode(&sd)));
        }
        if let Some(ed) = params.end_date {
            initial_url.push_str(&format!("&end_date={}", url_encode(&ed)));
        }

        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn novel_search(
        &self,
        word: String,
        search_target: Option<String>,
        sort: Option<String>,
    ) -> Arc<NovelFetchEngine> {
        self.novel_search_advanced(NovelSearchParams {
            word,
            search_target,
            sort,
            ..Default::default()
        })
    }

    pub fn work_search(
        &self,
        work_type: String,
        keyword: String,
        target: Option<String>,
        sort: Option<String>,
    ) -> Arc<WorkFetchEngine> {
        let filter = self.target_filter.read().clone();
        let is_novel = work_type.eq_ignore_ascii_case("novel");
        let path = if is_novel {
            "/v1/search/novel"
        } else {
            "/v1/search/illust"
        };
        let search_target = target.unwrap_or_else(|| "partial_match_for_tags".to_string());
        let sort_order = sort.unwrap_or_else(|| "date_desc".to_string());
        let initial_url = format!(
            "{APP_API_BASE_URL}{path}?filter={filter}&word={}&search_target={}&sort={}",
            url_encode(&keyword),
            url_encode(&search_target),
            url_encode(&sort_order)
        );

        let fetcher = Arc::new(WorkEntryPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
            is_novel,
        });

        Arc::new(WorkFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn user_recommended(&self) -> Arc<UserFetchEngine> {
        let filter = self.target_filter.read().clone();
        let initial_url = format!("{APP_API_BASE_URL}/v1/user/recommended?filter={filter}");
        let fetcher = Arc::new(UserPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(UserFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn user_search(&self, word: String) -> Arc<UserFetchEngine> {
        let filter = self.target_filter.read().clone();
        let initial_url = format!(
            "{APP_API_BASE_URL}/v1/search/user?filter={filter}&word={}",
            url_encode(&word)
        );
        let fetcher = Arc::new(UserPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(UserFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn user_related(&self, seed_user_id: i64) -> Arc<UserFetchEngine> {
        let initial_url = if seed_user_id <= 0 {
            String::new()
        } else {
            let filter = self.target_filter.read().clone();
            format!("{APP_API_BASE_URL}/v1/user/related?seed_user_id={seed_user_id}&filter={filter}")
        };
        let fetcher = Arc::new(UserPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(UserFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn user_following(&self, user_id: i64, restrict: String) -> Arc<UserFetchEngine> {
        let initial_url = if user_id <= 0 {
            String::new()
        } else {
            format!(
                "{APP_API_BASE_URL}/v1/user/following?user_id={user_id}&restrict={}",
                url_encode(&restrict)
            )
        };
        let fetcher = Arc::new(UserPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(UserFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn user_follower(&self, user_id: i64) -> Arc<UserFetchEngine> {
        let initial_url = if user_id <= 0 {
            String::new()
        } else {
            let filter = self.target_filter.read().clone();
            format!("{APP_API_BASE_URL}/v1/user/follower?user_id={user_id}&filter={filter}")
        };
        let fetcher = Arc::new(UserPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(UserFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn user_mypixiv(&self, user_id: i64) -> Arc<UserFetchEngine> {
        let initial_url = if user_id <= 0 {
            String::new()
        } else {
            format!("{APP_API_BASE_URL}/v1/user/mypixiv?user_id={user_id}")
        };
        let fetcher = Arc::new(UserPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(UserFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub async fn post_follow_user(
        &self,
        user_id: i64,
        restrict: String,
    ) -> Result<BoolResult, MakoError> {
        if user_id <= 0 {
            return Err(MakoError::InvalidParameter {
                message: "user_id must be greater than 0".to_string(),
            });
        }
        let url = format!("{APP_API_BASE_URL}/v1/user/follow/add");
        let id_str = user_id.to_string();
        let params = [
            ("user_id", id_str.as_str()),
            ("restrict", restrict.as_str()),
        ];
        let resp = self.request_post_form(&url, &params).await?;
        Ok(BoolResult {
            success: resp.status().is_success(),
        })
    }

    pub async fn remove_follow_user(&self, user_id: i64) -> Result<BoolResult, MakoError> {
        if user_id <= 0 {
            return Err(MakoError::InvalidParameter {
                message: "user_id must be greater than 0".to_string(),
            });
        }
        let url = format!("{APP_API_BASE_URL}/v1/user/follow/delete");
        let id_str = user_id.to_string();
        let params = [("user_id", id_str.as_str())];
        let resp = self.request_post_form(&url, &params).await?;
        Ok(BoolResult {
            success: resp.status().is_success(),
        })
    }

    pub fn work_following(&self, restrict: String) -> Arc<IllustrationFetchEngine> {
        let initial_url = format!(
            "{APP_API_BASE_URL}/v2/illust/follow?restrict={}",
            url_encode(&restrict)
        );
        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub fn work_mypixiv(&self) -> Arc<IllustrationFetchEngine> {
        let initial_url = format!("{APP_API_BASE_URL}/v2/illust/mypixiv");
        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub fn work_related(&self, illust_id: i64) -> Arc<IllustrationFetchEngine> {
        let filter = self.target_filter.read().clone();
        let initial_url =
            format!("{APP_API_BASE_URL}/v2/illust/related?illust_id={illust_id}&filter={filter}");
        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub fn novel_related(&self, novel_id: i64) -> Arc<NovelFetchEngine> {
        let filter = self.target_filter.read().clone();
        let initial_url =
            format!("{APP_API_BASE_URL}/v1/novel/related?novel_id={novel_id}&filter={filter}");
        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn work_posted(&self, user_id: i64, work_type: String) -> Arc<IllustrationFetchEngine> {
        let initial_url = if user_id <= 0 {
            String::new()
        } else {
            let filter = self.target_filter.read().clone();
            format!(
                "{APP_API_BASE_URL}/v1/user/illusts?user_id={user_id}&type={}&filter={filter}",
                url_encode(&work_type)
            )
        };
        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub fn novel_ranking(&self, mode: String, date: Option<String>) -> Arc<NovelFetchEngine> {
        let filter = self.target_filter.read().clone();
        let mut initial_url = format!(
            "{APP_API_BASE_URL}/v1/novel/ranking?filter={filter}&mode={}",
            url_encode(&mode)
        );
        if let Some(d) = date {
            initial_url.push_str(&format!("&date={}", url_encode(&d)));
        }
        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn novel_bookmarks(
        &self,
        user_id: i64,
        restrict: String,
        tag: Option<String>,
    ) -> Arc<NovelFetchEngine> {
        let initial_url = if user_id <= 0 {
            String::new()
        } else {
            let mut url = format!(
                "{APP_API_BASE_URL}/v1/user/bookmarks/novel?user_id={user_id}&restrict={}",
                url_encode(&restrict)
            );
            if let Some(t) = tag {
                url.push_str(&format!("&tag={}", url_encode(&t)));
            }
            url
        };
        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn novel_following(&self, restrict: String) -> Arc<NovelFetchEngine> {
        let initial_url = format!(
            "{APP_API_BASE_URL}/v1/novel/follow?restrict={}",
            url_encode(&restrict)
        );
        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn novel_mypixiv(&self) -> Arc<NovelFetchEngine> {
        let initial_url = format!("{APP_API_BASE_URL}/v2/novel/mypixiv");
        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn novel_posted(&self, user_id: i64) -> Arc<NovelFetchEngine> {
        let initial_url = if user_id <= 0 {
            String::new()
        } else {
            let filter = self.target_filter.read().clone();
            format!("{APP_API_BASE_URL}/v1/user/novels?user_id={user_id}&filter={filter}")
        };
        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn novel_series(&self, series_id: i64) -> Arc<NovelFetchEngine> {
        let filter = self.target_filter.read().clone();
        let initial_url =
            format!("{APP_API_BASE_URL}/v2/novel/series?series_id={series_id}&filter={filter}");
        let fetcher = Arc::new(NovelPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(NovelFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn work_series(&self, series_id: i64) -> Arc<IllustrationFetchEngine> {
        let filter = self.target_filter.read().clone();
        let initial_url = format!(
            "{APP_API_BASE_URL}/v1/illust/series?illust_series_id={series_id}&filter={filter}"
        );
        let fetcher = Arc::new(IllustrationPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(IllustrationFetchEngine::new(Arc::new(
            MakoFetchEngine::new(fetcher, Some(initial_url)),
        )))
    }

    pub async fn work_bookmark_tags(
        &self,
        is_novel: bool,
        user_id: i64,
        restrict: String,
    ) -> Result<Vec<BookmarkTag>, MakoError> {
        if user_id <= 0 {
            return Err(MakoError::InvalidParameter {
                message: "user_id must be greater than 0".to_string(),
            });
        }
        let kind = if is_novel { "novel" } else { "illust" };
        let filter = self.target_filter.read().clone();
        let restrict_enc = url_encode(&restrict);
        let mut current_url = Some(format!(
            "{APP_API_BASE_URL}/v1/user/bookmark-tags/{kind}?user_id={user_id}&restrict={restrict_enc}&filter={filter}"
        ));
        let mut all_tags = Vec::new();
        let mut page_count = 0;
        while let Some(url) = current_url {
            let resp = self.request_get(&url).await?;
            let page: BookmarkTagsResponse = resp.json().await?;
            all_tags.extend(page.bookmark_tags);
            page_count += 1;
            if page_count >= 50 {
                break;
            }
            current_url = page.next_url;
        }
        Ok(all_tags)
    }

    pub async fn work_trending_tags(&self, is_novel: bool) -> Result<Vec<TrendingTag>, MakoError> {
        let kind = if is_novel { "novel" } else { "illust" };
        let filter = self.target_filter.read().clone();
        let url = format!("{APP_API_BASE_URL}/v1/trending-tags/{kind}?filter={filter}");
        let resp = self.request_get(&url).await?;
        let res: TrendingTagResponse = resp.json().await?;
        Ok(res.trend_tags)
    }

    pub async fn get_novel_content(&self, id: i64) -> Result<String, MakoError> {
        let content = self.get_novel_content_structured(id).await?;
        Ok(content.text)
    }

    pub async fn get_novel_content_structured(&self, id: i64) -> Result<NovelContent, MakoError> {
        let webview_url =
            format!("{APP_API_BASE_URL}/webview/v2/novel?id={id}&viewer_version=20221031_ai");
        if let Ok(resp) = self.request_get(&webview_url).await {
            if let Ok(html) = resp.text().await {
                if let Some(json_str) = extract_novel_json_from_html(&html) {
                    if let Ok(content) = serde_json::from_str::<NovelContent>(&json_str) {
                        return Ok(content);
                    }
                }
            }
        }

        // Fallback to text endpoint if webview fails
        let text_url = format!("{APP_API_BASE_URL}/v1/novel/text?novel_id={id}");
        let text_resp = self.request_get(&text_url).await?;
        let text = text_resp.text().await.unwrap_or_default();
        Ok(NovelContent {
            id,
            text,
            ..Default::default()
        })
    }

    pub async fn get_work_comments(
        &self,
        is_novel: bool,
        work_id: i64,
        offset: Option<i64>,
    ) -> Result<CommentsResponse, MakoError> {
        let filter = self.target_filter.read().clone();
        let mut url = if is_novel {
            format!("{APP_API_BASE_URL}/v3/novel/comments?novel_id={work_id}&filter={filter}")
        } else {
            format!("{APP_API_BASE_URL}/v3/illust/comments?illust_id={work_id}&filter={filter}")
        };
        if let Some(off) = offset {
            url.push_str(&format!("&offset={off}"));
        }
        let resp = self.request_get(&url).await?;
        let res: CommentsResponse = resp.json().await?;
        Ok(res)
    }

    pub async fn get_work_comment_replies(
        &self,
        is_novel: bool,
        comment_id: i64,
        offset: Option<i64>,
    ) -> Result<CommentsResponse, MakoError> {
        let filter = self.target_filter.read().clone();
        let mut url = if is_novel {
            format!(
                "{APP_API_BASE_URL}/v2/novel/comment/replies?comment_id={comment_id}&filter={filter}"
            )
        } else {
            format!(
                "{APP_API_BASE_URL}/v2/illust/comment/replies?comment_id={comment_id}&filter={filter}"
            )
        };
        if let Some(off) = offset {
            url.push_str(&format!("&offset={off}"));
        }
        let resp = self.request_get(&url).await?;
        let res: CommentsResponse = resp.json().await?;
        Ok(res)
    }

    pub async fn get_search_options(&self) -> Result<SearchOptions, MakoError> {
        let url = format!("{APP_API_BASE_URL}/v1/search/options");
        let resp = self.request_get(&url).await?;
        let raw: SearchOptionsRaw = resp.json().await?;
        Ok(SearchOptions {
            illust: IllustrationSearchOptions {
                bookmark_ranges: raw.illust.bookmark_ranges,
                show_ai_condition: raw.illust.show_ai_condition,
                languages: raw.illust.lang.options,
                tools: raw.illust.tool.options,
            },
            novel: NovelSearchOptions {
                bookmark_ranges: raw.novel.bookmark_ranges,
                show_ai_condition: raw.novel.show_ai_condition,
                languages: raw.novel.lang.options,
                genres: raw.novel.genre.options,
                word_count_supported_languages: raw.novel.word_count_supported_languages,
            },
        })
    }

    pub async fn add_work_comment(
        &self,
        is_novel: bool,
        work_id: i64,
        comment: String,
        parent_comment_id: Option<i64>,
        stamp_id: Option<i64>,
    ) -> Result<Option<CommentRecord>, MakoError> {
        let url = if is_novel {
            format!("{APP_API_BASE_URL}/v1/novel/comment/add")
        } else {
            format!("{APP_API_BASE_URL}/v1/illust/comment/add")
        };
        let id_param = if is_novel { "novel_id" } else { "illust_id" };
        let id_str = work_id.to_string();
        let parent_str;
        let stamp_str;
        let mut params = vec![(id_param, id_str.as_str())];
        if !comment.is_empty() {
            params.push(("comment", comment.as_str()));
        }
        if let Some(parent) = parent_comment_id {
            parent_str = parent.to_string();
            params.push(("parent_comment_id", parent_str.as_str()));
        }
        if let Some(stamp) = stamp_id {
            stamp_str = stamp.to_string();
            params.push(("stamp_id", stamp_str.as_str()));
        }
        let resp = self.request_post_form(&url, &params).await?;
        let res: AddCommentResponse = resp.json().await?;
        Ok(res.comment)
    }

    pub async fn delete_work_comment(
        &self,
        is_novel: bool,
        comment_id: i64,
    ) -> Result<BoolResult, MakoError> {
        let url = if is_novel {
            format!("{APP_API_BASE_URL}/v1/novel/comment/delete")
        } else {
            format!("{APP_API_BASE_URL}/v1/illust/comment/delete")
        };
        let id_str = comment_id.to_string();
        let params = [("comment_id", id_str.as_str())];
        let resp = self.request_post_form(&url, &params).await?;
        Ok(BoolResult {
            success: resp.status().is_success(),
        })
    }

    pub async fn get_work_series_context(
        &self,
        illust_id: i64,
    ) -> Result<MangaSeriesContextResult, MakoError> {
        let filter = self.target_filter.read().clone();
        let url = format!(
            "{APP_API_BASE_URL}/v1/illust-series/illust?illust_id={illust_id}&filter={filter}"
        );
        let resp = self.request_get(&url).await?;
        let raw: MangaSeriesContextResponseRaw = resp.json().await?;
        let series = raw.illust_series_detail.or(raw.series);
        let (content_order, prev_illust, next_illust) = if let Some(ctx) = raw.illust_series_context {
            (ctx.content_order.unwrap_or(0), ctx.prev, ctx.next)
        } else {
            (raw.content_order.unwrap_or(0), raw.prev, raw.next)
        };
        Ok(MangaSeriesContextResult {
            series,
            context: MangaSeriesContextInfo {
                content_order,
                prev_illust,
                next_illust,
            },
        })
    }

    pub async fn add_series_watchlist(
        &self,
        is_novel: bool,
        series_id: i64,
    ) -> Result<BoolResult, MakoError> {
        let kind = if is_novel { "novel" } else { "manga" };
        let url = format!("{APP_API_BASE_URL}/v1/watchlist/{kind}/add");
        let id_str = series_id.to_string();
        let params = [("series_id", id_str.as_str())];
        let resp = self.request_post_form(&url, &params).await?;
        Ok(BoolResult {
            success: resp.status().is_success(),
        })
    }

    pub async fn delete_series_watchlist(
        &self,
        is_novel: bool,
        series_id: i64,
    ) -> Result<BoolResult, MakoError> {
        let kind = if is_novel { "novel" } else { "manga" };
        let url = format!("{APP_API_BASE_URL}/v1/watchlist/{kind}/delete");
        let id_str = series_id.to_string();
        let params = [("series_id", id_str.as_str())];
        let resp = self.request_post_form(&url, &params).await?;
        Ok(BoolResult {
            success: resp.status().is_success(),
        })
    }

    pub async fn search_autocomplete(&self, word: String) -> Result<Vec<Tag>, MakoError> {
        let enc_word = url_encode(&word);
        let url = format!(
            "{APP_API_BASE_URL}/v2/search/autocomplete?word={enc_word}&merge_plain_keyword_results=true"
        );
        let resp = self.request_get(&url).await?;
        let res: AutocompleteResponse = resp.json().await?;
        let mut tags = res.tags;
        if tags.is_empty() && !res.search_auto_complete_keywords.is_empty() {
            tags = res
                .search_auto_complete_keywords
                .into_iter()
                .map(|name| Tag {
                    name,
                    translated_name: None,
                })
                .collect();
        }
        Ok(tags)
    }

    pub async fn get_ai_show_settings(&self) -> Result<AiShowSettingsResponse, MakoError> {
        let url = format!("{APP_API_BASE_URL}/v1/user/ai-show-settings");
        let resp = self.request_get(&url).await?;
        let res: AiShowSettingsResponse = resp.json().await?;
        Ok(res)
    }

    pub async fn post_ai_show_settings(
        &self,
        show_ai: bool,
    ) -> Result<AiShowSettingsResponse, MakoError> {
        let url = format!("{APP_API_BASE_URL}/v1/user/ai-show-settings/edit");
        let show_ai_str = show_ai.to_string();
        let params = [("show_ai", show_ai_str.as_str())];
        let resp = self.request_post_form(&url, &params).await?;
        let res: AiShowSettingsResponse = resp.json().await?;
        Ok(res)
    }

    pub async fn get_restricted_mode_settings(
        &self,
    ) -> Result<RestrictedModeSettingsResponse, MakoError> {
        let url = format!("{APP_API_BASE_URL}/v1/user/restricted-mode-settings");
        let resp = self.request_get(&url).await?;
        let res: RestrictedModeSettingsResponse = resp.json().await?;
        Ok(res)
    }

    pub async fn post_restricted_mode_settings(
        &self,
        restricted_mode: bool,
    ) -> Result<RestrictedModeSettingsResponse, MakoError> {
        let url = format!("{APP_API_BASE_URL}/v1/user/restricted-mode-settings");
        let mode_str = restricted_mode.to_string();
        let params = [("is_restricted_mode_enabled", mode_str.as_str())];
        let resp = self.request_post_form(&url, &params).await?;
        let res: RestrictedModeSettingsResponse = resp.json().await?;
        Ok(res)
    }

    pub async fn get_ugoira_metadata(&self, illust_id: i64) -> Result<UgoiraMetadata, MakoError> {
        let url = format!("{APP_API_BASE_URL}/v1/ugoira/metadata?illust_id={illust_id}");
        let resp = self.request_get(&url).await?;
        let res: UgoiraMetadataResponse = resp.json().await?;
        Ok(res.ugoira_metadata)
    }

    pub fn work_series_watchlist(&self, is_novel: bool) -> Arc<SeriesFetchEngine> {
        let kind = if is_novel { "novel" } else { "manga" };
        let initial_url = format!("{APP_API_BASE_URL}/v1/watchlist/{kind}");
        let fetcher = Arc::new(SeriesPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(SeriesFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }

    pub fn spotlight_articles(&self, category: Option<String>) -> Arc<SpotlightFetchEngine> {
        let filter = self.target_filter.read().clone();
        let cat = category.unwrap_or_else(|| "all".to_string());
        let initial_url = format!(
            "{APP_API_BASE_URL}/v1/spotlight/articles?category={}&filter={filter}",
            url_encode(&cat)
        );
        let fetcher = Arc::new(SpotlightPageFetcher {
            client: self.clone(),
            initial_url: initial_url.clone(),
        });
        Arc::new(SpotlightFetchEngine::new(Arc::new(MakoFetchEngine::new(
            fetcher,
            Some(initial_url),
        ))))
    }
}

impl MakoClient {
    async fn get_access_token_or_refresh(&self) -> Result<String, MakoError> {
        if let Some(token) = self.oauth.get_valid_access_token() {
            return Ok(token);
        }
        let resp = self.refresh_token().await?;
        Ok(resp.access_token)
    }

    pub async fn request_get(&self, url: &str) -> Result<MahoResponse, MakoError> {
        let _gate_guard = self.throttler.acquire_gate().await;
        let token = self.get_access_token_or_refresh().await?;

        let mirror = self.mirror_host.read().clone();
        let target_url = if let Some(ref m) = mirror {
            let trimmed = m.trim();
            if !trimmed.is_empty() {
                url.replace("app-api.pixiv.net", trimmed)
            } else {
                url.to_string()
            }
        } else {
            url.to_string()
        };

        let cookie = self.web_cookie.read().clone();
        let client = self.http_client.read().clone();

        let mut req = client
            .get(&target_url)
            .header("Authorization", format!("Bearer {token}"))
            .header("App-OS", "android")
            .header("App-OS-Version", "15.0")
            .header("App-Version", "6.140.2");

        if let Some(ref c) = cookie {
            let trimmed = c.trim();
            if !trimmed.is_empty() {
                req = req.header("Cookie", trimmed);
            }
        }

        let resp = req.send().await?;

        if resp.status() == http::StatusCode::UNAUTHORIZED {
            self.oauth.invalidate_access_token();
            let new_token = match self.refresh_token().await {
                Ok(t) => t.access_token,
                Err(e) => {
                    self.oauth.clear();
                    self.notify_auth_invalidated("refresh_token_failed");
                    return Err(e);
                }
            };
            let mut retry_req = client
                .get(&target_url)
                .header("Authorization", format!("Bearer {new_token}"))
                .header("App-OS", "android")
                .header("App-OS-Version", "15.0")
                .header("App-Version", "6.140.2");
            if let Some(ref c) = cookie {
                let trimmed = c.trim();
                if !trimmed.is_empty() {
                    retry_req = retry_req.header("Cookie", trimmed);
                }
            }
            let retry_resp = retry_req.send().await?;
            if retry_resp.status() == http::StatusCode::UNAUTHORIZED {
                self.oauth.clear();
                self.notify_auth_invalidated("unauthorized");
                return Err(MakoError::Unauthorized);
            }
            if retry_resp.status() == http::StatusCode::TOO_MANY_REQUESTS {
                let retry_after_secs = if let Some(header) = retry_resp.headers().get("Retry-After") {
                    if let Ok(val) = header.to_str() {
                        let dur = RequestThrottler::parse_retry_after(val);
                        self.throttler.update_rate_limit(dur);
                        dur.as_secs()
                    } else {
                        self.throttler.update_rate_limit(std::time::Duration::from_secs(60));
                        60
                    }
                } else {
                    self.throttler.update_rate_limit(std::time::Duration::from_secs(60));
                    60
                };
                self.notify_rate_limit_encountered(retry_after_secs);
                return Err(MakoError::RateLimited { retry_after_secs });
            }
            if !retry_resp.status().is_success() {
                let status = retry_resp.status();
                let text = retry_resp.text().await.unwrap_or_default();
                return Err(MakoError::ApiStatus {
                    code: status.as_u16(),
                    message: text,
                });
            }
            return Ok(retry_resp);
        }

        if resp.status() == http::StatusCode::TOO_MANY_REQUESTS {
            let retry_after_secs = if let Some(header) = resp.headers().get("Retry-After") {
                if let Ok(val) = header.to_str() {
                    let dur = RequestThrottler::parse_retry_after(val);
                    self.throttler.update_rate_limit(dur);
                    dur.as_secs()
                } else {
                    self.throttler.update_rate_limit(std::time::Duration::from_secs(60));
                    60
                }
            } else {
                self.throttler.update_rate_limit(std::time::Duration::from_secs(60));
                60
            };
            self.notify_rate_limit_encountered(retry_after_secs);
            return Err(MakoError::RateLimited { retry_after_secs });
        }

        if !resp.status().is_success() {
            let status = resp.status();
            let text = resp.text().await.unwrap_or_default();
            return Err(MakoError::ApiStatus {
                code: status.as_u16(),
                message: text,
            });
        }

        Ok(resp)
    }

    pub async fn request_post_form(
        &self,
        url: &str,
        params: &[(&str, &str)],
    ) -> Result<MahoResponse, MakoError> {
        let _gate_guard = self.throttler.acquire_gate().await;
        let token = self.get_access_token_or_refresh().await?;

        let mirror = self.mirror_host.read().clone();
        let target_url = if let Some(ref m) = mirror {
            let trimmed = m.trim();
            if !trimmed.is_empty() {
                url.replace("app-api.pixiv.net", trimmed)
            } else {
                url.to_string()
            }
        } else {
            url.to_string()
        };

        let cookie = self.web_cookie.read().clone();
        let client = self.http_client.read().clone();

        let mut req = client
            .post(&target_url)
            .header("Authorization", format!("Bearer {token}"))
            .header("App-OS", "android")
            .header("App-OS-Version", "15.0")
            .header("App-Version", "6.140.2")
            .form(params);

        if let Some(ref c) = cookie {
            let trimmed = c.trim();
            if !trimmed.is_empty() {
                req = req.header("Cookie", trimmed);
            }
        }

        let resp = req.send().await?;

        if resp.status() == http::StatusCode::UNAUTHORIZED {
            self.oauth.invalidate_access_token();
            let new_token = match self.refresh_token().await {
                Ok(t) => t.access_token,
                Err(e) => {
                    self.oauth.clear();
                    self.notify_auth_invalidated("refresh_token_failed");
                    return Err(e);
                }
            };
            let mut retry_req = client
                .post(&target_url)
                .header("Authorization", format!("Bearer {new_token}"))
                .header("App-OS", "android")
                .header("App-OS-Version", "15.0")
                .header("App-Version", "6.140.2")
                .form(params);
            if let Some(ref c) = cookie {
                let trimmed = c.trim();
                if !trimmed.is_empty() {
                    retry_req = retry_req.header("Cookie", trimmed);
                }
            }
            let retry_resp = retry_req.send().await?;
            if retry_resp.status() == http::StatusCode::UNAUTHORIZED {
                self.oauth.clear();
                self.notify_auth_invalidated("unauthorized");
                return Err(MakoError::Unauthorized);
            }
            if retry_resp.status() == http::StatusCode::TOO_MANY_REQUESTS {
                let retry_after_secs = if let Some(header) = retry_resp.headers().get("Retry-After") {
                    if let Ok(val) = header.to_str() {
                        let dur = RequestThrottler::parse_retry_after(val);
                        self.throttler.update_rate_limit(dur);
                        dur.as_secs()
                    } else {
                        self.throttler.update_rate_limit(std::time::Duration::from_secs(60));
                        60
                    }
                } else {
                    self.throttler.update_rate_limit(std::time::Duration::from_secs(60));
                    60
                };
                self.notify_rate_limit_encountered(retry_after_secs);
                return Err(MakoError::RateLimited { retry_after_secs });
            }
            if !retry_resp.status().is_success() {
                let status = retry_resp.status();
                let text = retry_resp.text().await.unwrap_or_default();
                return Err(MakoError::ApiStatus {
                    code: status.as_u16(),
                    message: text,
                });
            }
            return Ok(retry_resp);
        }

        if resp.status() == http::StatusCode::TOO_MANY_REQUESTS {
            let retry_after_secs = if let Some(header) = resp.headers().get("Retry-After") {
                if let Ok(val) = header.to_str() {
                    let dur = RequestThrottler::parse_retry_after(val);
                    self.throttler.update_rate_limit(dur);
                    dur.as_secs()
                } else {
                    self.throttler.update_rate_limit(std::time::Duration::from_secs(60));
                    60
                }
            } else {
                self.throttler.update_rate_limit(std::time::Duration::from_secs(60));
                60
            };
            self.notify_rate_limit_encountered(retry_after_secs);
            return Err(MakoError::RateLimited { retry_after_secs });
        }

        if !resp.status().is_success() {
            let status = resp.status();
            let text = resp.text().await.unwrap_or_default();
            return Err(MakoError::ApiStatus {
                code: status.as_u16(),
                message: text,
            });
        }

        Ok(resp)
    }
}

impl MakoClient {
    pub fn maho_config(&self) -> &Arc<MahoConfig> {
        &self.maho_config
    }

    fn build_client(config: &MakoConfigurationDto) -> Result<MahoHttpClient, MakoError> {
        let resolver = DnsResolver::new();
        if config.domain_fronting_enabled {
            for (host, ips) in &config.host_ips {
                let parsed: Vec<IpAddr> = ips.iter().filter_map(|s| s.parse().ok()).collect();
                resolver.set_static_ips(host, parsed);
            }

            for host in [
                pixeval_maho::APP_API_HOST,
                pixeval_maho::OAUTH_HOST,
                pixeval_maho::IMAGE_HOST,
                pixeval_maho::IMAGE_HOST2,
                pixeval_maho::WEB_API_HOST,
                pixeval_maho::ACCOUNT_HOST,
            ] {
                if let Some(ips) = resolver.get_static_ips(host) {
                    resolver.set_static_ips(host, ips);
                }
            }
        }

        let maho_config = Arc::new(MahoConfig {
            enabled: config.domain_fronting_enabled,
            split_delay_ms: config.split_delay_ms,
            dns_resolver: resolver,
        });

        let clean_proxy = config
            .proxy_url
            .as_deref()
            .map(|s| s.trim())
            .filter(|s| !s.is_empty())
            .map(|s| s.to_string());

        Ok(MahoHttpClient::new(maho_config, clean_proxy))
    }
}

struct IllustrationPageFetcher {
    client: MakoClient,
    initial_url: String,
}

impl PageFetcher<Illustration> for IllustrationPageFetcher {
    fn fetch_page<'a>(
        &'a self,
        next_url: Option<&'a str>,
    ) -> Pin<Box<dyn Future<Output = PageFetchResult<Illustration>> + Send + 'a>> {
        Box::pin(async move {
            let url = match next_url {
                Some(u) => u,
                None => {
                    if self.initial_url.is_empty() {
                        return Ok((Vec::new(), None));
                    }
                    &self.initial_url
                }
            };
            let resp = self
                .client
                .request_get(url)
                .await
                .map_err(|e| e.to_string())?;
            let page: IllustrationResponse = resp.json().await.map_err(|e| e.to_string())?;
            Ok((page.illusts, page.next_url))
        })
    }
}

struct NovelPageFetcher {
    client: MakoClient,
    initial_url: String,
}

impl PageFetcher<Novel> for NovelPageFetcher {
    fn fetch_page<'a>(
        &'a self,
        next_url: Option<&'a str>,
    ) -> Pin<Box<dyn Future<Output = PageFetchResult<Novel>> + Send + 'a>> {
        Box::pin(async move {
            let url = match next_url {
                Some(u) => u,
                None => {
                    if self.initial_url.is_empty() {
                        return Ok((Vec::new(), None));
                    }
                    &self.initial_url
                }
            };
            let resp = self
                .client
                .request_get(url)
                .await
                .map_err(|e| e.to_string())?;
            let page: NovelResponse = resp.json().await.map_err(|e| e.to_string())?;
            Ok((page.novels, page.next_url))
        })
    }
}

struct WorkEntryPageFetcher {
    client: MakoClient,
    initial_url: String,
    is_novel: bool,
}

impl PageFetcher<WorkEntry> for WorkEntryPageFetcher {
    fn fetch_page<'a>(
        &'a self,
        next_url: Option<&'a str>,
    ) -> Pin<Box<dyn Future<Output = PageFetchResult<WorkEntry>> + Send + 'a>> {
        Box::pin(async move {
            let url = match next_url {
                Some(u) => u,
                None => {
                    if self.initial_url.is_empty() {
                        return Ok((Vec::new(), None));
                    }
                    &self.initial_url
                }
            };
            let resp = self
                .client
                .request_get(url)
                .await
                .map_err(|e| e.to_string())?;

            if self.is_novel {
                let page: NovelResponse = resp.json().await.map_err(|e| e.to_string())?;
                let entries: Vec<WorkEntry> = page
                    .novels
                    .into_iter()
                    .map(|novel| WorkEntry::NovelWork { novel })
                    .collect();
                Ok((entries, page.next_url))
            } else {
                let page: IllustrationResponse = resp.json().await.map_err(|e| e.to_string())?;
                let entries: Vec<WorkEntry> = page
                    .illusts
                    .into_iter()
                    .map(|illustration| WorkEntry::Illust { illustration })
                    .collect();
                Ok((entries, page.next_url))
            }
        })
    }
}

struct UserPageFetcher {
    client: MakoClient,
    initial_url: String,
}

impl PageFetcher<User> for UserPageFetcher {
    fn fetch_page<'a>(
        &'a self,
        next_url: Option<&'a str>,
    ) -> Pin<Box<dyn Future<Output = PageFetchResult<User>> + Send + 'a>> {
        Box::pin(async move {
            let url = match next_url {
                Some(u) => u,
                None => {
                    if self.initial_url.is_empty() {
                        return Ok((Vec::new(), None));
                    }
                    &self.initial_url
                }
            };
            let resp = self
                .client
                .request_get(url)
                .await
                .map_err(|e| e.to_string())?;
            let page: UserResponse = resp.json().await.map_err(|e| e.to_string())?;
            let users: Vec<User> = if !page.user_previews.is_empty() {
                page.user_previews
                    .into_iter()
                    .map(|p| {
                        let mut user = p.user;
                        let mut thumbs = Vec::new();
                        for illust in &p.illusts {
                            if let Some(thumb) = illust
                                .image_urls
                                .square_medium
                                .as_ref()
                                .or(illust.image_urls.medium.as_ref())
                                .or(illust.image_urls.large.as_ref())
                            {
                                thumbs.push(thumb.clone());
                                if thumbs.len() >= 3 {
                                    break;
                                }
                            }
                        }
                        if thumbs.len() < 3 {
                            for novel in &p.novels {
                                if let Some(thumb) = novel
                                    .image_urls
                                    .square_medium
                                    .as_ref()
                                    .or(novel.image_urls.medium.as_ref())
                                    .or(novel.image_urls.large.as_ref())
                                {
                                    thumbs.push(thumb.clone());
                                    if thumbs.len() >= 3 {
                                        break;
                                    }
                                }
                            }
                        }
                        user.sample_work_thumbnails = thumbs;
                        user
                    })
                    .collect()
            } else {
                page.users
            };
            Ok((users, page.next_url))
        })
    }
}

struct SeriesPageFetcher {
    client: MakoClient,
    initial_url: String,
}

impl PageFetcher<Series> for SeriesPageFetcher {
    fn fetch_page<'a>(
        &'a self,
        next_url: Option<&'a str>,
    ) -> Pin<Box<dyn Future<Output = PageFetchResult<Series>> + Send + 'a>> {
        Box::pin(async move {
            let url = match next_url {
                Some(u) => u,
                None => {
                    if self.initial_url.is_empty() {
                        return Ok((Vec::new(), None));
                    }
                    &self.initial_url
                }
            };
            let resp = self
                .client
                .request_get(url)
                .await
                .map_err(|e| e.to_string())?;
            let page: SeriesResponse = resp.json().await.map_err(|e| e.to_string())?;
            Ok((page.series, page.next_url))
        })
    }
}

struct SpotlightPageFetcher {
    client: MakoClient,
    initial_url: String,
}

impl PageFetcher<SpotlightArticle> for SpotlightPageFetcher {
    fn fetch_page<'a>(
        &'a self,
        next_url: Option<&'a str>,
    ) -> Pin<Box<dyn Future<Output = PageFetchResult<SpotlightArticle>> + Send + 'a>> {
        Box::pin(async move {
            let url = match next_url {
                Some(u) => u,
                None => {
                    if self.initial_url.is_empty() {
                        return Ok((Vec::new(), None));
                    }
                    &self.initial_url
                }
            };
            let resp = self
                .client
                .request_get(url)
                .await
                .map_err(|e| e.to_string())?;
            let page: SpotlightResponse = resp.json().await.map_err(|e| e.to_string())?;
            Ok((page.spotlight_articles, page.next_url))
        })
    }
}

pub fn extract_novel_json_from_html(html: &str) -> Option<String> {
    let patterns = ["\"novel\":", "novel:", "{\"id\":", "\"seriesNavigation\":"];
    for pattern in patterns {
        if let Some(pos) = html.find(pattern) {
            let slice = &html[pos..];
            if let Some(brace_rel) = slice.find('{') {
                let start_idx = pos + brace_rel;
                let mut depth = 0;
                let mut in_str = false;
                let mut escape = false;
                for (i, ch) in html[start_idx..].char_indices() {
                    if escape {
                        escape = false;
                        continue;
                    }
                    if ch == '\\' && in_str {
                        escape = true;
                        continue;
                    }
                    if ch == '"' {
                        in_str = !in_str;
                        continue;
                    }
                    if !in_str {
                        if ch == '{' {
                            depth += 1;
                        } else if ch == '}' {
                            depth -= 1;
                            if depth == 0 {
                                let end_idx = start_idx + i + 1;
                                return Some(html[start_idx..end_idx].to_string());
                            }
                        }
                    }
                }
            }
        }
    }
    None
}
