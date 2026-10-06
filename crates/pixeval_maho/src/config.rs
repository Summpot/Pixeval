// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::dns::DnsResolver;
use std::sync::Arc;

pub const APP_API_HOST: &str = "app-api.pixiv.net";
pub const WEB_API_HOST: &str = "www.pixiv.net";
pub const OAUTH_HOST: &str = "oauth.secure.pixiv.net";
pub const IMAGE_HOST: &str = "i.pximg.net";
pub const IMAGE_HOST2: &str = "s.pximg.net";
pub const ACCOUNT_HOST: &str = "accounts.pixiv.net";

#[derive(Debug, Clone)]
pub struct MahoConfig {
    pub enabled: bool,
    pub split_delay_ms: u64,
    pub dns_resolver: Arc<DnsResolver>,
}

impl Default for MahoConfig {
    fn default() -> Self {
        let resolver = DnsResolver::new();
        // Populate standard default Pixiv domain fronting IPs matching C# AppSettings
        resolver.set_static_ips(
            APP_API_HOST,
            vec![
                "104.18.42.239".parse().unwrap(),
                "172.64.145.17".parse().unwrap(),
            ],
        );
        resolver.set_static_ips(
            OAUTH_HOST,
            vec![
                "104.18.42.239".parse().unwrap(),
                "172.64.145.17".parse().unwrap(),
            ],
        );
        resolver.set_static_ips(
            IMAGE_HOST,
            vec![
                "210.140.139.134".parse().unwrap(),
                "210.140.139.135".parse().unwrap(),
                "210.140.139.136".parse().unwrap(),
                "210.140.139.137".parse().unwrap(),
            ],
        );
        resolver.set_static_ips(
            IMAGE_HOST2,
            vec![
                "210.140.139.135".parse().unwrap(),
                "210.140.139.136".parse().unwrap(),
                "210.140.139.137".parse().unwrap(),
            ],
        );
        resolver.set_static_ips(
            WEB_API_HOST,
            vec![
                "210.140.139.155".parse().unwrap(),
                "210.140.139.156".parse().unwrap(),
                "210.140.139.157".parse().unwrap(),
            ],
        );
        resolver.set_static_ips(
            ACCOUNT_HOST,
            vec![
                "210.140.139.155".parse().unwrap(),
                "210.140.139.156".parse().unwrap(),
                "210.140.139.157".parse().unwrap(),
            ],
        );

        Self {
            enabled: true,
            split_delay_ms: 100,
            dns_resolver: resolver,
        }
    }
}
