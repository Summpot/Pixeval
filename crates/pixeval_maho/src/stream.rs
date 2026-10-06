// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::VecDeque;
use std::io;
use std::pin::Pin;
use std::task::{Context, Poll};
use std::time::Duration;
use tokio::io::{AsyncRead, AsyncWrite, ReadBuf};
use tokio::time::Sleep;

use crate::fragmentation::split_client_hello;
use crate::state_machine::{ClientHelloCollectingState, ClientHelloStateMachine};

pub struct TlsFragmentedStream<S> {
    inner: S,
    state_machine: ClientHelloStateMachine,
    split_delay: Duration,
    outgoing_queue: VecDeque<Vec<u8>>,
    current_chunk_written: usize,
    active_delay: Option<Pin<Box<Sleep>>>,
}

impl<S> TlsFragmentedStream<S> {
    pub fn new(inner: S, split_delay_ms: u64) -> Self {
        Self {
            inner,
            state_machine: ClientHelloStateMachine::new(),
            split_delay: Duration::from_millis(split_delay_ms),
            outgoing_queue: VecDeque::new(),
            current_chunk_written: 0,
            active_delay: None,
        }
    }

    pub fn into_inner(self) -> S {
        self.inner
    }

    pub fn inner(&self) -> &S {
        &self.inner
    }

    pub fn inner_mut(&mut self) -> &mut S {
        &mut self.inner
    }
}

impl<S: AsyncRead + Unpin> AsyncRead for TlsFragmentedStream<S> {
    fn poll_read(
        mut self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut ReadBuf<'_>,
    ) -> Poll<io::Result<()>> {
        Pin::new(&mut self.inner).poll_read(cx, buf)
    }
}

impl<S: AsyncWrite + Unpin> AsyncWrite for TlsFragmentedStream<S> {
    fn poll_write(
        mut self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &[u8],
    ) -> Poll<io::Result<usize>> {
        // If we've completed fragmentation and drained our queue, act as direct transparent passthrough
        if self.state_machine.completed && self.outgoing_queue.is_empty() {
            return Pin::new(&mut self.inner).poll_write(cx, buf);
        }

        // First, ensure any pending outgoing fragmented chunks are sent
        if !self.outgoing_queue.is_empty() && self.as_mut().poll_flush_outgoing(cx)?.is_pending() {
            return Poll::Pending;
        }

        if self.state_machine.completed && self.outgoing_queue.is_empty() {
            return Pin::new(&mut self.inner).poll_write(cx, buf);
        }

        // Process incoming write through state machine
        let result = self.state_machine.flow_state(buf);
        match result.state {
            ClientHelloCollectingState::Idle => {
                // Not a TLS ClientHello packet, pass through directly
                Pin::new(&mut self.inner).poll_write(cx, buf)
            }
            ClientHelloCollectingState::CollectingHeader
            | ClientHelloCollectingState::Collecting => {
                // Consumed into internal buffer of state machine
                Poll::Ready(Ok(buf.len()))
            }
            ClientHelloCollectingState::Emitted => {
                if let Some(packet) = result.packet {
                    let fragments = split_client_hello(&packet);
                    for frag in fragments {
                        self.outgoing_queue.push_back(frag.data);
                    }
                    if !result.remaining_bytes.is_empty() {
                        self.outgoing_queue.push_back(result.remaining_bytes);
                    }
                }
                // Try to flush as much as possible right now
                let _ = self.as_mut().poll_flush_outgoing(cx)?;
                Poll::Ready(Ok(buf.len()))
            }
        }
    }

    fn poll_flush(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        if !self.outgoing_queue.is_empty() && self.as_mut().poll_flush_outgoing(cx)?.is_pending() {
            return Poll::Pending;
        }
        Pin::new(&mut self.inner).poll_flush(cx)
    }

    fn poll_shutdown(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        if !self.outgoing_queue.is_empty() && self.as_mut().poll_flush_outgoing(cx)?.is_pending() {
            return Poll::Pending;
        }
        Pin::new(&mut self.inner).poll_shutdown(cx)
    }
}

impl<S: AsyncWrite + Unpin> TlsFragmentedStream<S> {
    fn poll_flush_outgoing(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        let Self {
            inner,
            outgoing_queue,
            current_chunk_written,
            active_delay,
            split_delay,
            ..
        } = &mut *self;

        while let Some(front) = outgoing_queue.front() {
            // Check if there is an active delay between fragments
            if let Some(delay) = active_delay {
                if delay.as_mut().poll(cx).is_pending() {
                    return Poll::Pending;
                }
                *active_delay = None;
            }

            let slice = &front[*current_chunk_written..];
            let n = match Pin::new(&mut *inner).poll_write(cx, slice) {
                Poll::Ready(Ok(n)) => n,
                Poll::Ready(Err(e)) => return Poll::Ready(Err(e)),
                Poll::Pending => return Poll::Pending,
            };

            *current_chunk_written += n;
            if *current_chunk_written >= front.len() {
                outgoing_queue.pop_front();
                *current_chunk_written = 0;

                // If more chunks remain, enforce sleep delay between them
                if !outgoing_queue.is_empty() && !split_delay.is_zero() {
                    *active_delay = Some(Box::pin(tokio::time::sleep(*split_delay)));
                }
            }
        }

        // Flush inner stream once queue is emptied
        Pin::new(inner).poll_flush(cx)
    }
}
