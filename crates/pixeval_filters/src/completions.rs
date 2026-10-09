use crate::text::FilterTextSpan;

#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)]
pub enum FilterCompletionKind {
    Keyword,
    Prefix,
    Value,
    Flag,
    Operator,
    Hint,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct FilterCompletionItem {
    pub display_text: String,
    pub insert_text: String,
    pub completed_text: String,
    pub replacement_span: FilterTextSpan,
    pub description: Option<String>,
    pub is_hint_only: bool,
    pub kind: FilterCompletionKind,
}

impl FilterCompletionItem {
    pub fn new(
        display_text: impl Into<String>,
        insert_text: impl Into<String>,
        completed_text: impl Into<String>,
        replacement_span: FilterTextSpan,
        description: Option<String>,
        is_hint_only: bool,
        kind: FilterCompletionKind,
    ) -> Self {
        Self {
            display_text: display_text.into(),
            insert_text: insert_text.into(),
            completed_text: completed_text.into(),
            replacement_span,
            description,
            is_hint_only,
            kind,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct TagCandidate {
    pub name: String,
    pub translated_name: Option<String>,
}

impl TagCandidate {
    pub fn new(name: impl Into<String>, translated_name: Option<String>) -> Self {
        Self {
            name: name.into(),
            translated_name,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct AuthorCandidate {
    pub name: String,
    pub account: Option<String>,
}

impl AuthorCandidate {
    pub fn new(name: impl Into<String>, account: Option<String>) -> Self {
        Self {
            name: name.into(),
            account,
        }
    }
}

#[uniffi::export(callback_interface)]
pub trait FilterStorageProvider: Send + Sync {
    fn query_search_history_tags(&self, pattern: String, limit: u32) -> Vec<TagCandidate>;
    fn query_subscription_authors(&self, pattern: String, limit: u32) -> Vec<AuthorCandidate>;
}

#[derive(Debug, Clone, PartialEq, Eq, Default, uniffi::Record)]
pub struct FilterLocalization {
    pub and_description: Option<String>,
    pub or_description: Option<String>,
    pub not_description: Option<String>,
    pub title_description: Option<String>,
    pub author_description: Option<String>,
    pub tag_description: Option<String>,
    pub bookmark_description: Option<String>,
    pub ratio_description: Option<String>,
    pub start_date_description: Option<String>,
    pub end_date_description: Option<String>,
    pub include_constraint_description: Option<String>,
    pub exclude_constraint_description: Option<String>,
    pub include_ai_description: Option<String>,
    pub exclude_ai_description: Option<String>,
    pub include_r18_description: Option<String>,
    pub exclude_r18_description: Option<String>,
    pub include_r18g_description: Option<String>,
    pub exclude_r18g_description: Option<String>,
    pub include_gif_description: Option<String>,
    pub exclude_gif_description: Option<String>,
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
