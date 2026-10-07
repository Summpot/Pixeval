// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::net::SocketAddr;
use std::sync::Arc;
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::{TcpListener, TcpStream};
use tokio::sync::watch;

use crate::config::MahoConfig;
use crate::stream::TlsFragmentedStream;

pub struct MahoProxyServer {
    port: u16,
    shutdown_tx: watch::Sender<bool>,
}

impl MahoProxyServer {
    pub fn start_sync(config: Arc<MahoConfig>) -> Result<Self, std::io::Error> {
        let std_listener = std::net::TcpListener::bind("127.0.0.1:0")?;
        std_listener.set_nonblocking(true)?;
        let port = std_listener.local_addr()?.port();
        let (shutdown_tx, mut shutdown_rx) = watch::channel(false);

        let cfg = config.clone();
        let server_future = async move {
            let Ok(listener) = TcpListener::from_std(std_listener) else {
                return;
            };
            loop {
                tokio::select! {
                    accept_res = listener.accept() => {
                        match accept_res {
                            Ok((client_socket, _)) => {
                                let cfg_clone = cfg.clone();
                                tokio::spawn(async move {
                                    let _ = handle_client(client_socket, cfg_clone).await;
                                });
                            }
                            Err(_) => break,
                        }
                    }
                    _ = shutdown_rx.changed() => {
                        break;
                    }
                }
            }
        };

        if let Ok(handle) = tokio::runtime::Handle::try_current() {
            handle.spawn(server_future);
        } else {
            std::thread::Builder::new()
                .name("maho-proxy".to_string())
                .spawn(move || {
                    if let Ok(rt) = tokio::runtime::Builder::new_current_thread()
                        .enable_all()
                        .build()
                    {
                        rt.block_on(server_future);
                    }
                })?;
        }

        Ok(Self { port, shutdown_tx })
    }

    pub async fn start(config: Arc<MahoConfig>) -> Result<Self, std::io::Error> {
        Self::start_sync(config)
    }

    pub fn port(&self) -> u16 {
        self.port
    }

    pub fn proxy_url(&self) -> String {
        format!("http://127.0.0.1:{}", self.port)
    }

    pub fn stop(&self) {
        let _ = self.shutdown_tx.send(true);
    }
}

impl Drop for MahoProxyServer {
    fn drop(&mut self) {
        self.stop();
    }
}

async fn handle_client(
    mut client: TcpStream,
    config: Arc<MahoConfig>,
) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let mut buf = [0u8; 4096];
    let mut header_len = 0;

    loop {
        let n = client.read(&mut buf[header_len..]).await?;
        if n == 0 {
            return Ok(());
        }
        header_len += n;
        if buf[..header_len].windows(4).any(|w| w == b"\r\n\r\n") {
            break;
        }
        if header_len >= buf.len() {
            return Err("HTTP header too long".into());
        }
    }

    let header_str = std::str::from_utf8(&buf[..header_len])?;
    let mut lines = header_str.lines();
    let request_line = lines.next().ok_or("Empty request line")?;
    let mut parts = request_line.split_whitespace();
    let method = parts.next().ok_or("Missing method")?;
    let target = parts.next().ok_or("Missing target")?;

    if method.eq_ignore_ascii_case("CONNECT") {
        let (host, port) = if let Some(idx) = target.rfind(':') {
            let host = &target[..idx];
            let port = target[idx + 1..].parse::<u16>().unwrap_or(443);
            (host, port)
        } else {
            (target, 443)
        };

        let addrs = if let Ok(ips) = config.dns_resolver.resolve_host(host).await {
            if !ips.is_empty() {
                ips.into_iter()
                    .map(|ip| SocketAddr::new(ip, port))
                    .collect::<Vec<_>>()
            } else {
                tokio::net::lookup_host(target).await?.collect::<Vec<_>>()
            }
        } else {
            tokio::net::lookup_host(target).await?.collect::<Vec<_>>()
        };

        if addrs.is_empty() {
            return Err("Failed to resolve target".into());
        }

        let mut target_tcp = None;
        for addr in addrs {
            match tokio::time::timeout(std::time::Duration::from_secs(3), TcpStream::connect(addr)).await {
                Ok(Ok(stream)) => {
                    target_tcp = Some(stream);
                    break;
                }
                _ => continue,
            }
        }
        let target_tcp = target_tcp.ok_or("Failed to connect to any resolved address")?;

        client
            .write_all(b"HTTP/1.1 200 Connection Established\r\n\r\n")
            .await?;
        client.flush().await?;

        if config.enabled {
            let mut fragmented = TlsFragmentedStream::new(target_tcp, config.split_delay_ms);
            let _ = tokio::io::copy_bidirectional(&mut client, &mut fragmented).await;
        } else {
            let mut target_tcp = target_tcp;
            let _ = tokio::io::copy_bidirectional(&mut client, &mut target_tcp).await;
        }
    }

    Ok(())
}
