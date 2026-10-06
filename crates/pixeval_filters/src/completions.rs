use crate::text::FilterTextSpan;

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct FilterCompletionItem {
    pub display_text: String,
    pub insert_text: String,
    pub replacement_span: FilterTextSpan,
    pub description: Option<String>,
    pub is_hint_only: bool,
}

impl FilterCompletionItem {
    pub fn new(
        display_text: impl Into<String>,
        insert_text: impl Into<String>,
        replacement_span: FilterTextSpan,
        description: Option<String>,
        is_hint_only: bool,
    ) -> Self {
        Self {
            display_text: display_text.into(),
            insert_text: insert_text.into(),
            replacement_span,
            description,
            is_hint_only,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct FilterCompletionDefinition {
    pub key: String,
    pub display_text: String,
    pub insert_text: String,
    pub description: Option<String>,
}

impl FilterCompletionDefinition {
    pub fn new(
        key: impl Into<String>,
        display_text: impl Into<String>,
        insert_text: impl Into<String>,
        description: Option<String>,
    ) -> Self {
        Self {
            key: key.into(),
            display_text: display_text.into(),
            insert_text: insert_text.into(),
            description,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct FilterFullCompletionDefinition {
    pub key: String,
    pub display_text: String,
    pub insert_text: String,
    pub description: Option<String>,
    pub covered_syntax_prefixes: Vec<String>,
}

impl FilterFullCompletionDefinition {
    pub fn new(
        key: impl Into<String>,
        display_text: impl Into<String>,
        insert_text: impl Into<String>,
        description: Option<String>,
        covered_syntax_prefixes: Vec<String>,
    ) -> Self {
        Self {
            key: key.into(),
            display_text: display_text.into(),
            insert_text: insert_text.into(),
            description,
            covered_syntax_prefixes,
        }
    }
}

use crate::values::FilterValueKind;

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct FilterValueCompletionContext {
    pub match_syntax_key: String,
    pub source: String,
    pub token_span: FilterTextSpan,
    pub value_span: FilterTextSpan,
    pub fragment_span: FilterTextSpan,
    pub is_negated: bool,
}

#[uniffi::export(callback_interface)]
pub trait FilterCompletionCallback: Send + Sync {
    fn get_completions(
        &self,
        context: FilterValueCompletionContext,
    ) -> Vec<FilterCompletionDefinition>;
}

pub trait FilterValueCompletionCallback: Send + Sync {
    fn get_completions(
        &self,
        context: &FilterValueCompletionContext,
    ) -> Vec<FilterCompletionDefinition>;
}

pub struct CallbackAdapter<'a>(pub &'a dyn FilterCompletionCallback);

impl<'a> FilterValueCompletionCallback for CallbackAdapter<'a> {
    fn get_completions(
        &self,
        context: &FilterValueCompletionContext,
    ) -> Vec<FilterCompletionDefinition> {
        self.0.get_completions(context.clone())
    }
}

#[derive(uniffi::Record, Clone, Debug, PartialEq, Eq)]
pub struct FilterValueHintGroup {
    pub kind: FilterValueKind,
    pub hints: Vec<FilterCompletionDefinition>,
}
