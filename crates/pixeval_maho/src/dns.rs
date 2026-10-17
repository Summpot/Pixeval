// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::net::IpAddr;
use std::sync::RwLock;

use std::sync::Arc;

#[boltffi::error]
#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum DnsError {
    #[error("DNS resolution error: {message}")]
    Resolution { message: String },
}

#[derive(Debug, Default, uniffi::Object)]
pub struct DnsResolver {
    mappings: RwLock<HashMap<String, Vec<IpAddr>>>,
}

#[uniffi::export]
impl DnsResolver {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Arc::new(Self {
            mappings: RwLock::new(HashMap::new()),
        })
    }

    pub fn set_mapping(&self, host: String, ips: Vec<String>) {
        let parsed_ips: Vec<IpAddr> = ips.iter().filter_map(|s| s.parse().ok()).collect();
        let mut map = self.mappings.write().unwrap();
        map.insert(host, parsed_ips);
    }

    pub fn get_mapping(&self, host: String) -> Vec<String> {
        let map = self.mappings.read().unwrap();
        map.get(&host)
            .map(|ips| ips.iter().map(|ip| ip.to_string()).collect())
            .unwrap_or_default()
    }

    pub fn clear(&self) {
        self.clear_mappings();
    }

    pub async fn resolve(&self, host: String) -> Result<Vec<String>, DnsError> {
        self.resolve_host(&host)
            .await
            .map(|ips| ips.into_iter().map(|ip| ip.to_string()).collect())
            .map_err(|e| DnsError::Resolution {
                message: e.to_string(),
            })
    }
}

impl DnsResolver {
    pub fn set_static_ips(&self, host: &str, ips: Vec<IpAddr>) {
        let mut map = self.mappings.write().unwrap();
        map.insert(host.to_string(), ips);
    }

    pub fn remove_mapping(&self, host: &str) {
        let mut map = self.mappings.write().unwrap();
        map.remove(host);
    }

    pub fn clear_mappings(&self) {
        let mut map = self.mappings.write().unwrap();
        map.clear();
    }

    pub fn get_static_ips(&self, host: &str) -> Option<Vec<IpAddr>> {
        let map = self.mappings.read().unwrap();
        map.get(host).cloned().or_else(|| map.iter()
            .filter_map(|(pattern, ips)| pattern.strip_prefix("*.").filter(|suffix|
                host.len() > suffix.len() + 1 && host.ends_with(&format!(".{suffix}"))).map(|suffix| (suffix.len(), ips)))
            .max_by_key(|(len, _)| *len).map(|(_, ips)| ips.clone()))
    }

    pub async fn resolve_host(&self, host: &str) -> std::io::Result<Vec<IpAddr>> {
        if let Some(ips) = self.get_static_ips(host).filter(|ips| !ips.is_empty()) {
            return Ok(ips);
        }

        // System lookup fallback
        let addrs: Vec<IpAddr> = tokio::net::lookup_host((host, 0))
            .await?
            .map(|socket_addr| socket_addr.ip())
            .collect();

        if addrs.is_empty() {
            Err(std::io::Error::new(
                std::io::ErrorKind::NotFound,
                format!("Failed to resolve host '{host}'"),
            ))
        } else {
            Ok(addrs)
        }
    }
}

#[cfg(test)]
mod mapping_tests {
    use super::*;
    #[test]
    fn wildcard_mappings_respect_label_boundaries_and_exact_overrides() {
        let resolver = DnsResolver::new();
        resolver.set_static_ips("*.githubusercontent.com", vec!["127.0.0.1".parse().unwrap()]);
        resolver.set_static_ips("avatars.githubusercontent.com", vec!["127.0.0.2".parse().unwrap()]);
        assert_eq!(resolver.get_static_ips("raw.githubusercontent.com").unwrap()[0].to_string(), "127.0.0.1");
        assert_eq!(resolver.get_static_ips("avatars.githubusercontent.com").unwrap()[0].to_string(), "127.0.0.2");
        assert!(resolver.get_static_ips("githubusercontent.com").is_none());
        assert!(resolver.get_static_ips("evilgithubusercontent.com").is_none());
    }
}
