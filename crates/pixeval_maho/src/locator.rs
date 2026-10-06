// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ServerNameLocatingResult {
    Located,
    InvalidRecordHeader,
    InvalidHandshakeHeader,
    InvalidServerNameExtensionNameType,
    ServerNameExtensionNotFound,
    PacketTooShort,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct HostnameLocation {
    pub start: usize,
    pub length: usize,
}

pub struct ServerNameLocator<'a> {
    packet: &'a [u8],
    index: usize,
}

impl<'a> ServerNameLocator<'a> {
    pub const TLS_RECORD_HEADER_LENGTH: usize = 5;
    pub const SERVER_NAME_EXTENSION_ID: u16 = 0;

    pub fn new(packet: &'a [u8]) -> Self {
        Self { packet, index: 0 }
    }

    pub fn locate_server_names(&mut self) -> (ServerNameLocatingResult, Vec<HostnameLocation>) {
        if self.packet.len() < Self::TLS_RECORD_HEADER_LENGTH {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }

        // 1. Read & validate record header
        // Record type: 0x16 (Handshake)
        if self.packet[self.index] != 0x16 {
            return (ServerNameLocatingResult::InvalidRecordHeader, Vec::new());
        }
        self.index += 1;

        // Skip TLS record version (2 bytes)
        if self.index + 2 > self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        self.index += 2;

        // Record length (2 bytes)
        if self.index + 2 > self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        let record_len =
            ((self.packet[self.index] as usize) << 8) | (self.packet[self.index + 1] as usize);
        self.index += 2;

        if self.packet.len() - Self::TLS_RECORD_HEADER_LENGTH != record_len {
            return (ServerNameLocatingResult::InvalidRecordHeader, Vec::new());
        }

        // 2. Read & validate Handshake header
        if self.packet.len() - self.index < 4 {
            return (ServerNameLocatingResult::InvalidHandshakeHeader, Vec::new());
        }

        // Handshake type: 0x01 (ClientHello)
        if self.packet[self.index] != 0x01 {
            return (ServerNameLocatingResult::InvalidHandshakeHeader, Vec::new());
        }
        self.index += 1;

        // Skip 3 bytes handshake length
        self.index += 3;

        // 3. Skip client version (2 bytes)
        if self.index + 2 > self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        self.index += 2;

        // 4. Skip client random (32 bytes)
        if self.index + 32 > self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        self.index += 32;

        // 5. Skip session id
        if self.index >= self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        let session_id_len = self.packet[self.index] as usize;
        self.index += 1;
        if self.index + session_id_len > self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        self.index += session_id_len;

        // 6. Skip cipher suites
        if self.index + 2 > self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        let cipher_suites_len =
            ((self.packet[self.index] as usize) << 8) | (self.packet[self.index + 1] as usize);
        self.index += 2;
        if self.index + cipher_suites_len > self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        self.index += cipher_suites_len;

        // 7. Skip compression methods
        if self.index >= self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        let comp_len = self.packet[self.index] as usize;
        self.index += 1;
        if self.index + comp_len > self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        self.index += comp_len;

        // 8. Extensions length
        if self.index + 2 > self.packet.len() {
            return (ServerNameLocatingResult::PacketTooShort, Vec::new());
        }
        let _ext_total_len =
            ((self.packet[self.index] as usize) << 8) | (self.packet[self.index + 1] as usize);
        self.index += 2;

        // 9. Iterate extensions
        let mut locations = Vec::new();
        while self.index < self.packet.len() {
            if self.index + 4 > self.packet.len() {
                break;
            }
            let ext_id =
                ((self.packet[self.index] as u16) << 8) | (self.packet[self.index + 1] as u16);
            let ext_len = ((self.packet[self.index + 2] as usize) << 8)
                | (self.packet[self.index + 3] as usize);
            self.index += 4;

            if self.index + ext_len > self.packet.len() {
                break;
            }

            let ext_start = self.index;
            let ext_content = &self.packet[ext_start..ext_start + ext_len];
            self.index += ext_len;

            if ext_id == Self::SERVER_NAME_EXTENSION_ID {
                match Self::parse_sni_extension(ext_content) {
                    Ok(parsed) => {
                        for loc in parsed {
                            locations.push(HostnameLocation {
                                start: ext_start + loc.start,
                                length: loc.length,
                            });
                        }
                    }
                    Err(err) => return (err, Vec::new()),
                }
            }
        }

        if locations.is_empty() {
            (
                ServerNameLocatingResult::ServerNameExtensionNotFound,
                Vec::new(),
            )
        } else {
            (ServerNameLocatingResult::Located, locations)
        }
    }

    fn parse_sni_extension(
        content: &[u8],
    ) -> Result<Vec<HostnameLocation>, ServerNameLocatingResult> {
        if content.len() < 2 {
            return Err(ServerNameLocatingResult::InvalidServerNameExtensionNameType);
        }
        let mut idx = 2; // skip list length
        let mut res = Vec::new();

        while idx < content.len() {
            if content.len() < idx + 3 {
                return Err(ServerNameLocatingResult::InvalidServerNameExtensionNameType);
            }
            let name_type = content[idx];
            idx += 1;
            if name_type != 0 {
                return Err(ServerNameLocatingResult::InvalidServerNameExtensionNameType);
            }
            let name_len = ((content[idx] as usize) << 8) | (content[idx + 1] as usize);
            idx += 2;
            if name_len == 0 {
                continue;
            }
            if content.len() < idx + name_len {
                return Err(ServerNameLocatingResult::InvalidServerNameExtensionNameType);
            }
            res.push(HostnameLocation {
                start: idx,
                length: name_len,
            });
            idx += name_len;
        }

        Ok(res)
    }
}
