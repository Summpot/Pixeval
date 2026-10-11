uniffi::setup_scaffolding!();

pub mod client;
pub mod config;
pub mod connector;
pub mod dns;
pub mod fragmentation;
pub mod locator;
pub mod state_machine;
pub mod stream;

pub use client::*;
pub use config::*;
pub use connector::*;
pub use dns::*;
pub use fragmentation::*;
pub use locator::*;
pub use state_machine::*;
pub use stream::*;

#[uniffi::export]
pub fn split_client_hello(packet: Vec<u8>) -> Vec<Vec<u8>> {
    fragmentation::split_client_hello(&packet)
        .into_iter()
        .map(|f| f.data)
        .collect()
}

#[uniffi::export]
pub fn locate_server_name(packet: Vec<u8>) -> Option<String> {
    let mut locator = locator::ServerNameLocator::new(&packet);
    let (res, hostnames) = locator.locate_server_names();
    if res == locator::ServerNameLocatingResult::Located && !hostnames.is_empty() {
        let loc = hostnames[0];
        String::from_utf8(packet[loc.start..loc.start + loc.length].to_vec()).ok()
    } else {
        None
    }
}

#[uniffi::export]
pub fn is_pixiv_host(host: String) -> bool {
    let h = host.to_ascii_lowercase();
    h == "pixiv.net" || h.ends_with(".pixiv.net") || h == "pximg.net" || h.ends_with(".pximg.net")
}

#[derive(uniffi::Record, Clone, Debug, PartialEq, Eq)]
pub struct MahoProcessResult {
    pub is_handshake: bool,
    pub fragments: Vec<Vec<u8>>,
    pub remaining_bytes: Vec<u8>,
    pub is_completed: bool,
}

#[derive(uniffi::Object, Default)]
pub struct MahoClientHelloProcessor {
    inner: std::sync::Mutex<ClientHelloStateMachine>,
}

#[uniffi::export]
impl MahoClientHelloProcessor {
    #[uniffi::constructor]
    pub fn new() -> std::sync::Arc<Self> {
        std::sync::Arc::new(Self {
            inner: std::sync::Mutex::new(ClientHelloStateMachine::new()),
        })
    }

    pub fn is_completed(&self) -> bool {
        self.inner.lock().unwrap().completed
    }

    pub fn process_chunk(&self, chunk: Vec<u8>) -> MahoProcessResult {
        let mut sm = self.inner.lock().unwrap();
        let res = sm.flow_state(&chunk);
        let fragments = if let Some(packet) = res.packet {
            split_client_hello(packet)
        } else {
            Vec::new()
        };
        MahoProcessResult {
            is_handshake: res.state != ClientHelloCollectingState::Idle,
            fragments,
            remaining_bytes: res.remaining_bytes,
            is_completed: sm.completed,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::net::IpAddr;
    use tokio::io::{AsyncReadExt, AsyncWriteExt};

    /// Constructs a valid minimal TLS 1.2/1.3 ClientHello record containing SNI extension.
    fn make_test_client_hello(hostname: &str) -> Vec<u8> {
        let host_bytes = hostname.as_bytes();
        let host_len = host_bytes.len();

        // 1. SNI extension data
        // List length: 2 bytes (3 + host_len)
        // Entry: name_type(1 byte = 0), name_len(2 bytes), host_bytes
        let sni_list_len = 3 + host_len;
        let mut sni_data = Vec::new();
        sni_data.extend_from_slice(&[(sni_list_len >> 8) as u8, sni_list_len as u8]);
        sni_data.push(0x00); // HostName type
        sni_data.extend_from_slice(&[(host_len >> 8) as u8, host_len as u8]);
        sni_data.extend_from_slice(host_bytes);

        // Extension header: id(0x0000), length
        let mut ext = Vec::new();
        ext.extend_from_slice(&[0x00, 0x00]); // SNI extension id = 0
        ext.extend_from_slice(&[(sni_data.len() >> 8) as u8, sni_data.len() as u8]);
        ext.extend_from_slice(&sni_data);

        // 2. Handshake ClientHello body
        let mut handshake = Vec::new();
        handshake.push(0x01); // HandshakeType: ClientHello
        // Handshake length (3 bytes placeholder)
        handshake.extend_from_slice(&[0x00, 0x00, 0x00]);

        // Client version (TLS 1.2: 0x03, 0x03)
        handshake.extend_from_slice(&[0x03, 0x03]);
        // Random (32 bytes)
        handshake.extend_from_slice(&[0x42; 32]);
        // Session ID (0 bytes)
        handshake.push(0x00);
        // Cipher suites (2 bytes len + 2 bytes cipher)
        handshake.extend_from_slice(&[0x00, 0x02, 0x13, 0x01]);
        // Compression methods (1 byte len + 0x00)
        handshake.extend_from_slice(&[0x01, 0x00]);
        // Extensions length (2 bytes) + extensions
        handshake.extend_from_slice(&[(ext.len() >> 8) as u8, ext.len() as u8]);
        handshake.extend_from_slice(&ext);

        // Fill handshake length (3 bytes, big endian)
        let hs_body_len = handshake.len() - 4;
        handshake[1] = ((hs_body_len >> 16) & 0xFF) as u8;
        handshake[2] = ((hs_body_len >> 8) & 0xFF) as u8;
        handshake[3] = (hs_body_len & 0xFF) as u8;

        // 3. TLS Record header:
        // Type: 0x16 (Handshake)
        // Version: 0x03 0x01 (TLS 1.0)
        // Length: handshake.len()
        let mut record = Vec::new();
        record.push(0x16);
        record.extend_from_slice(&[0x03, 0x01]);
        record.extend_from_slice(&[(handshake.len() >> 8) as u8, handshake.len() as u8]);
        record.extend_from_slice(&handshake);

        record
    }

    #[test]
    fn test_sni_locator_success() {
        let hostname = "app-api.pixiv.net";
        let packet = make_test_client_hello(hostname);

        let mut locator = ServerNameLocator::new(&packet);
        let (result, locations) = locator.locate_server_names();

        assert_eq!(result, ServerNameLocatingResult::Located);
        assert_eq!(locations.len(), 1);
        let loc = locations[0];
        assert_eq!(loc.length, hostname.len());
        assert_eq!(
            &packet[loc.start..loc.start + loc.length],
            hostname.as_bytes()
        );
    }

    #[test]
    fn test_split_client_hello_fragments() {
        let hostname = "app-api.pixiv.net";
        let packet = make_test_client_hello(hostname);

        let frags = fragmentation::split_client_hello(&packet);
        // Expecting 3 fragments: before SNI, first half of SNI, second half of SNI to end
        assert_eq!(frags.len(), 3);

        for frag in &frags {
            assert!(frag.data.len() >= 5);
            assert_eq!(frag.data[0], 0x16); // TLS Handshake
            assert_eq!(frag.data[1], 0x03);
            assert_eq!(frag.data[2], 0x09); // Firewall evasion minor version
            let payload_len = ((frag.data[3] as usize) << 8) | (frag.data[4] as usize);
            assert_eq!(frag.data.len() - 5, payload_len);
        }
    }

    #[tokio::test]
    async fn test_state_machine_and_stream() {
        let hostname = "oauth.secure.pixiv.net";
        let packet = make_test_client_hello(hostname);

        let (client, mut server) = tokio::io::duplex(4096);
        let mut fragmented_stream = TlsFragmentedStream::new(client, 10);

        // Write in small 10-byte chunks to test stream packet reconstruction
        let packet_clone = packet.clone();
        tokio::spawn(async move {
            for chunk in packet_clone.chunks(10) {
                fragmented_stream.write_all(chunk).await.unwrap();
            }
            fragmented_stream.flush().await.unwrap();

            // After handshake, write some normal application data
            fragmented_stream.write_all(b"PING").await.unwrap();
            fragmented_stream.flush().await.unwrap();
        });

        let mut received = Vec::new();
        let mut buf = [0u8; 1024];
        loop {
            let n = server.read(&mut buf).await.unwrap();
            if n == 0 {
                break;
            }
            received.extend_from_slice(&buf[..n]);
            if received.ends_with(b"PING") {
                break;
            }
        }

        // Verify that received contains the fragmented ClientHello records ending with PING
        assert!(received.ends_with(b"PING"));
        assert_eq!(received[0], 0x16);
        assert_eq!(received[1], 0x03);
        assert_eq!(received[2], 0x09);
    }

    #[tokio::test]
    async fn test_dns_resolver() {
        let resolver = DnsResolver::new();
        let ip: IpAddr = "1.2.3.4".parse().unwrap();
        resolver.set_static_ips("test.pixiv.net", vec![ip]);

        let resolved = resolver.resolve_host("test.pixiv.net").await.unwrap();
        assert_eq!(resolved, vec![ip]);

        let resolved_strings = resolver
            .resolve("test.pixiv.net".to_string())
            .await
            .unwrap();
        assert_eq!(resolved_strings, vec!["1.2.3.4"]);
    }

    #[test]
    fn test_client_hello_processor() {
        let processor = MahoClientHelloProcessor::new();
        assert!(!processor.is_completed());

        let hostname = "oauth.secure.pixiv.net";
        let packet = make_test_client_hello(hostname);

        // Split packet into two chunks to simulate streaming write
        let (first_half, second_half) = packet.split_at(20);
        let res1 = processor.process_chunk(first_half.to_vec());
        assert!(res1.is_handshake);
        assert!(res1.fragments.is_empty());
        assert!(!res1.is_completed);

        let mut chunk2 = second_half.to_vec();
        chunk2.extend_from_slice(b"EXTRA_DATA");
        let res2 = processor.process_chunk(chunk2);
        assert!(res2.is_handshake);
        assert_eq!(res2.fragments.len(), 3);
        assert_eq!(res2.remaining_bytes, b"EXTRA_DATA");
        assert!(res2.is_completed);
        assert!(processor.is_completed());
    }

    #[test]
    fn test_compat_tcp_connect_and_timeout() {
        // Run outside of any ambient Tokio runtime
        std::thread::spawn(|| {
            assert!(tokio::runtime::Handle::try_current().is_err());
            let fut = async_compat::Compat::new(async {
                let _ = tokio::time::timeout(
                    std::time::Duration::from_millis(50),
                    tokio::net::TcpStream::connect("127.0.0.1:54321"),
                )
                .await;
            });
            futures::executor::block_on(fut);
        })
        .join()
        .expect("Thread panicked inside test_compat_tcp_connect_and_timeout");
    }

    #[test]
    fn test_is_pixiv_host() {
        assert!(is_pixiv_host("pixiv.net".to_string()));
        assert!(is_pixiv_host("app-api.pixiv.net".to_string()));
        assert!(is_pixiv_host("i.pximg.net".to_string()));
        assert!(is_pixiv_host("s.pximg.net".to_string()));
        assert!(is_pixiv_host("pximg.net".to_string()));

        assert!(!is_pixiv_host("google.com".to_string()));
        assert!(!is_pixiv_host("github.com".to_string()));
        assert!(!is_pixiv_host("127.0.0.1".to_string()));
    }

    #[tokio::test]
    async fn test_maho_client_construction_and_options() {
        use std::collections::HashMap;
        let mut host_ips = HashMap::new();
        host_ips.insert("custom.pixiv.net".to_string(), vec!["1.2.3.4".to_string()]);

        let options = MahoClientOptions {
            domain_fronting_enabled: true,
            split_delay_ms: 50,
            host_ips,
            proxy_url: Some("http://127.0.0.1:7890".to_string()),
        };

        let client = MahoClient::new(Some(options));
        let mut updated_ips = HashMap::new();
        updated_ips.insert("custom2.pixiv.net".to_string(), vec!["5.6.7.8".to_string()]);
        client.update_options(MahoClientOptions {
            domain_fronting_enabled: false,
            split_delay_ms: 0,
            host_ips: updated_ips,
            proxy_url: None,
        });
    }

    #[tokio::test]
    async fn test_connector_domain_fronting_proxy_bypass_logic() {
        use std::net::IpAddr;
        let resolver = DnsResolver::new();
        let ip: IpAddr = "127.0.0.1".parse().unwrap();
        resolver.set_static_ips("bypassed.pixiv.net", vec![ip]);

        let config = std::sync::Arc::new(MahoConfig {
            enabled: true,
            split_delay_ms: 0,
            dns_resolver: resolver,
        });

        let is_fronted = config.enabled
            && config
                .dns_resolver
                .get_static_ips("bypassed.pixiv.net")
                .is_some_and(|ips| !ips.is_empty());
        assert!(is_fronted);

        let not_fronted = config.enabled
            && config
                .dns_resolver
                .get_static_ips("other.domain.com")
                .is_some_and(|ips| !ips.is_empty());
        assert!(!not_fronted);
    }
}
