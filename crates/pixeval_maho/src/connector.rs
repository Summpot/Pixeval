// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::future::Future;
use std::io;
use std::net::SocketAddr;
use std::pin::Pin;
use std::sync::Arc;
use std::task::{Context, Poll};
use std::time::Duration;

use http::Uri;
use hyper_util::client::legacy::connect::Connected;
use hyper_util::client::legacy::connect::Connection;
use hyper_util::rt::TokioIo;
use rustls_pki_types::ServerName;
use tokio::io::{AsyncRead, AsyncWrite, ReadBuf};
use tokio::net::TcpStream;
use tokio_rustls::TlsConnector;
use tokio_rustls::rustls;
use tower_service::Service;

use crate::config::MahoConfig;
use crate::stream::TlsFragmentedStream;

pub enum MahoRawStream {
    Plain(TcpStream),
    Fragmented(TlsFragmentedStream<TcpStream>),
}

impl AsyncRead for MahoRawStream {
    fn poll_read(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut ReadBuf<'_>,
    ) -> Poll<io::Result<()>> {
        match self.get_mut() {
            MahoRawStream::Plain(s) => Pin::new(s).poll_read(cx, buf),
            MahoRawStream::Fragmented(s) => Pin::new(s).poll_read(cx, buf),
        }
    }
}

impl AsyncWrite for MahoRawStream {
    fn poll_write(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &[u8],
    ) -> Poll<io::Result<usize>> {
        match self.get_mut() {
            MahoRawStream::Plain(s) => Pin::new(s).poll_write(cx, buf),
            MahoRawStream::Fragmented(s) => Pin::new(s).poll_write(cx, buf),
        }
    }

    fn poll_flush(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        match self.get_mut() {
            MahoRawStream::Plain(s) => Pin::new(s).poll_flush(cx),
            MahoRawStream::Fragmented(s) => Pin::new(s).poll_flush(cx),
        }
    }

    fn poll_shutdown(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        match self.get_mut() {
            MahoRawStream::Plain(s) => Pin::new(s).poll_shutdown(cx),
            MahoRawStream::Fragmented(s) => Pin::new(s).poll_shutdown(cx),
        }
    }
}

pub enum MahoStream {
    Tls(tokio_rustls::client::TlsStream<MahoRawStream>),
    Plain(TcpStream),
}

impl AsyncRead for MahoStream {
    fn poll_read(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut ReadBuf<'_>,
    ) -> Poll<io::Result<()>> {
        match self.get_mut() {
            MahoStream::Tls(s) => Pin::new(s).poll_read(cx, buf),
            MahoStream::Plain(s) => Pin::new(s).poll_read(cx, buf),
        }
    }
}

impl AsyncWrite for MahoStream {
    fn poll_write(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &[u8],
    ) -> Poll<io::Result<usize>> {
        match self.get_mut() {
            MahoStream::Tls(s) => Pin::new(s).poll_write(cx, buf),
            MahoStream::Plain(s) => Pin::new(s).poll_write(cx, buf),
        }
    }

    fn poll_flush(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        match self.get_mut() {
            MahoStream::Tls(s) => Pin::new(s).poll_flush(cx),
            MahoStream::Plain(s) => Pin::new(s).poll_flush(cx),
        }
    }

    fn poll_shutdown(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        match self.get_mut() {
            MahoStream::Tls(s) => Pin::new(s).poll_shutdown(cx),
            MahoStream::Plain(s) => Pin::new(s).poll_shutdown(cx),
        }
    }
}

impl Connection for MahoStream {
    fn connected(&self) -> Connected {
        Connected::new()
    }
}

#[derive(Clone)]
pub struct MahoConnector {
    config: Arc<MahoConfig>,
    tls_connector: TlsConnector,
    proxy_url: Option<String>,
}

impl MahoConnector {
    pub fn new(config: Arc<MahoConfig>, proxy_url: Option<String>) -> Self {
        let mut root_store = rustls::RootCertStore::empty();
        root_store.extend(webpki_roots::TLS_SERVER_ROOTS.iter().cloned());

        let mut client_config = rustls::ClientConfig::builder()
            .with_root_certificates(root_store)
            .with_no_client_auth();

        client_config.alpn_protocols = vec![b"http/1.1".to_vec()];

        Self {
            config,
            tls_connector: TlsConnector::from(Arc::new(client_config)),
            proxy_url,
        }
    }

    async fn connect_internal(&self, uri: Uri) -> io::Result<TokioIo<MahoStream>> {
        let scheme = uri.scheme_str().unwrap_or("https");
        let host = uri
            .host()
            .ok_or_else(|| io::Error::new(io::ErrorKind::InvalidInput, "Missing host in URI"))?;
        let port = uri
            .port_u16()
            .unwrap_or(if scheme == "http" { 80 } else { 443 });

        // Check if explicit proxy is configured and domain fronting is not applicable
        if let Some(ref proxy) = self.proxy_url {
            let proxy_str = proxy.trim();
            if !proxy_str.is_empty() {
                // If an explicit proxy is configured (e.g. http://127.0.0.1:7890), connect via HTTP CONNECT
                return self
                    .connect_via_http_proxy(proxy_str, host, port, scheme == "https")
                    .await;
            }
        }

        // Resolve addresses via DnsResolver (static IP mapping first, fallback to lookup_host)
        let addrs = if let Ok(ips) = self.config.dns_resolver.resolve_host(host).await {
            if !ips.is_empty() {
                ips.into_iter()
                    .map(|ip| SocketAddr::new(ip, port))
                    .collect::<Vec<_>>()
            } else {
                tokio::net::lookup_host((host, port))
                    .await?
                    .collect::<Vec<_>>()
            }
        } else {
            tokio::net::lookup_host((host, port))
                .await?
                .collect::<Vec<_>>()
        };

        if addrs.is_empty() {
            return Err(io::Error::new(
                io::ErrorKind::NotFound,
                format!("Failed to resolve host: {host}"),
            ));
        }

        let mut target_tcp = None;
        for addr in addrs {
            match tokio::time::timeout(Duration::from_secs(5), TcpStream::connect(addr)).await {
                Ok(Ok(stream)) => {
                    let _ = stream.set_nodelay(true);
                    target_tcp = Some(stream);
                    break;
                }
                _ => continue,
            }
        }

        let tcp_stream = target_tcp.ok_or_else(|| {
            io::Error::new(
                io::ErrorKind::NotConnected,
                format!("Failed to connect to any resolved IP for {host}"),
            )
        })?;

        if scheme == "http" {
            return Ok(TokioIo::new(MahoStream::Plain(tcp_stream)));
        }

        let raw_stream = if self.config.enabled {
            MahoRawStream::Fragmented(TlsFragmentedStream::new(
                tcp_stream,
                self.config.split_delay_ms,
            ))
        } else {
            MahoRawStream::Plain(tcp_stream)
        };

        let server_name = ServerName::try_from(host.to_string())
            .map_err(|e| io::Error::new(io::ErrorKind::InvalidInput, e.to_string()))?;

        let tls_stream = self
            .tls_connector
            .connect(server_name, raw_stream)
            .await
            .map_err(|e| io::Error::new(io::ErrorKind::ConnectionReset, e))?;

        Ok(TokioIo::new(MahoStream::Tls(tls_stream)))
    }

    async fn connect_via_http_proxy(
        &self,
        proxy_str: &str,
        host: &str,
        port: u16,
        is_tls: bool,
    ) -> io::Result<TokioIo<MahoStream>> {
        let proxy_uri: Uri = proxy_str
            .parse()
            .map_err(|e| io::Error::new(io::ErrorKind::InvalidInput, e))?;
        let proxy_host = proxy_uri
            .host()
            .ok_or_else(|| io::Error::new(io::ErrorKind::InvalidInput, "Missing proxy host"))?;
        let proxy_port = proxy_uri.port_u16().unwrap_or(80);

        let mut client = TcpStream::connect((proxy_host, proxy_port)).await?;
        let _ = client.set_nodelay(true);

        if is_tls {
            use tokio::io::{AsyncReadExt, AsyncWriteExt};

            let connect_req = format!(
                "CONNECT {host}:{port} HTTP/1.1\r\nHost: {host}:{port}\r\nProxy-Connection: Keep-Alive\r\n\r\n"
            );
            client.write_all(connect_req.as_bytes()).await?;

            let mut buf = [0u8; 1024];
            let mut read_len = 0;
            loop {
                let n = client.read(&mut buf[read_len..]).await?;
                if n == 0 {
                    return Err(io::Error::new(
                        io::ErrorKind::UnexpectedEof,
                        "Proxy connection closed unexpectedly",
                    ));
                }
                read_len += n;
                if buf[..read_len].windows(4).any(|w| w == b"\r\n\r\n") {
                    break;
                }
            }

            let resp_str = std::str::from_utf8(&buf[..read_len])
                .map_err(|e| io::Error::new(io::ErrorKind::InvalidData, e))?;
            if !resp_str.starts_with("HTTP/1.1 200") && !resp_str.starts_with("HTTP/1.0 200") {
                return Err(io::Error::new(
                    io::ErrorKind::ConnectionRefused,
                    format!("Proxy CONNECT returned error: {resp_str}"),
                ));
            }

            let raw_stream = MahoRawStream::Plain(client);
            let server_name = ServerName::try_from(host.to_string())
                .map_err(|e| io::Error::new(io::ErrorKind::InvalidInput, e.to_string()))?;

            let tls_stream = self
                .tls_connector
                .connect(server_name, raw_stream)
                .await
                .map_err(|e| io::Error::new(io::ErrorKind::ConnectionReset, e))?;

            Ok(TokioIo::new(MahoStream::Tls(tls_stream)))
        } else {
            Ok(TokioIo::new(MahoStream::Plain(client)))
        }
    }
}

impl Service<Uri> for MahoConnector {
    type Response = TokioIo<MahoStream>;
    type Error = io::Error;
    type Future = Pin<Box<dyn Future<Output = Result<Self::Response, Self::Error>> + Send>>;

    fn poll_ready(&mut self, _cx: &mut Context<'_>) -> Poll<Result<(), Self::Error>> {
        Poll::Ready(Ok(()))
    }

    fn call(&mut self, req: Uri) -> Self::Future {
        let this = self.clone();
        Box::pin(async move { this.connect_internal(req).await })
    }
}
