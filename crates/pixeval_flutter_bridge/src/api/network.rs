use crate::state::APP_STATE;

#[derive(Clone, Debug)]
pub struct FlutterNetworkConfig {
    pub domain_fronting_enabled: bool,
    pub split_delay_ms: u64,
    pub proxy_url: Option<String>,
    pub mirror_host: Option<String>,
    pub doh_url: Option<String>,
    pub host_ips_json: String,
}

pub fn get_network_config() -> FlutterNetworkConfig {
    let config = APP_STATE.current_config.read();
    let host_ips_json = serde_json::to_string(&config.host_ips).unwrap_or_else(|_| "{}".to_string());
    FlutterNetworkConfig {
        domain_fronting_enabled: config.domain_fronting_enabled,
        split_delay_ms: config.split_delay_ms,
        proxy_url: config.proxy_url.clone(),
        mirror_host: config.mirror_host.clone(),
        doh_url: None,
        host_ips_json,
    }
}

pub fn update_network_config(config: FlutterNetworkConfig) -> Result<(), String> {
    let mut current = APP_STATE.current_config.write();
    current.domain_fronting_enabled = config.domain_fronting_enabled;
    current.split_delay_ms = config.split_delay_ms;
    current.proxy_url = config.proxy_url;
    current.mirror_host = config.mirror_host;

    if !config.host_ips_json.trim().is_empty() {
        if let Ok(parsed) = serde_json::from_str::<std::collections::HashMap<String, Vec<String>>>(&config.host_ips_json) {
            current.host_ips = parsed;
        }
    }

    let res = APP_STATE
        .client
        .update_configuration(current.clone())
        .map_err(|e| format!("Failed to update network configuration: {e}"));

    if res.is_ok() {
        if let Some(cache) = APP_STATE.image_cache.read().as_ref() {
            cache.update_network_options(
                current.domain_fronting_enabled,
                current.split_delay_ms,
                current.host_ips.clone(),
                current.proxy_url.clone(),
            );
        }
        if let Some(dm) = APP_STATE.download_manager.read().as_ref() {
            dm.update_network_options(Some(pixeval_download::DownloadNetworkOptions {
                proxy_url: current.proxy_url.clone(),
                static_domain_ips: current.host_ips.clone(),
            }));
        }
    }
    res
}

#[derive(Clone, Debug)]
pub struct ConnectivityCheckResult {
    pub success: bool,
    pub latency_ms: u64,
    pub message: String,
}

pub async fn test_connectivity() -> ConnectivityCheckResult {
    let start = std::time::Instant::now();
    let client = &APP_STATE.client;
    let feed = client.work_recommended(false, false);
    match feed.next_page().await {
        Ok(_) => ConnectivityCheckResult {
            success: true,
            latency_ms: start.elapsed().as_millis() as u64,
            message: "Successfully connected to Pixiv App API".to_string(),
        },
        Err(e) => ConnectivityCheckResult {
            success: false,
            latency_ms: start.elapsed().as_millis() as u64,
            message: format!("Connection failed: {e}"),
        },
    }
}
