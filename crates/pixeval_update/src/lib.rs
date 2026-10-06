uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod downloader;
pub mod engine;
pub mod error;
pub mod github;
pub mod models;
pub mod version;

pub use engine::{UpdateCancellationToken, UpdateEngine, UpdateProgressCallback};
pub use error::UpdateError;
pub use models::{AppRelease, ReleaseAsset, UpdateCheckResult, UpdateNetworkOptions};
pub use version::UpdateState;

#[uniffi::export]
pub fn update_ping() -> String {
    "update_pong".to_string()
}
