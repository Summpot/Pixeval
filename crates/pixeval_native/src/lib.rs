uniffi::setup_scaffolding!();

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

#[uniffi::export]
pub fn version() -> String {
    env!("CARGO_PKG_VERSION").to_string()
}

#[uniffi::export]
pub async fn ping(message: String) -> String {
    format!("pong: {message}")
}
