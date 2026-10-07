// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::sync::Arc;
use std::time::Duration;

use bytes::Bytes;
use pixeval_maho::{DnsResolver, MahoConfig, MahoHttpClient};

use crate::error::SauceNaoError;
use crate::models::{
    RawSauceNaoResponse, SauceNaoItem, SauceNaoSearchResult, map_raw_result,
};

const BASE_URL: &str = "https://saucenao.com/search.php";
const DEFAULT_BOUNDARY: &str = "----PixevalSauceNaoBoundary7MA4YWxkTrZu0gW";

#[derive(uniffi::Object)]
pub struct SauceNaoClient {
    api_key: String,
    http_client: MahoHttpClient,
    num_results: u32,
}

#[uniffi::export(async_runtime = "tokio")]
impl SauceNaoClient {
    #[uniffi::constructor]
    pub fn new(api_key: String, proxy_url: Option<String>) -> Arc<Self> {
        let maho_config = Arc::new(MahoConfig {
            enabled: false,
            split_delay_ms: 0,
            dns_resolver: DnsResolver::new(),
        });
        let clean_proxy = proxy_url
            .as_deref()
            .map(|s| s.trim())
            .filter(|s| !s.is_empty())
            .map(|s| s.to_string());
        let http_client = MahoHttpClient::new(maho_config, clean_proxy);

        Arc::new(Self {
            api_key: api_key.trim().to_string(),
            http_client,
            num_results: 16,
        })
    }

    pub async fn search(&self, file_bytes: Vec<u8>) -> Result<Vec<SauceNaoItem>, SauceNaoError> {
        let detailed = self.search_detailed(file_bytes).await?;
        Ok(detailed.results)
    }

    pub async fn search_detailed(
        &self,
        file_bytes: Vec<u8>,
    ) -> Result<SauceNaoSearchResult, SauceNaoError> {
        if self.api_key.is_empty() {
            return Err(SauceNaoError::InvalidApiKey);
        }

        // SauceNao rate limit retry with exponential backoff (up to 2 retries)
        let mut retries = 0;
        loop {
            match self.execute_search(&file_bytes).await {
                Ok(result) => return Ok(result),
                Err(SauceNaoError::RateLimited { wait_seconds }) if retries < 2 => {
                    retries += 1;
                    let wait = if wait_seconds == 0 { 5 } else { wait_seconds.min(15) };
                    tokio::time::sleep(Duration::from_secs(wait)).await;
                }
                Err(err) => return Err(err),
            }
        }
    }
}

impl SauceNaoClient {
    async fn execute_search(&self, file_bytes: &[u8]) -> Result<SauceNaoSearchResult, SauceNaoError> {
        let boundary = DEFAULT_BOUNDARY;
        let mut body = Vec::with_capacity(file_bytes.len() + 256);
        body.extend_from_slice(format!("--{boundary}\r\n").as_bytes());
        body.extend_from_slice(
            b"Content-Disposition: form-data; name=\"file\"; filename=\"image.jpg\"\r\n",
        );
        body.extend_from_slice(b"Content-Type: application/octet-stream\r\n\r\n");
        body.extend_from_slice(file_bytes);
        body.extend_from_slice(format!("\r\n--{boundary}--\r\n").as_bytes());

        let url = format!(
            "{BASE_URL}?api_key={}&output_type=2&numres={}&db=999&testmode=1",
            urlencoding(&self.api_key),
            self.num_results
        );

        let resp = self
            .http_client
            .post(&url)
            .header(
                "Content-Type",
                format!("multipart/form-data; boundary={boundary}"),
            )
            .body(Bytes::from(body))
            .send()
            .await?;

        let status = resp.status();
        if status.as_u16() == 429 {
            return Err(SauceNaoError::RateLimited { wait_seconds: 30 });
        }
        if status.as_u16() == 403 {
            return Err(SauceNaoError::InvalidApiKey);
        }

        let body_bytes = resp.bytes().await?;
        let raw: RawSauceNaoResponse = match serde_json::from_slice(&body_bytes) {
            Ok(r) => r,
            Err(e) => {
                let text = String::from_utf8_lossy(&body_bytes);
                if text.contains("Rate limit") || text.contains("Too many requests") {
                    return Err(SauceNaoError::RateLimited { wait_seconds: 30 });
                }
                return Err(SauceNaoError::ParseError {
                    message: format!("Failed to parse SauceNao response: {e}. Raw: {text}"),
                });
            }
        };

        let header = raw.header;
        if header.status < 0 {
            let msg = header.message.unwrap_or_default();
            if msg.to_lowercase().contains("limit") {
                return Err(SauceNaoError::RateLimited { wait_seconds: 30 });
            }
            if msg.to_lowercase().contains("api key") || msg.to_lowercase().contains("key") {
                return Err(SauceNaoError::InvalidApiKey);
            }
            return Err(SauceNaoError::ApiError {
                status: header.status,
                message: msg,
            });
        }

        if header.status > 0 {
            return Err(SauceNaoError::ApiError {
                status: header.status,
                message: header.message.unwrap_or_default(),
            });
        }

        let results: Vec<SauceNaoItem> = raw.results.into_iter().map(map_raw_result).collect();
        Ok(SauceNaoSearchResult {
            status: header.status,
            message: header.message.unwrap_or_default(),
            remaining_short: header.short_remaining.unwrap_or(-1),
            remaining_long: header.long_remaining.unwrap_or(-1),
            results,
        })
    }
}

fn urlencoding(input: &str) -> String {
    url::form_urlencoded::byte_serialize(input.as_bytes()).collect()
}
