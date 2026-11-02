#[flutter_rust_bridge::frb(init)]
pub fn init_app() {
    // Default utilities - feel free to customize
    flutter_rust_bridge::setup_default_user_utils();
}

pub fn version() -> String {
    env!("CARGO_PKG_VERSION").to_string()
}

pub fn ping(message: String) -> String {
    format!("pong from pixeval_flutter_bridge: {message}")
}

pub async fn async_ping(message: String) -> String {
    format!("async pong from pixeval_flutter_bridge: {message}")
}
