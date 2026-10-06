// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::metapath::ast::MacroTextSpan;

#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq)]
pub enum MacroHighlightKind {
    Delimiter,
    Name,
    Formatter,
    Separator,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq, Eq)]
pub struct MacroHighlightSpan {
    pub span: MacroTextSpan,
    pub kind: MacroHighlightKind,
    pub nesting_depth: i32,
}

#[derive(uniffi::Enum, Clone, Copy, Debug, PartialEq, Eq)]
pub enum MacroDiagnosticKind {
    UnexpectedToken,
    ExpectedLeftBraceAfterAt,
    ExpectedMacroName,
    MissingRightBrace,
    MissingConditionalSeparator,
    UnknownMacroName,
    NonParameterizedMacroBearingParameter,
    ConditionalBranchesMissing,
    InvalidFormatter,
    MacroContextRestrictionNotSatisfied,
    MacroShouldBeInLastSegment,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq, Eq)]
pub struct MacroDiagnostic {
    pub kind: MacroDiagnosticKind,
    pub span: MacroTextSpan,
    pub arguments: Vec<String>,
}

impl MacroDiagnostic {
    pub fn new(kind: MacroDiagnosticKind, span: MacroTextSpan, arguments: Vec<String>) -> Self {
        Self {
            kind,
            span,
            arguments,
        }
    }
}

#[derive(uniffi::Record, Clone, Debug, PartialEq, Eq, Default)]
pub struct MacroAnalysisResult {
    pub is_success: bool,
    pub diagnostics: Vec<MacroDiagnostic>,
    pub highlights: Vec<MacroHighlightSpan>,
}
