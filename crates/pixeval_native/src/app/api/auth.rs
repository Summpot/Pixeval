use crate::app::state::APP_STATE;
use pixeval_mako::models::{TokenResponse, TokenUser};

#[derive(Clone, Debug, uniffi::Record)]
#[boltffi::data]
pub struct UserProfile {
    pub id: String,
    pub name: String,
    pub account: String,
    pub mail_address: String,
    pub is_premium: bool,
    pub avatar_url: String,
}

impl From<TokenUser> for UserProfile {
    fn from(u: TokenUser) -> Self {
        let avatar_url = u
            .profile_image_urls
            .medium
            .or(u.profile_image_urls.px_50x50)
            .or(u.profile_image_urls.px_16x16)
            .unwrap_or_default();
        Self {
            id: u.id,
            name: u.name,
            account: u.account,
            mail_address: u.mail_address,
            is_premium: u.is_premium,
            avatar_url,
        }
    }
}

#[derive(Clone, Debug, uniffi::Record)]
#[boltffi::data]
pub struct AuthResponse {
    pub access_token: String,
    pub refresh_token: String,
    pub user: Option<UserProfile>,
}

impl From<TokenResponse> for AuthResponse {
    fn from(r: TokenResponse) -> Self {
        Self {
            access_token: r.access_token,
            refresh_token: r.refresh_token,
            user: r.user.map(Into::into),
        }
    }
}

pub async fn login_by_code(code: String, code_verifier: String) -> Result<AuthResponse, String> {
    let client = &APP_STATE.client;
    let resp = client
        .exchange_code(code, code_verifier)
        .await
        .map_err(|e| format!("Exchange code error: {e}"))?;
    Ok(resp.into())
}

pub async fn login_by_refresh_token(refresh_token: String) -> Result<AuthResponse, String> {
    let client = &APP_STATE.client;
    client.clear_token();
    client.set_refresh_token(refresh_token);
    let resp = client
        .refresh_token()
        .await
        .map_err(|e| format!("Login by refresh token failed: {e}"))?;
    Ok(resp.into())
}

pub async fn refresh_session() -> Result<AuthResponse, String> {
    let client = &APP_STATE.client;
    let resp = client
        .refresh_token()
        .await
        .map_err(|e| format!("Session refresh failed: {e}"))?;
    Ok(resp.into())
}

pub fn get_current_user() -> Option<UserProfile> {
    APP_STATE.client.get_user().map(Into::into)
}

pub fn get_refresh_token() -> Option<String> {
    APP_STATE.client.get_refresh_token()
}

pub fn logout() -> Result<(), String> {
    APP_STATE.client.clear_token();
    APP_STATE
        .get_or_init_storage()?
        .set_mobile_refresh_token(None)
        .map_err(|e| format!("Failed to clear saved session: {e}"))
}

pub fn get_stored_refresh_token() -> Result<Option<String>, String> {
    APP_STATE
        .get_or_init_storage()?
        .get_mobile_refresh_token()
        .map_err(|e| format!("Failed to read saved session: {e}"))
}

#[derive(Clone, Debug, uniffi::Record)]
#[boltffi::data]
pub struct PkceAuthParamsDto {
    pub code_verifier: String,
    pub auth_url: String,
}

pub fn create_pkce_auth_params() -> Result<PkceAuthParamsDto, String> {
    use sha2::{Digest, Sha256};

    let mut raw_bytes = [0u8; 32];
    getrandom::fill(&mut raw_bytes)
        .map_err(|e| format!("Secure random source unavailable: {e}"))?;

    let code_verifier = base64_url_encode(&raw_bytes);

    let mut hasher = Sha256::new();
    hasher.update(code_verifier.as_bytes());
    let hash = hasher.finalize();
    let code_challenge = base64_url_encode(&hash);

    let auth_url = format!(
        "https://app-api.pixiv.net/web/v1/login?code_challenge={code_challenge}&code_challenge_method=S256&client=pixiv-android"
    );

    Ok(PkceAuthParamsDto {
        code_verifier,
        auth_url,
    })
}

fn base64_url_encode(bytes: &[u8]) -> String {
    const CHARSET: &[u8] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    let mut out = String::new();
    let mut i = 0;
    while i < bytes.len() {
        let b0 = bytes[i];
        let b1 = if i + 1 < bytes.len() { bytes[i + 1] } else { 0 };
        let b2 = if i + 2 < bytes.len() { bytes[i + 2] } else { 0 };

        out.push(CHARSET[(b0 >> 2) as usize] as char);
        out.push(CHARSET[(((b0 & 0x03) << 4) | (b1 >> 4)) as usize] as char);
        if i + 1 < bytes.len() {
            out.push(CHARSET[(((b1 & 0x0f) << 2) | (b2 >> 6)) as usize] as char);
        }
        if i + 2 < bytes.len() {
            out.push(CHARSET[(b2 & 0x3f) as usize] as char);
        }
        i += 3;
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;
    use sha2::{Digest, Sha256};

    #[test]
    fn pkce_uses_distinct_verifiers_and_matching_s256_challenges() {
        let first = create_pkce_auth_params().unwrap();
        let second = create_pkce_auth_params().unwrap();
        assert_ne!(first.code_verifier, second.code_verifier);
        assert_eq!(first.code_verifier.len(), 43);
        assert!(
            first
                .code_verifier
                .bytes()
                .all(|c| c.is_ascii_alphanumeric() || b"-_".contains(&c))
        );
        let url = url::Url::parse(&first.auth_url).unwrap();
        let challenge = url
            .query_pairs()
            .find(|(k, _)| k == "code_challenge")
            .unwrap()
            .1
            .into_owned();
        assert_eq!(
            challenge,
            base64_url_encode(&Sha256::digest(first.code_verifier.as_bytes()))
        );
    }
}

pub mod ffi {

    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn login_by_code(
        code: String,
        code_verifier: String,
    ) -> Result<crate::app::api::auth::AuthResponse, String> {
        async_compat::Compat::new(async move { super::login_by_code(code, code_verifier).await })
            .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn login_by_refresh_token(
        refresh_token: String,
    ) -> Result<crate::app::api::auth::AuthResponse, String> {
        async_compat::Compat::new(async move { super::login_by_refresh_token(refresh_token).await })
            .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn refresh_session() -> Result<crate::app::api::auth::AuthResponse, String> {
        async_compat::Compat::new(async move { super::refresh_session().await }).await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn get_current_user() -> Option<crate::app::api::auth::UserProfile> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::get_current_user())
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn get_refresh_token() -> Option<String> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::get_refresh_token())
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn logout() -> Result<(), String> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::logout())
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn get_stored_refresh_token() -> Result<Option<String>, String> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::get_stored_refresh_token())
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn create_pkce_auth_params()
    -> Result<crate::app::api::auth::PkceAuthParamsDto, String> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::create_pkce_auth_params())
                .await
                .expect("native application worker panicked")
        })
        .await
    }
}
