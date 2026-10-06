// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::locator::{ServerNameLocatingResult, ServerNameLocator};

pub const TLS_RECORD_HEADER_LENGTH: usize = 5;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FragmentedTlsRecord {
    pub data: Vec<u8>,
}

pub fn split_client_hello(packet: &[u8]) -> Vec<FragmentedTlsRecord> {
    if packet.len() <= TLS_RECORD_HEADER_LENGTH {
        return vec![FragmentedTlsRecord {
            data: packet.to_vec(),
        }];
    }

    let mut locator = ServerNameLocator::new(packet);
    let (res, hostnames) = locator.locate_server_names();

    if res != ServerNameLocatingResult::Located || hostnames.is_empty() {
        // 8.1: If SNI not located or no hostnames, pass through original ClientHello without splitting
        return vec![FragmentedTlsRecord {
            data: packet.to_vec(),
        }];
    }

    let mut cuts = Vec::new();
    for loc in &hostnames {
        cuts.push(loc.start);
        cuts.push(loc.start + loc.length / 2);
    }
    cuts.sort_unstable();
    cuts.dedup();

    // 8.5: Guard against underflow: all cuts must be > TLS_RECORD_HEADER_LENGTH and < packet.len()
    cuts.retain(|&c| c > TLS_RECORD_HEADER_LENGTH && c < packet.len());
    if cuts.is_empty() {
        return vec![FragmentedTlsRecord {
            data: packet.to_vec(),
        }];
    }

    let mut fragments = Vec::new();
    let mut start = 0;

    for (index, &cut) in cuts.iter().enumerate() {
        let end = cut.min(packet.len());
        if end <= start {
            continue;
        }

        let slice = &packet[start..end];
        if index == 0 {
            if slice.len() < TLS_RECORD_HEADER_LENGTH {
                return vec![FragmentedTlsRecord {
                    data: packet.to_vec(),
                }];
            }
            let fragment_len = slice.len() - TLS_RECORD_HEADER_LENGTH;
            let mut record = slice.to_vec();
            record[1] = 0x03;
            record[2] = 0x09;
            record[3] = (fragment_len >> 8) as u8;
            record[4] = fragment_len as u8;
            fragments.push(FragmentedTlsRecord { data: record });
        } else {
            let fragment_len = slice.len();
            let mut record = Vec::with_capacity(TLS_RECORD_HEADER_LENGTH + fragment_len);
            record.extend_from_slice(&[
                0x16,
                0x03,
                0x09,
                (fragment_len >> 8) as u8,
                fragment_len as u8,
            ]);
            record.extend_from_slice(slice);
            fragments.push(FragmentedTlsRecord { data: record });
        }
        start = end;
    }

    if start < packet.len() {
        let slice = &packet[start..];
        let fragment_len = slice.len();
        let mut record = Vec::with_capacity(TLS_RECORD_HEADER_LENGTH + fragment_len);
        record.extend_from_slice(&[
            0x16,
            0x03,
            0x09,
            (fragment_len >> 8) as u8,
            fragment_len as u8,
        ]);
        record.extend_from_slice(slice);
        fragments.push(FragmentedTlsRecord { data: record });
    }

    fragments
}
