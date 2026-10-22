pub fn init_app() {
    // Default utilities - feel free to customize
    let _ = &*crate::app::state::APP_STATE;
}

pub fn version() -> String {
    env!("CARGO_PKG_VERSION").to_string()
}

pub fn ping(message: String) -> String {
    format!("pong from pixeval_native: {message}")
}

pub async fn async_ping(message: String) -> String {
    format!("async pong from pixeval_native: {message}")
}

pub mod ffi {

    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn init_app() {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::init_app())
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn async_ping(message: String) -> String {
        async_compat::Compat::new(async move { super::async_ping(message).await }).await
    }
}
