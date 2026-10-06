// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use thiserror::Error;

#[derive(Debug, Error, uniffi::Error)]
pub enum SubscriptionError {
    #[error("Subscription not found: {id}")]
    NotFound { id: i64 },

    #[error("Sync operation cancelled")]
    Cancelled,

    #[error("Execution error: {message}")]
    Execution { message: String },
}
