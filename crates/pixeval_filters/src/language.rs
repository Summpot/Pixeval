use std::collections::{HashMap, HashSet};

use crate::ast::FilterQuery;
use crate::completions::{
    FilterCompletionDefinition, FilterCompletionItem, FilterCompletionKind,
    FilterFullCompletionDefinition, FilterValueCompletionCallback, FilterValueCompletionContext,
};
use crate::diagnostics::FilterDiagnostic;
use crate::parser::Parser;
use crate::syntax::{FilterSyntaxDefinition, FilterSyntaxMatch};
use crate::text::{FilterTextSpan, Utf16Mapping};
use crate::values::FilterValueKind;

#[derive(Debug, Clone, PartialEq)]
pub struct FilterLanguageResult {
    pub query: Option<FilterQuery>,
    pub diagnostics: Vec<FilterDiagnostic>,
    pub completions: Vec<FilterCompletionItem>,
    pub is_success: bool,
}

pub struct FilterLanguage {
    matches: Vec<FilterSyntaxMatch>,
    default_text_match: Option<FilterSyntaxMatch>,
    intrinsic_completions: Vec<FilterCompletionDefinition>,
    full_completions: Vec<FilterFullCompletionDefinition>,
    value_hint_completions: HashMap<FilterValueKind, Vec<FilterCompletionDefinition>>,
    full_completion_covered_syntax_prefixes: Vec<String>,
    special_starters: Vec<char>,
}

pub(crate) fn apply_completion(utf16: &[u16], span: FilterTextSpan, insert_text: &str) -> String {
    let start = (span.start as usize).min(utf16.len());
    let end = (span.end() as usize).min(utf16.len());
    let mut result = String::new();
    if let Ok(prefix) = String::from_utf16(&utf16[..start]) {
        result.push_str(&prefix);
    }
    result.push_str(insert_text);
    if let Ok(suffix) = String::from_utf16(&utf16[end..]) {
        result.push_str(&suffix);
    }
    result
}

impl FilterLanguage {
    pub fn new(
        syntaxes: Vec<FilterSyntaxDefinition>,
        intrinsic_completions: Option<Vec<FilterCompletionDefinition>>,
        full_completions: Option<Vec<FilterFullCompletionDefinition>>,
        value_hint_completions: Option<HashMap<FilterValueKind, Vec<FilterCompletionDefinition>>>,
    ) -> Self {
        let mut expanded: Vec<FilterSyntaxMatch> = Vec::new();
        for syntax in &syntaxes {
            for pattern in &syntax.patterns {
                expanded.extend(pattern.expand(
                    &syntax.key,
                    syntax.value_kind,
                    syntax.example_value.as_deref(),
                ));
            }
        }

        let default_intrinsics = vec![
            FilterCompletionDefinition::new(
                "builtin.and",
                "and",
                "and ",
                Some("逻辑与分组".to_string()),
            ),
            FilterCompletionDefinition::new(
                "builtin.or",
                "or",
                "or ",
                Some("逻辑或分组".to_string()),
            ),
            FilterCompletionDefinition::new("builtin.not", "!", "!", Some("逻辑非".to_string())),
        ];

        let intrinsic = intrinsic_completions.unwrap_or(default_intrinsics);
        let full = full_completions.unwrap_or_default();
        let value_hints = value_hint_completions.unwrap_or_default();

        let mut covered_prefixes = Vec::new();
        for fc in &full {
            for prefix in &fc.covered_syntax_prefixes {
                if !prefix.is_empty() {
                    covered_prefixes.push(prefix.clone());
                }
            }
        }

        let default_text = expanded
            .iter()
            .find(|m| m.value_kind == FilterValueKind::Text && m.header_text.is_empty())
            .cloned();

        let header_matches: Vec<FilterSyntaxMatch> = expanded
            .into_iter()
            .filter(|m| !m.header_text.is_empty())
            .collect();

        let mut special_starters = Vec::new();
        for m in &header_matches {
            if let Some(first) = m.header_text.chars().next()
                && !first.is_alphanumeric()
                && !special_starters.contains(&first)
            {
                special_starters.push(first);
            }
        }

        Self {
            matches: header_matches,
            default_text_match: default_text,
            intrinsic_completions: intrinsic,
            full_completions: full,
            value_hint_completions: value_hints,
            full_completion_covered_syntax_prefixes: covered_prefixes,
            special_starters,
        }
    }

    pub fn analyze(
        &self,
        text: &str,
        caret_position: i32,
        callback: Option<&dyn FilterValueCompletionCallback>,
    ) -> FilterLanguageResult {
        let normalized = text;
        let parser = Parser::new(
            normalized,
            &self.matches,
            self.default_text_match.as_ref(),
            &self.special_starters,
        );
        let (query, diagnostics) = parser.parse();

        let mapping = Utf16Mapping::new(normalized);
        let utf16: Vec<u16> = normalized.encode_utf16().collect();
        let caret = if caret_position < 0 {
            utf16.len() as i32
        } else {
            caret_position.clamp(0, utf16.len() as i32)
        };

        let completions = self.get_completions(normalized, &mapping, &utf16, caret, callback);
        let is_success = diagnostics.is_empty();

        FilterLanguageResult {
            query,
            diagnostics,
            completions,
            is_success,
        }
    }

    fn get_completions(
        &self,
        text: &str,
        mapping: &Utf16Mapping,
        utf16: &[u16],
        caret: i32,
        callback: Option<&dyn FilterValueCompletionCallback>,
    ) -> Vec<FilterCompletionItem> {
        let token_start = self.find_token_start(utf16, caret);
        let token_end = self.find_token_end(utf16, caret);
        let replacement_span = FilterTextSpan::from_bounds(token_start, token_end);

        let fragment_slice = if caret > token_start {
            mapping.slice(text, FilterTextSpan::from_bounds(token_start, caret))
        } else {
            ""
        };

        let is_negated = fragment_slice.starts_with('!');
        let fragment = if is_negated {
            &fragment_slice[1..]
        } else {
            fragment_slice
        };

        let follows_left_paren = self.is_after_left_parenthesis(utf16, token_start);
        if follows_left_paren {
            let mut group_completions = Vec::new();
            let mut seen = HashSet::new();
            self.append_intrinsic_completions(
                utf16,
                "",
                replacement_span,
                &mut group_completions,
                &mut seen,
                is_negated,
                follows_left_paren,
            );
            return group_completions;
        }

        if let Some(cb) = callback
            && let Some(context) = self.try_create_value_completion_context(
                text,
                mapping,
                caret,
                token_start,
                token_end,
                is_negated,
            )
        {
            let dynamic_items = cb.get_completions(&context);
            if !dynamic_items.is_empty() {
                let mut filtered = Vec::new();
                let mut seen = HashSet::new();
                let frag_str = context.fragment_span.get_text(text);
                for item in dynamic_items {
                    if (item
                        .display_text
                        .to_lowercase()
                        .starts_with(&frag_str.to_lowercase())
                        || item
                            .insert_text
                            .to_lowercase()
                            .starts_with(&frag_str.to_lowercase()))
                        && seen.insert(item.key.clone())
                    {
                        let completed_text = apply_completion(utf16, context.value_span, &item.insert_text);
                        filtered.push(FilterCompletionItem::new(
                            item.display_text,
                            item.insert_text,
                            completed_text,
                            context.value_span,
                            item.description,
                            false,
                            FilterCompletionKind::Value,
                        ));
                    }
                }
                if !filtered.is_empty() {
                    return filtered;
                }
            }
        }

        if let Some(hints) = self.try_create_value_hint_completions(
            text,
            mapping,
            caret,
            token_start,
            token_end,
            is_negated,
        ) {
            return hints;
        }

        let completion_fragment = if is_negated { "" } else { fragment };
        let mut completions = Vec::new();
        let mut seen = HashSet::new();

        self.append_intrinsic_completions(
            utf16,
            completion_fragment,
            replacement_span,
            &mut completions,
            &mut seen,
            is_negated,
            follows_left_paren,
        );

        if let Some(ref default_text) = self.default_text_match
            && let Some(ref example) = default_text.example_value
            && !example.is_empty()
            && example
                .to_lowercase()
                .starts_with(&completion_fragment.to_lowercase())
        {
            let insert_text = if is_negated {
                format!("!{}", default_text.completion_insert_text)
            } else {
                default_text.completion_insert_text.clone()
            };
            let key = format!("{}|{:?}", default_text.syntax_key, default_text.metadata);
            if seen.insert(key) {
                let completed_text = apply_completion(utf16, replacement_span, &insert_text);
                completions.push(FilterCompletionItem::new(
                    example,
                    insert_text,
                    completed_text,
                    replacement_span,
                    default_text.description.clone(),
                    false,
                    FilterCompletionKind::Keyword,
                ));
            }
        }

        if completion_fragment.is_empty() {
            self.append_full_completions(utf16, replacement_span, &mut completions, &mut seen, is_negated);
        }

        for m in &self.matches {
            let completion = &m.completion_text;
            if completion.is_empty()
                || !completion
                    .to_lowercase()
                    .starts_with(&completion_fragment.to_lowercase())
            {
                continue;
            }

            if completion_fragment.is_empty() && self.is_covered_by_full_completion(m) {
                continue;
            }

            let insert_text = if is_negated {
                format!("!{}", m.completion_insert_text)
            } else {
                m.completion_insert_text.clone()
            };

            let key = format!("{}|{:?}", m.syntax_key, m.metadata);
            if seen.insert(key) {
                let kind = if m.header_text.starts_with('#') || m.header_text.starts_with('@') {
                    FilterCompletionKind::Prefix
                } else if m.value_kind == FilterValueKind::Flag {
                    FilterCompletionKind::Flag
                } else {
                    FilterCompletionKind::Keyword
                };
                let completed_text = apply_completion(utf16, replacement_span, &insert_text);
                completions.push(FilterCompletionItem::new(
                    completion,
                    insert_text,
                    completed_text,
                    replacement_span,
                    m.description.clone(),
                    false,
                    kind,
                ));
            }
        }

        completions
    }

    fn find_token_start(&self, utf16: &[u16], caret: i32) -> i32 {
        let mut index = caret as usize;
        while index > 0 {
            let prev = utf16[index - 1];
            if prev == b' ' as u16
                || prev == b'\t' as u16
                || prev == b'\r' as u16
                || prev == b'\n' as u16
                || prev == b'(' as u16
                || prev == b')' as u16
            {
                break;
            }
            index -= 1;
        }
        index as i32
    }

    fn find_token_end(&self, utf16: &[u16], caret: i32) -> i32 {
        let mut index = caret as usize;
        while index < utf16.len() {
            let curr = utf16[index];
            if curr == b' ' as u16
                || curr == b'\t' as u16
                || curr == b'\r' as u16
                || curr == b'\n' as u16
                || curr == b'(' as u16
                || curr == b')' as u16
            {
                break;
            }
            index += 1;
        }
        index as i32
    }

    fn is_after_left_parenthesis(&self, utf16: &[u16], token_start: i32) -> bool {
        let mut index = token_start as isize - 1;
        while index >= 0 {
            let ch = utf16[index as usize];
            if ch == b' ' as u16 || ch == b'\t' as u16 || ch == b'\r' as u16 || ch == b'\n' as u16 {
                index -= 1;
                continue;
            }
            return ch == b'(' as u16;
        }
        false
    }

    fn try_create_value_completion_context(
        &self,
        text: &str,
        mapping: &Utf16Mapping,
        caret: i32,
        token_start: i32,
        token_end: i32,
        is_negated: bool,
    ) -> Option<FilterValueCompletionContext> {
        let token_offset = if is_negated { 1 } else { 0 };
        let header_start = token_start + token_offset;
        if header_start >= token_end {
            return None;
        }

        let token = mapping.slice(text, FilterTextSpan::from_bounds(header_start, token_end));
        for m in &self.matches {
            if m.value_kind == FilterValueKind::None
                || !token
                    .to_lowercase()
                    .starts_with(&m.header_text.to_lowercase())
            {
                continue;
            }

            let header_utf16_len = m.header_text.encode_utf16().count() as i32;
            let value_start = header_start + header_utf16_len;
            if caret < value_start {
                break;
            }

            return Some(FilterValueCompletionContext {
                match_syntax_key: m.syntax_key.clone(),
                source: text.to_string(),
                token_span: FilterTextSpan::from_bounds(token_start, token_end),
                value_span: FilterTextSpan::from_bounds(value_start, token_end),
                fragment_span: FilterTextSpan::from_bounds(value_start, caret),
                is_negated,
            });
        }

        None
    }

    fn try_create_value_hint_completions(
        &self,
        text: &str,
        mapping: &Utf16Mapping,
        caret: i32,
        token_start: i32,
        token_end: i32,
        is_negated: bool,
    ) -> Option<Vec<FilterCompletionItem>> {
        let context = self.try_create_value_completion_context(
            text,
            mapping,
            caret,
            token_start,
            token_end,
            is_negated,
        )?;
        let syntax_match = self
            .matches
            .iter()
            .find(|m| m.syntax_key == context.match_syntax_key)?;

        let definitions = self.value_hint_completions.get(&syntax_match.value_kind)?;
        if definitions.is_empty() {
            return None;
        }

        let token_text = context.token_span.get_text(text).to_string();
        let items = definitions
            .iter()
            .map(|d| {
                FilterCompletionItem::new(
                    &d.display_text,
                    &token_text,
                    text,
                    context.token_span,
                    d.description.clone(),
                    true,
                    FilterCompletionKind::Hint,
                )
            })
            .collect();

        Some(items)
    }

    fn append_intrinsic_completions(
        &self,
        utf16: &[u16],
        fragment: &str,
        replacement_span: FilterTextSpan,
        completions: &mut Vec<FilterCompletionItem>,
        seen: &mut HashSet<String>,
        is_negated: bool,
        follows_left_paren: bool,
    ) {
        for c in &self.intrinsic_completions {
            let is_group = c.key == "builtin.and" || c.key == "builtin.or";
            if follows_left_paren && !is_group {
                continue;
            }

            let disp_matches = c
                .display_text
                .to_lowercase()
                .starts_with(&fragment.to_lowercase());
            let ins_matches = c
                .insert_text
                .to_lowercase()
                .starts_with(&fragment.to_lowercase());
            if !disp_matches && !ins_matches {
                continue;
            }

            if seen.insert(c.key.clone()) {
                let insert_text = if !is_group || follows_left_paren {
                    c.insert_text.clone()
                } else if is_negated {
                    format!("!({}", c.insert_text)
                } else {
                    format!("({}", c.insert_text)
                };

                let completed_text = apply_completion(utf16, replacement_span, &insert_text);
                completions.push(FilterCompletionItem::new(
                    &c.display_text,
                    insert_text,
                    completed_text,
                    replacement_span,
                    c.description.clone(),
                    false,
                    FilterCompletionKind::Operator,
                ));
            }
        }
    }

    fn append_full_completions(
        &self,
        utf16: &[u16],
        replacement_span: FilterTextSpan,
        completions: &mut Vec<FilterCompletionItem>,
        seen: &mut HashSet<String>,
        is_negated: bool,
    ) {
        for fc in &self.full_completions {
            let key = format!("full-completion|{}", fc.key);
            if seen.insert(key) {
                let insert_text = if is_negated {
                    format!("!{}", fc.insert_text)
                } else {
                    fc.insert_text.clone()
                };

                let completed_text = apply_completion(utf16, replacement_span, &insert_text);
                completions.push(FilterCompletionItem::new(
                    &fc.display_text,
                    insert_text,
                    completed_text,
                    replacement_span,
                    fc.description.clone(),
                    false,
                    FilterCompletionKind::Flag,
                ));
            }
        }
    }

    fn is_covered_by_full_completion(&self, m: &FilterSyntaxMatch) -> bool {
        self.full_completion_covered_syntax_prefixes
            .iter()
            .any(|p| m.header_text.to_lowercase().starts_with(&p.to_lowercase()))
    }
}

use std::sync::Arc;

#[derive(uniffi::Record, Debug, Clone)]
pub struct FilterAnalysisResult {
    pub has_query: bool,
    pub is_success: bool,
    pub diagnostics: Vec<FilterDiagnostic>,
    pub completions: Vec<FilterCompletionItem>,
    pub query_handle: Option<Arc<FilterQuery>>,
    pub ast_root: Option<crate::ast::FilterAstNode>,
}

#[derive(uniffi::Object)]
pub struct FilterEngine {
    inner: FilterLanguage,
}

#[uniffi::export]
impl FilterEngine {
    #[uniffi::constructor]
    pub fn new(
        syntaxes: Vec<FilterSyntaxDefinition>,
        intrinsic_completions: Option<Vec<FilterCompletionDefinition>>,
        full_completions: Option<Vec<FilterFullCompletionDefinition>>,
        value_hints: Option<Vec<crate::completions::FilterValueHintGroup>>,
    ) -> Arc<Self> {
        let rust_hints = value_hints.map(|groups| {
            groups
                .into_iter()
                .map(|g| (g.kind, g.hints))
                .collect()
        });

        let inner = FilterLanguage::new(syntaxes, intrinsic_completions, full_completions, rust_hints);
        Arc::new(Self { inner })
    }

    pub fn analyze(
        &self,
        text: String,
        caret_position: i32,
        callback: Option<Box<dyn crate::completions::FilterCompletionCallback>>,
    ) -> FilterAnalysisResult {
        let cb_adapter = callback.as_ref().map(|cb| crate::completions::CallbackAdapter(cb.as_ref()));
        let cb_ref: Option<&dyn crate::completions::FilterValueCompletionCallback> = cb_adapter
            .as_ref()
            .map(|a| a as &dyn crate::completions::FilterValueCompletionCallback);

        let result = self.inner.analyze(&text, caret_position, cb_ref);

        let ast_root = result
            .query
            .as_ref()
            .map(|q| crate::ast::FilterAstNode::from(&crate::ast::FilterNode::Group(q.root.clone())));
        let query_handle = result.query.map(Arc::new);
        let has_query = query_handle.is_some();

        FilterAnalysisResult {
            has_query,
            is_success: result.is_success,
            diagnostics: result.diagnostics,
            completions: result.completions,
            query_handle,
            ast_root,
        }
    }
}
