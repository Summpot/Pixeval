// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::models::{TokenResponse, TokenUser};
use parking_lot::RwLock;
use std::sync::Arc;
use std::time::{Duration, Instant};

pub const OAUTH_URL: &str = "https://oauth.secure.pixiv.net/auth/token";
pub const CLIENT_ID: &str = "MOBrBDS8blbauoSck0ZfDbtuzpyT";
pub const CLIENT_SECRET: &str = "lsACyCD94FhDUtGTXi3QzcFE2uU1hqtDaKeqrdwj";
pub const REDIRECT_URI: &str = "https://app-api.pixiv.net/web/v1/users/auth/pixiv/callback";

#[derive(Debug, thiserror::Error)]
pub enum AuthError {
    #[error("Network error during auth: {0}")]
    Network(#[from] pixeval_maho::MahoError),
    #[error("No refresh token available")]
    NoRefreshToken,
    #[error("Auth failed with status {0}: {1}")]
    AuthFailed(http::StatusCode, String),
    #[error("Deserialization error: {0}")]
    Json(#[from] serde_json::Error),
}

#[derive(Clone, Debug)]
struct TokenEntry {
    response: TokenResponse,
    obtained_at: Instant,
}

#[derive(Clone)]
pub struct OAuthManager {
    state: Arc<RwLock<Option<TokenEntry>>>,
    refresh_lock: Arc<tokio::sync::Mutex<()>>,
}

impl Default for OAuthManager {
    fn default() -> Self {
        Self::new()
    }
}

impl OAuthManager {
    pub fn new() -> Self {
        Self {
            state: Arc::new(RwLock::new(None)),
            refresh_lock: Arc::new(tokio::sync::Mutex::new(())),
        }
    }

    pub fn set_token_response(&self, resp: TokenResponse) {
        *self.state.write() = Some(TokenEntry {
            response: resp,
            obtained_at: Instant::now(),
        });
    }

    pub fn set_refresh_token(&self, refresh_token: String) {
        let mut guard = self.state.write();
        if let Some(existing) = guard.as_mut() {
            existing.response.refresh_token = refresh_token;
        } else {
            *guard = Some(TokenEntry {
                response: TokenResponse {
                    access_token: String::new(),
                    expires_in: 0,
                    token_type: "Bearer".to_string(),
                    refresh_token,
                    user: None,
                },
                obtained_at: Instant::now(),
            });
        }
    }

    pub fn set_user(&self, user: TokenUser) {
        let mut guard = self.state.write();
        if let Some(existing) = guard.as_mut() {
            existing.response.user = Some(user);
        } else {
            *guard = Some(TokenEntry {
                response: TokenResponse {
                    access_token: String::new(),
                    expires_in: 0,
                    token_type: "Bearer".to_string(),
                    refresh_token: String::new(),
                    user: Some(user),
                },
                obtained_at: Instant::now(),
            });
        }
    }

    pub fn clear(&self) {
        *self.state.write() = None;
    }

    pub fn invalidate_access_token(&self) {
        let mut guard = self.state.write();
        if let Some(existing) = guard.as_mut() {
            existing.response.access_token.clear();
        }
    }

    pub fn get_access_token(&self) -> Option<String> {
        let guard = self.state.read();
        guard
            .as_ref()
            .map(|t| t.response.access_token.clone())
            .filter(|t| !t.is_empty())
    }

    pub fn get_valid_access_token(&self) -> Option<String> {
        let guard = self.state.read();
        if let Some(entry) = guard.as_ref() {
            if entry.response.access_token.is_empty() {
                return None;
            }
            if entry.response.expires_in > 0 {
                // Buffer 300 seconds to refresh beforehand
                let valid_seconds = (entry.response.expires_in as u64).saturating_sub(300);
                if entry.obtained_at.elapsed() >= Duration::from_secs(valid_seconds) {
                    return None;
                }
            }
            return Some(entry.response.access_token.clone());
        }
        None
    }

    pub fn get_refresh_token(&self) -> Option<String> {
        let guard = self.state.read();
        guard
            .as_ref()
            .map(|t| t.response.refresh_token.clone())
            .filter(|t| !t.is_empty())
    }

    pub fn get_user(&self) -> Option<TokenUser> {
        let guard = self.state.read();
        guard.as_ref().and_then(|t| t.response.user.clone())
    }

    pub fn get_token_response(&self) -> Option<TokenResponse> {
        self.state.read().as_ref().map(|t| t.response.clone())
    }

    pub async fn refresh(
        &self,
        client: &pixeval_maho::MahoHttpClient,
    ) -> Result<TokenResponse, AuthError> {
        let _guard = self.refresh_lock.lock().await;
        // Double check after lock
        if self.get_valid_access_token().is_some() {
            if let Some(resp) = self.get_token_response() {
                return Ok(resp);
            }
        }

        let refresh_token = self.get_refresh_token().ok_or(AuthError::NoRefreshToken)?;

        let params = [
            ("client_id", CLIENT_ID),
            ("client_secret", CLIENT_SECRET),
            ("grant_type", "refresh_token"),
            ("refresh_token", &refresh_token),
            ("include_policy", "true"),
        ];

        let resp = client
            .post(OAUTH_URL)
            .header("User-Agent", "PixivAndroidApp/6.140.2 (Android 15.0)")
            .form(&params)
            .send()
            .await?;

        if !resp.status().is_success() {
            let status = resp.status();
            let text =
                String::from_utf8_lossy(&resp.bytes().await.unwrap_or_default()).into_owned();
            return Err(AuthError::AuthFailed(status, text));
        }

        let token_resp: TokenResponse = resp.json().await?;
        self.set_token_response(token_resp.clone());
        Ok(token_resp)
    }

    pub async fn exchange_code(
        &self,
        client: &pixeval_maho::MahoHttpClient,
        code: &str,
        code_verifier: &str,
    ) -> Result<TokenResponse, AuthError> {
        let params = [
            ("client_id", CLIENT_ID),
            ("client_secret", CLIENT_SECRET),
            ("grant_type", "authorization_code"),
            ("code", code),
            ("code_verifier", code_verifier),
            ("redirect_uri", REDIRECT_URI),
            ("include_policy", "true"),
        ];

        let resp = client
            .post(OAUTH_URL)
            .header("User-Agent", "PixivAndroidApp/6.140.2 (Android 15.0)")
            .form(&params)
            .send()
            .await?;

        if !resp.status().is_success() {
            let status = resp.status();
            let text =
                String::from_utf8_lossy(&resp.bytes().await.unwrap_or_default()).into_owned();
            return Err(AuthError::AuthFailed(status, text));
        }

        let token_resp: TokenResponse = resp.json().await?;
        self.set_token_response(token_resp.clone());
        Ok(token_resp)
    }
}
