// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::pin::Pin;
use std::sync::Arc;
use std::task::{Context, Poll};
use std::time::Duration;

use bytes::Bytes;
use futures_util::Stream;
use http::header::{CONTENT_TYPE, HeaderMap, HeaderName, HeaderValue, USER_AGENT};
use http::{Method, Request, StatusCode, Uri};
use http_body_util::{BodyExt, Full};
use hyper::body::Body;
use hyper_util::client::legacy::Client;
use hyper_util::rt::TokioExecutor;
use parking_lot::RwLock;
use serde::Serialize;
use serde::de::DeserializeOwned;
use url::form_urlencoded;

use crate::config::MahoConfig;
use crate::connector::MahoConnector;

#[derive(thiserror::Error, Debug, uniffi::Error)]
pub enum MahoError {
    #[error("Network error: {message}")]
    Network { message: String },

    #[error("JSON error: {message}")]
    Json { message: String },

    #[error("HTTP error with status code: {code}")]
    Http { code: u16 },

    #[error("Invalid URL: {message}")]
    Url { message: String },

    #[error("IO error: {message}")]
    Io { message: String },
}

impl From<std::io::Error> for MahoError {
    fn from(err: std::io::Error) -> Self {
        MahoError::Io {
            message: err.to_string(),
        }
    }
}

impl From<hyper::Error> for MahoError {
    fn from(err: hyper::Error) -> Self {
        MahoError::Network {
            message: err.to_string(),
        }
    }
}

impl From<hyper_util::client::legacy::Error> for MahoError {
    fn from(err: hyper_util::client::legacy::Error) -> Self {
        MahoError::Network {
            message: err.to_string(),
        }
    }
}

impl From<serde_json::Error> for MahoError {
    fn from(err: serde_json::Error) -> Self {
        MahoError::Json {
            message: err.to_string(),
        }
    }
}

pub struct MahoByteStream {
    inner: hyper::body::Incoming,
}

impl MahoByteStream {
    pub fn new(inner: hyper::body::Incoming) -> Self {
        Self { inner }
    }
}

impl Stream for MahoByteStream {
    type Item = Result<Bytes, MahoError>;

    fn poll_next(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        let mut fut = async_compat::Compat::new(std::future::poll_fn(|cx| {
            loop {
                match Pin::new(&mut self.inner).poll_frame(cx) {
                    Poll::Ready(Some(Ok(frame))) => {
                        if let Ok(data) = frame.into_data() {
                            if !data.is_empty() {
                                return Poll::Ready(Some(Ok(data)));
                            }
                        }
                    }
                    Poll::Ready(Some(Err(e))) => {
                        return Poll::Ready(Some(Err(MahoError::Network {
                            message: e.to_string(),
                        })));
                    }
                    Poll::Ready(None) => return Poll::Ready(None),
                    Poll::Pending => return Poll::Pending,
                }
            }
        }));
        Pin::new(&mut fut).poll(cx)
    }
}

pub struct MahoResponse {
    status: StatusCode,
    headers: HeaderMap,
    body: hyper::body::Incoming,
}

impl MahoResponse {
    pub fn new(status: StatusCode, headers: HeaderMap, body: hyper::body::Incoming) -> Self {
        Self {
            status,
            headers,
            body,
        }
    }

    pub fn status(&self) -> StatusCode {
        self.status
    }

    pub fn headers(&self) -> &HeaderMap {
        &self.headers
    }

    pub fn content_length(&self) -> Option<u64> {
        self.headers
            .get(http::header::CONTENT_LENGTH)
            .and_then(|v| v.to_str().ok())
            .and_then(|s| s.parse().ok())
    }

    pub async fn bytes(self) -> Result<Bytes, MahoError> {
        async_compat::Compat::new(async move {
            let collected = self.body.collect().await.map_err(|e| MahoError::Network {
                message: e.to_string(),
            })?;
            Ok(collected.to_bytes())
        })
        .await
    }

    pub async fn text(self) -> Result<String, MahoError> {
        let bytes = self.bytes().await?;
        String::from_utf8(bytes.to_vec()).map_err(|e| MahoError::Network {
            message: format!("UTF-8 decode error: {e}"),
        })
    }

    pub async fn json<T: DeserializeOwned>(self) -> Result<T, MahoError> {
        let bytes = self.bytes().await?;
        serde_json::from_slice(&bytes).map_err(|e| MahoError::Json {
            message: e.to_string(),
        })
    }

    pub fn bytes_stream(self) -> MahoByteStream {
        MahoByteStream::new(self.body)
    }
}

#[derive(Clone)]
pub struct MahoHttpClient {
    inner: Client<MahoConnector, Full<Bytes>>,
    config: Arc<MahoConfig>,
    default_user_agent: String,
}

impl MahoHttpClient {
    pub fn new(config: Arc<MahoConfig>, proxy_url: Option<String>) -> Self {
        let connector = MahoConnector::new(config.clone(), proxy_url);
        let inner = Client::builder(TokioExecutor::new())
            .pool_idle_timeout(Duration::from_secs(90))
            .pool_max_idle_per_host(16)
            .build(connector);

        Self {
            inner,
            config,
            default_user_agent: "PixivAndroidApp/6.199.0 (Android 15.0; Pixel 8)".to_string(),
        }
    }

    pub fn config(&self) -> &Arc<MahoConfig> {
        &self.config
    }

    pub fn get(&self, url: &str) -> MahoRequestBuilder {
        self.request(Method::GET, url)
    }

    pub fn post(&self, url: &str) -> MahoRequestBuilder {
        self.request(Method::POST, url)
    }

    pub fn request(&self, method: Method, url: &str) -> MahoRequestBuilder {
        MahoRequestBuilder::new(self.clone(), method, url)
    }

    pub(crate) async fn execute(
        &self,
        builder: MahoRequestBuilder,
    ) -> Result<MahoResponse, MahoError> {
        let uri: Uri = builder
            .url
            .parse()
            .map_err(|e| MahoError::Url {
                message: format!("{}: {}", builder.url, e),
            })?;

        let mut req_builder = Request::builder().method(builder.method).uri(uri);

        let mut headers = builder.headers;
        if !headers.contains_key(USER_AGENT) {
            headers.insert(
                USER_AGENT,
                HeaderValue::from_str(&self.default_user_agent).unwrap(),
            );
        }

        if let Some(h) = req_builder.headers_mut() {
            *h = headers;
        }

        let body = Full::new(builder.body.unwrap_or_default());
        let request = req_builder.body(body).map_err(|e| MahoError::Network {
            message: e.to_string(),
        })?;

        async_compat::Compat::new(async move {
            let resp = self.inner.request(request).await?;
            let (parts, body) = resp.into_parts();
            Ok(MahoResponse::new(parts.status, parts.headers, body))
        })
        .await
    }
}

pub struct MahoRequestBuilder {
    client: MahoHttpClient,
    method: Method,
    url: String,
    headers: HeaderMap,
    body: Option<Bytes>,
}

impl MahoRequestBuilder {
    pub fn new(client: MahoHttpClient, method: Method, url: &str) -> Self {
        Self {
            client,
            method,
            url: url.to_string(),
            headers: HeaderMap::new(),
            body: None,
        }
    }

    pub fn header<K, V>(mut self, key: K, val: V) -> Self
    where
        HeaderName: TryFrom<K>,
        HeaderValue: TryFrom<V>,
    {
        if let (Ok(k), Ok(v)) = (HeaderName::try_from(key), HeaderValue::try_from(val)) {
            self.headers.insert(k, v);
        }
        self
    }

    pub fn headers(mut self, headers: HeaderMap) -> Self {
        for (k, v) in headers {
            if let Some(k) = k {
                self.headers.insert(k, v);
            }
        }
        self
    }

    pub fn body<B: Into<Bytes>>(mut self, body: B) -> Self {
        self.body = Some(body.into());
        self
    }

    pub fn form(mut self, params: &[(&str, &str)]) -> Self {
        let encoded = form_urlencoded::Serializer::new(String::new())
            .extend_pairs(params)
            .finish();
        self.headers.insert(
            CONTENT_TYPE,
            HeaderValue::from_static("application/x-www-form-urlencoded"),
        );
        self.body = Some(Bytes::from(encoded));
        self
    }

    pub fn json<T: Serialize>(mut self, json_val: &T) -> Result<Self, MahoError> {
        let bytes = serde_json::to_vec(json_val).map_err(|e| MahoError::Json {
            message: e.to_string(),
        })?;
        self.headers
            .insert(CONTENT_TYPE, HeaderValue::from_static("application/json"));
        self.body = Some(Bytes::from(bytes));
        Ok(self)
    }

    pub async fn send(self) -> Result<MahoResponse, MahoError> {
        let client = self.client.clone();
        client.execute(self).await
    }
}

#[derive(uniffi::Record, Clone, Debug, Default)]
pub struct MahoClientOptions {
    pub domain_fronting_enabled: bool,
    pub split_delay_ms: u64,
    pub host_ips: HashMap<String, Vec<String>>,
    pub proxy_url: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug)]
pub struct MahoResponseData {
    pub status: u16,
    pub headers: HashMap<String, String>,
    pub body: Vec<u8>,
}

#[derive(uniffi::Object)]
pub struct MahoClient {
    inner: Arc<RwLock<MahoHttpClient>>,
}

#[uniffi::export(async_runtime = "tokio")]
impl MahoClient {
    #[uniffi::constructor]
    pub fn new(options: Option<MahoClientOptions>) -> Arc<Self> {
        let client = Self::build_client(options.as_ref());
        Arc::new(Self {
            inner: Arc::new(RwLock::new(client)),
        })
    }

    pub fn update_options(&self, options: MahoClientOptions) {
        let new_client = Self::build_client(Some(&options));
        *self.inner.write() = new_client;
    }

    pub async fn get(
        &self,
        url: String,
        headers: Option<HashMap<String, String>>,
    ) -> Result<MahoResponseData, MahoError> {
        self.request("GET".to_string(), url, headers, None).await
    }

    pub async fn post(
        &self,
        url: String,
        headers: Option<HashMap<String, String>>,
        body: Option<Vec<u8>>,
    ) -> Result<MahoResponseData, MahoError> {
        self.request("POST".to_string(), url, headers, body).await
    }

    pub async fn request(
        &self,
        method: String,
        url: String,
        headers: Option<HashMap<String, String>>,
        body: Option<Vec<u8>>,
    ) -> Result<MahoResponseData, MahoError> {
        let client = self.inner.read().clone();
        let parsed_method = method.parse::<http::Method>().map_err(|e| MahoError::Network {
            message: format!("Invalid HTTP method '{method}': {e}"),
        })?;

        let mut builder = client.request(parsed_method, &url);
        if let Some(h) = headers {
            for (k, v) in h {
                builder = builder.header(k.as_str(), v.as_str());
            }
        }
        if let Some(b) = body {
            builder = builder.body(Bytes::from(b));
        }

        let resp = builder.send().await?;
        let status = resp.status().as_u16();
        let mut resp_headers = HashMap::new();
        for (k, v) in resp.headers() {
            if let Ok(val_str) = v.to_str() {
                resp_headers.insert(k.as_str().to_string(), val_str.to_string());
            }
        }
        let body_bytes = resp.bytes().await?.to_vec();

        Ok(MahoResponseData {
            status,
            headers: resp_headers,
            body: body_bytes,
        })
    }

    pub async fn get_bytes(&self, url: String) -> Result<Vec<u8>, MahoError> {
        let resp = self.get(url, None).await?;
        if resp.status >= 400 {
            return Err(MahoError::Http { code: resp.status });
        }
        Ok(resp.body)
    }

    pub async fn get_string(&self, url: String) -> Result<String, MahoError> {
        let bytes = self.get_bytes(url).await?;
        String::from_utf8(bytes).map_err(|e| MahoError::Network {
            message: format!("UTF-8 decode error: {e}"),
        })
    }

    pub async fn download_file(
        &self,
        url: String,
        destination: String,
    ) -> Result<(), MahoError> {
        let bytes = self.get_bytes(url).await?;
        if let Some(parent) = std::path::Path::new(&destination).parent() {
            tokio::fs::create_dir_all(parent).await.map_err(|e| MahoError::Io {
                message: e.to_string(),
            })?;
        }
        tokio::fs::write(&destination, bytes)
            .await
            .map_err(|e| MahoError::Io {
                message: e.to_string(),
            })?;
        Ok(())
    }
}

impl MahoClient {
    fn build_client(options: Option<&MahoClientOptions>) -> MahoHttpClient {
        let resolver = crate::dns::DnsResolver::new();
        let mut enabled = true;
        let mut split_delay_ms = 100;
        let mut proxy_url = None;

        if let Some(opts) = options {
            enabled = opts.domain_fronting_enabled;
            split_delay_ms = opts.split_delay_ms;
            proxy_url = opts
                .proxy_url
                .as_ref()
                .map(|s| s.trim().to_string())
                .filter(|s| !s.is_empty());
            for (host, ips) in &opts.host_ips {
                resolver.set_mapping(host.clone(), ips.clone());
            }
        }

        let config = Arc::new(MahoConfig {
            enabled,
            split_delay_ms,
            dns_resolver: resolver,
        });

        MahoHttpClient::new(config, proxy_url)
    }
}

