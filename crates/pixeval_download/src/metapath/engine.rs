// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::metapath::context::MacroContext;
use crate::metapath::eval::{analyze, reduce};
use crate::metapath::syntax::MacroAnalysisResult;

#[boltffi::error]
#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum MetaPathError {
    #[error("MetaPath evaluation error: {message}")]
    Evaluation { message: String },
}

#[uniffi::export]
pub fn analyze_meta_path(text: String) -> MacroAnalysisResult {
    analyze(&text)
}

#[uniffi::export]
pub fn reduce_meta_path(raw: String, ctx: MacroContext) -> Result<String, MetaPathError> {
    reduce(&raw, &ctx).map_err(|e| MetaPathError::Evaluation {
        message: e.to_string(),
    })
}

