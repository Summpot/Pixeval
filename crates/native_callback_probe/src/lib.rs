use std::sync::atomic::{AtomicUsize, Ordering};

type Add = extern "C" fn(i32, i32) -> i32;
type Notify = extern "C" fn(i64);
type Fallible = extern "C" fn(i32, *mut i32, *mut i8);

static ACTIVE_THREADS: AtomicUsize = AtomicUsize::new(0);

struct ThreadGuard;
impl ThreadGuard {
    fn new() -> Self {
        ACTIVE_THREADS.fetch_add(1, Ordering::SeqCst);
        Self
    }
}
impl Drop for ThreadGuard {
    fn drop(&mut self) {
        ACTIVE_THREADS.fetch_sub(1, Ordering::SeqCst);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn probe_sync(callback: Add, a: i32, b: i32) -> i32 {
    callback(a, b)
}

#[unsafe(no_mangle)]
pub extern "C" fn probe_thread(callback: Add, a: i32, b: i32) -> i32 {
    std::thread::spawn(move || {
        let _guard = ThreadGuard::new();
        callback(a, b)
    }).join().unwrap_or(i32::MIN)
}

#[unsafe(no_mangle)]
pub extern "C" fn probe_concurrent(callback: Add, workers: u32, count: u32) -> i64 {
    let threads: Vec<_> = (0..workers).map(|worker| std::thread::spawn(move || {
        let _guard = ThreadGuard::new();
        (0..count).map(|i| i64::from(callback(worker as i32, i as i32))).sum::<i64>()
    })).collect();
    threads.into_iter().map(|thread| thread.join().unwrap_or(i64::MIN)).sum()
}

#[unsafe(no_mangle)]
pub extern "C" fn probe_notify(callback: Notify, count: u32) {
    std::thread::spawn(move || {
        let _guard = ThreadGuard::new();
        for i in 0..count { callback(i64::from(i)); }
    }).join().unwrap();
}

#[unsafe(no_mangle)]
pub extern "C" fn probe_status(callback: Fallible, input: i32) -> i64 {
    std::thread::spawn(move || {
        let _guard = ThreadGuard::new();
        let mut result = 0;
        let mut status = 0;
        callback(input, &mut result, &mut status);
        (i64::from(status) << 32) | i64::from(result as u32)
    }).join().unwrap_or(i64::MIN)
}

#[unsafe(no_mangle)]
pub extern "C" fn probe_active_threads() -> u32 {
    ACTIVE_THREADS.load(Ordering::SeqCst) as u32
}
