uniffi::setup_scaffolding!();

pub mod app;
pub use pixeval_filters::eval::{ArtworkMetadata, ArtworkTag};
pub use pixeval_mako::{Illustration, IllustrationFeedPage};

pixeval_cache::uniffi_reexport_scaffolding!();
pixeval_maho::uniffi_reexport_scaffolding!();
pixeval_filters::uniffi_reexport_scaffolding!();
pixeval_download::uniffi_reexport_scaffolding!();
pixeval_mako::uniffi_reexport_scaffolding!();
pixeval_storage::uniffi_reexport_scaffolding!();
pixeval_subscription::uniffi_reexport_scaffolding!();
pixeval_config::uniffi_reexport_scaffolding!();
pixeval_plugin::uniffi_reexport_scaffolding!();
pixeval_mcp::uniffi_reexport_scaffolding!();
pixeval_novel::uniffi_reexport_scaffolding!();
pixeval_media::uniffi_reexport_scaffolding!();
pixeval_booru::uniffi_reexport_scaffolding!();
pixeval_saucenao::uniffi_reexport_scaffolding!();
pixeval_update::uniffi_reexport_scaffolding!();

pub use pixeval_booru::booru_ping;
pub use pixeval_maho::{is_pixiv_host, locate_server_name, split_client_hello};
pub use pixeval_media::media_ping;
pub use pixeval_saucenao::saucenao_ping;
pub use pixeval_subscription::compute_subscription_folder_snapshot;
pub use pixeval_update::update_ping;

#[uniffi::export]
#[boltffi::export]
pub fn version() -> String {
    env!("CARGO_PKG_VERSION").to_string()
}

#[boltffi::export]
#[uniffi::export]
pub async fn ping(message: String) -> String {
    format!("pong: {message}")
}
