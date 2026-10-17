uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod downloader;
pub mod engine;
pub mod error;
pub mod github;
pub mod models;
pub mod version;

pub use engine::{UpdateCancellationToken, GithubClient, UpdateProgressCallback};
pub use error::UpdateError;
pub use models::{GitHubUserProfile, AppRelease, ReleaseAsset, UpdateCheckResult, UpdateNetworkOptions};
pub use version::UpdateState;

#[boltffi::export]
#[uniffi::export]
pub fn update_ping() -> String {
    "update_pong".to_string()
}

#[uniffi::export]
#[boltffi::export]
pub fn is_github_host(host: String) -> bool {
    let host = host.to_ascii_lowercase();
    matches!(host.as_str(), "github.com" | "gist.github.com" | "api.github.com" | "github.githubassets.com" | "codeload.github.com")
        || host.ends_with(".githubusercontent.com")
}
