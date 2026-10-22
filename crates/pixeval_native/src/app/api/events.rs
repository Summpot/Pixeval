#[derive(Clone, Debug, uniffi::Enum)]
#[boltffi::data]
pub enum SessionEvent {
    AuthInvalidated { reason: String },
    RateLimited { retry_after_seconds: u64 },
}
