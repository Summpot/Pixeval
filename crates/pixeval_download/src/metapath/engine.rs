// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::sync::Arc;

use crate::metapath::context::MacroContext;
use crate::metapath::eval::{analyze, reduce};
use crate::metapath::syntax::MacroAnalysisResult;

#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum MetaPathError {
    #[error("MetaPath evaluation error: {message}")]
    Evaluation { message: String },
}

#[derive(uniffi::Object)]
pub struct MetaPathEngine;

#[uniffi::export]
impl MetaPathEngine {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Arc::new(Self)
    }

    pub fn analyze(&self, text: String) -> MacroAnalysisResult {
        analyze(&text)
    }

    pub fn reduce(&self, raw: String, ctx: MacroContext) -> Result<String, MetaPathError> {
        reduce(&raw, &ctx).map_err(|e| MetaPathError::Evaluation {
            message: e.to_string(),
        })
    }
}
