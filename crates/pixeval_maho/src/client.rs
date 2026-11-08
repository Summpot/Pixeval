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
use http_body_util::{BodyExt, Full, combinators::UnsyncBoxBody};
use hyper::body::Body;
use hyper_util::client::legacy::Client;
use hyper_util::rt::TokioExecutor;
use parking_lot::RwLock;
use serde::Serialize;
use serde::de::DeserializeOwned;
use url::form_urlencoded;

use crate::config::MahoConfig;
use crate::connector::MahoConnector;

#[boltffi::error]
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
    inner: UnsyncBoxBody<Bytes, MahoError>,
}

impl MahoByteStream {
    pub fn new(inner: UnsyncBoxBody<Bytes, MahoError>) -> Self {
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
    body: UnsyncBoxBody<Bytes, MahoError>,
}

impl MahoResponse {
    pub fn new(status: StatusCode, headers: HeaderMap, body: hyper::body::Incoming) -> Self {
        Self {
            status,
            headers,
            body: body.map_err(MahoError::from).boxed_unsync(),
        }
    }

    pub fn from_data(data: MahoResponseData) -> Result<Self, MahoError> {
        let status = StatusCode::from_u16(data.status).map_err(|e| MahoError::Network { message: e.to_string() })?;
        let mut headers = HeaderMap::new();
        for (key, value) in data.headers {
            let key = HeaderName::try_from(key).map_err(|e| MahoError::Network { message: e.to_string() })?;
            let value = HeaderValue::try_from(value).map_err(|e| MahoError::Network { message: e.to_string() })?;
            headers.insert(key, value);
        }
        Ok(Self { status, headers, body: Full::new(Bytes::from(data.body))
            .map_err(|never| match never {}).boxed_unsync() })
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
    transport: Arc<RwLock<Option<Arc<dyn MahoTransport>>>>,
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
            transport: Arc::new(RwLock::new(None)),
            default_user_agent: "PixivAndroidApp/6.199.0 (Android 15.0; Pixel 8)".to_string(),
        }
    }

    pub fn set_transport(&self, transport: Option<Arc<dyn MahoTransport>>) {
        *self.transport.write() = transport;
    }

    pub fn transport(&self) -> Option<Arc<dyn MahoTransport>> {
        self.transport.read().clone()
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
        if let Some(transport) = self.transport() {
            let headers = builder.headers.iter().filter_map(|(key, value)|
                value.to_str().ok().map(|value| (key.to_string(), value.to_owned()))).collect();
            static NEXT_REQUEST: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(1);
            let id = NEXT_REQUEST.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
            let mut lifetime = TransportRequest { transport: transport.clone(), id, completed: false };
            let response = transport.send(id, builder.method.to_string(), builder.url, headers,
                builder.body.unwrap_or_default().to_vec()).await;
            lifetime.completed = true;
            let response = response?;
            return MahoResponse::from_data(response);
        }
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
            let resp = tokio::time::timeout(Duration::from_secs(30), self.inner.request(request))
                .await
                .map_err(|_| MahoError::Network {
                    message: format!("HTTP request timed out: {}", builder.url),
                })??;
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

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug, Default)]
pub struct MahoClientOptions {
    pub domain_fronting_enabled: bool,
    pub split_delay_ms: u64,
    pub host_ips: HashMap<String, Vec<String>>,
    pub proxy_url: Option<String>,
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug)]
pub struct MahoResponseData {
    pub status: u16,
    pub headers: HashMap<String, String>,
    pub body: Vec<u8>,
}

/// Optional per-client transport supplied at the application composition boundary.
/// Request construction, authentication and response parsing remain in Rust.
#[uniffi::export(callback_interface)]
#[async_trait::async_trait]
pub trait MahoTransport: Send + Sync {
    async fn send(&self, request_id: u64, method: String, url: String, headers: HashMap<String, String>, body: Vec<u8>)
        -> Result<MahoResponseData, MahoError>;
    fn cancel(&self, request_id: u64);
}

struct TransportRequest {
    transport: Arc<dyn MahoTransport>,
    id: u64,
    completed: bool,
}

impl Drop for TransportRequest {
    fn drop(&mut self) {
        if !self.completed {
            self.transport.cancel(self.id);
        }
    }
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

    pub fn set_transport(&self, transport: Option<Box<dyn MahoTransport>>) {
        self.inner.read().set_transport(transport.map(Arc::from));
    }

    pub fn update_options(&self, options: MahoClientOptions) {
        let new_client = Self::build_client(Some(&options));
        let mut current = self.inner.write();
        new_client.set_transport(current.transport());
        *current = new_client;
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


#[cfg(test)]
mod transport_tests {
    use super::*;
    use futures_util::StreamExt;
    use tokio::sync::mpsc;

    struct Transport {
        started: mpsc::UnboundedSender<u64>,
        cancelled: mpsc::UnboundedSender<u64>,
        pending: bool,
    }

    #[async_trait::async_trait]
    impl MahoTransport for Transport {
        async fn send(&self, id: u64, method: String, url: String,
            headers: HashMap<String, String>, body: Vec<u8>) -> Result<MahoResponseData, MahoError> {
            assert_eq!(method, "POST");
            assert_eq!(url, "https://fixture.invalid/resource");
            assert_eq!(headers.get("x-fixture").map(String::as_str), Some("value"));
            assert_eq!(body, b"payload");
            self.started.send(id).unwrap();
            if self.pending { std::future::pending::<()>().await; }
            Ok(MahoResponseData { status: 201, headers: HashMap::from([("x-result".into(), "ok".into())]), body: b"response".to_vec() })
        }
        fn cancel(&self, id: u64) { let _ = self.cancelled.send(id); }
    }

    #[tokio::test]
    async fn transport_preserves_request_response_stream_and_configuration_updates() {
        let (started, mut requests) = mpsc::unbounded_channel();
        let (cancelled, mut cancellations) = mpsc::unbounded_channel();
        let client = MahoClient::new(None);
        client.set_transport(Some(Box::new(Transport { started, cancelled, pending: false })));
        client.update_options(MahoClientOptions::default());
        let http = client.inner.read().clone();
        for _ in 0..2 {
            let response = http.post("https://fixture.invalid/resource")
                .header("x-fixture", "value").body(Bytes::from_static(b"payload")).send().await.unwrap();
            assert_eq!(response.status(), StatusCode::CREATED);
            assert_eq!(response.headers()["x-result"], "ok");
            let chunks: Vec<_> = response.bytes_stream().collect().await;
            assert_eq!(chunks.len(), 1);
            assert_eq!(chunks[0].as_ref().unwrap().as_ref(), b"response");
        }
        assert_ne!(requests.recv().await.unwrap(), requests.recv().await.unwrap());
        assert!(cancellations.try_recv().is_err(), "Completed requests must not be cancelled");
    }

    #[tokio::test]
    async fn dropping_an_inflight_request_cancels_only_its_transport_operation() {
        let (started, mut requests) = mpsc::unbounded_channel();
        let (cancelled, mut cancellations) = mpsc::unbounded_channel();
        let http = MahoClient::build_client(None);
        http.set_transport(Some(Arc::new(Transport { started, cancelled, pending: true })));
        let request = tokio::spawn(async move {
            http.post("https://fixture.invalid/resource")
                .header("x-fixture", "value").body(Bytes::from_static(b"payload")).send().await
        });
        let id = requests.recv().await.unwrap();
        request.abort();
        assert!(matches!(request.await, Err(error) if error.is_cancelled()));
        assert_eq!(cancellations.recv().await.unwrap(), id);
        assert!(cancellations.try_recv().is_err());
    }
}
