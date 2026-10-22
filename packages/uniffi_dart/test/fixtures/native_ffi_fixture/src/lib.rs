//! Native protocol fixture. All non-native tooling and tests are Dart.
uniffi::setup_scaffolding!();

#[boltffi::data]
#[derive(uniffi::Record, Clone, Copy, Debug, PartialEq)]
#[repr(C)]
pub struct Point {
    pub x: f64,
    pub y: f64,
}

#[boltffi::data]
#[derive(uniffi::Record, Clone, Debug, PartialEq)]
pub struct Payload {
    pub name: String,
    pub bytes: Vec<u8>,
    pub points: Vec<Point>,
    pub optional: Option<u64>,
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Debug, PartialEq)]
pub enum Event {
    Progress { value: u64 },
    Completed { name: String },
}

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq)]
#[repr(u32)]
pub enum Mode {
    Ready = 3,
    Done = 9,
}

#[uniffi::export]
#[boltffi::export]
pub fn echo_mode(value: Mode) -> Mode {
    value
}

#[uniffi::export]
#[boltffi::export]
pub fn echo_optional(value: Option<u64>) -> Option<u64> {
    value
}

#[uniffi::export]
#[boltffi::export]
pub fn echo_bytes(value: Vec<u8>) -> Vec<u8> {
    value
}

#[uniffi::export]
#[boltffi::export]
pub fn echo_numbers(value: Vec<u64>) -> Vec<u64> {
    value
}

#[boltffi::error]
#[derive(uniffi::Error, thiserror::Error, Debug)]
pub enum FixtureError {
    #[error("Invalid value: {message}")]
    Invalid { message: String },
}

#[uniffi::export]
#[boltffi::export]
pub fn add(a: i64, b: i64) -> i64 {
    a + b
}

#[uniffi::export]
#[boltffi::export]
pub fn echo_u64(value: u64) -> u64 {
    value
}

#[uniffi::export]
#[boltffi::export]
pub fn echo_point(value: Point) -> Point {
    value
}

#[uniffi::export]
#[boltffi::export]
pub fn echo_payload(value: Payload) -> Payload {
    value
}

#[uniffi::export]
#[boltffi::export]
pub fn echo_event(value: Event) -> Event {
    value
}

#[uniffi::export]
#[boltffi::export]
pub fn divide(a: i32, b: i32) -> Result<i32, FixtureError> {
    if b == 0 {
        Err(FixtureError::Invalid {
            message: "division by zero".into(),
        })
    } else {
        Ok(a / b)
    }
}

#[uniffi::export]
#[boltffi::export]
pub fn string_error(fail: bool) -> Result<String, String> {
    if fail {
        Err("fixture string error".into())
    } else {
        Ok("ok".into())
    }
}

#[uniffi::export(async_runtime = "tokio")]
#[boltffi::export]
pub async fn delayed_echo(value: String, delay_ms: u32) -> String {
    async_compat::Compat::new(async move {
        tokio::time::sleep(std::time::Duration::from_millis(delay_ms.into())).await;
        value
    })
    .await
}

#[derive(uniffi::Object)]
pub struct Counter {
    value: std::sync::atomic::AtomicI64,
}

#[uniffi::export]
#[boltffi::export]
pub fn read_counter(value: &Counter) -> i64 {
    value.value.load(std::sync::atomic::Ordering::SeqCst)
}

#[uniffi::export]
#[boltffi::export]
impl Counter {
    #[uniffi::constructor]
    pub fn new(value: i64) -> Self {
        Self {
            value: std::sync::atomic::AtomicI64::new(value),
        }
    }
    pub fn increment(&self, amount: i64) -> i64 {
        self.value
            .fetch_add(amount, std::sync::atomic::Ordering::SeqCst)
            + amount
    }
    pub async fn read_later(&self, delay_ms:u32)->i64 {
        async_compat::Compat::new(async {
            tokio::time::sleep(std::time::Duration::from_millis(delay_ms.into())).await;
            self.value.load(std::sync::atomic::Ordering::SeqCst)
        }).await
    }
}

pub struct Ticker {
    producer: boltffi::StreamProducer<u32>,
}

#[boltffi::export]
impl Ticker {
    pub fn new() -> Self {
        Self {
            producer: boltffi::StreamProducer::new(64),
        }
    }
    pub fn emit(&self, value: u32) {
        self.producer.push(value);
    }
    #[ffi_stream(item = u32)]
    pub fn updates(&self) -> std::sync::Arc<boltffi::EventSubscription<u32>> {
        self.producer.subscribe()
    }
}

#[uniffi::export(async_runtime = "tokio")]
#[boltffi::export]
pub async fn async_divide(a: i32, b: i32) -> Result<i32, FixtureError> {
    divide(a, b)
}
