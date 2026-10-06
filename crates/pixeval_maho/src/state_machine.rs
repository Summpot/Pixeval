// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ClientHelloCollectingState {
    Idle,
    CollectingHeader,
    Collecting,
    Emitted,
}

pub struct ClientHelloStateMachine {
    state: ClientHelloCollectingState,
    expected_packet_size: usize,
    buffer: Vec<u8>,
    pub completed: bool,
}

pub struct StateMachineFlowResult {
    pub state: ClientHelloCollectingState,
    pub packet: Option<Vec<u8>>,
    pub remaining_bytes: Vec<u8>,
}

impl ClientHelloStateMachine {
    pub const TLS_RECORD_HEADER_LENGTH: usize = 5;
    pub const HANDSHAKE_IDENTIFIER: u8 = 0x16;

    pub fn new() -> Self {
        Self {
            state: ClientHelloCollectingState::Idle,
            expected_packet_size: 0,
            buffer: Vec::new(),
            completed: false,
        }
    }

    pub fn flow_state(&mut self, chunk: &[u8]) -> StateMachineFlowResult {
        if chunk.is_empty() {
            return StateMachineFlowResult {
                state: self.state,
                packet: None,
                remaining_bytes: Vec::new(),
            };
        }

        if self.state == ClientHelloCollectingState::Idle && chunk[0] != Self::HANDSHAKE_IDENTIFIER
        {
            return StateMachineFlowResult {
                state: ClientHelloCollectingState::Idle,
                packet: None,
                remaining_bytes: chunk.to_vec(),
            };
        }

        match self.state {
            ClientHelloCollectingState::Idle => {
                if chunk.len() >= Self::TLS_RECORD_HEADER_LENGTH {
                    let payload_size = ((chunk[3] as usize) << 8) | (chunk[4] as usize);
                    let total_size = Self::TLS_RECORD_HEADER_LENGTH + payload_size;
                    self.expected_packet_size = total_size;

                    if chunk.len() >= total_size {
                        let packet = chunk[..total_size].to_vec();
                        let remaining = chunk[total_size..].to_vec();
                        self.state = ClientHelloCollectingState::Emitted;
                        self.completed = true;
                        StateMachineFlowResult {
                            state: ClientHelloCollectingState::Emitted,
                            packet: Some(packet),
                            remaining_bytes: remaining,
                        }
                    } else {
                        self.buffer.extend_from_slice(chunk);
                        self.state = ClientHelloCollectingState::Collecting;
                        StateMachineFlowResult {
                            state: ClientHelloCollectingState::Collecting,
                            packet: None,
                            remaining_bytes: Vec::new(),
                        }
                    }
                } else {
                    self.buffer.extend_from_slice(chunk);
                    self.state = ClientHelloCollectingState::CollectingHeader;
                    StateMachineFlowResult {
                        state: ClientHelloCollectingState::CollectingHeader,
                        packet: None,
                        remaining_bytes: Vec::new(),
                    }
                }
            }
            ClientHelloCollectingState::CollectingHeader => {
                self.buffer.extend_from_slice(chunk);
                if self.buffer.len() >= Self::TLS_RECORD_HEADER_LENGTH {
                    let payload_size = ((self.buffer[3] as usize) << 8) | (self.buffer[4] as usize);
                    let total_size = Self::TLS_RECORD_HEADER_LENGTH + payload_size;
                    self.expected_packet_size = total_size;

                    if self.buffer.len() >= total_size {
                        let packet = self.buffer[..total_size].to_vec();
                        let remaining = self.buffer[total_size..].to_vec();
                        self.buffer.clear();
                        self.state = ClientHelloCollectingState::Emitted;
                        self.completed = true;
                        StateMachineFlowResult {
                            state: ClientHelloCollectingState::Emitted,
                            packet: Some(packet),
                            remaining_bytes: remaining,
                        }
                    } else {
                        self.state = ClientHelloCollectingState::Collecting;
                        StateMachineFlowResult {
                            state: ClientHelloCollectingState::Collecting,
                            packet: None,
                            remaining_bytes: Vec::new(),
                        }
                    }
                } else {
                    StateMachineFlowResult {
                        state: ClientHelloCollectingState::CollectingHeader,
                        packet: None,
                        remaining_bytes: Vec::new(),
                    }
                }
            }
            ClientHelloCollectingState::Collecting => {
                self.buffer.extend_from_slice(chunk);
                if self.buffer.len() >= self.expected_packet_size {
                    let packet = self.buffer[..self.expected_packet_size].to_vec();
                    let remaining = self.buffer[self.expected_packet_size..].to_vec();
                    self.buffer.clear();
                    self.state = ClientHelloCollectingState::Emitted;
                    self.completed = true;
                    StateMachineFlowResult {
                        state: ClientHelloCollectingState::Emitted,
                        packet: Some(packet),
                        remaining_bytes: remaining,
                    }
                } else {
                    StateMachineFlowResult {
                        state: ClientHelloCollectingState::Collecting,
                        packet: None,
                        remaining_bytes: Vec::new(),
                    }
                }
            }
            ClientHelloCollectingState::Emitted => StateMachineFlowResult {
                state: ClientHelloCollectingState::Emitted,
                packet: None,
                remaining_bytes: chunk.to_vec(),
            },
        }
    }
}

impl Default for ClientHelloStateMachine {
    fn default() -> Self {
        Self::new()
    }
}
