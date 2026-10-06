// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;

use crate::metapath::ast::{MacroNode, Sequence, SingleNode, TextSpan};
use crate::metapath::syntax::{MacroDiagnostic, MacroDiagnosticKind};

#[derive(Clone, Debug)]
struct LastSegmentRecord {
    name: String,
    span: TextSpan,
    context: HashMap<String, bool>,
}

pub struct MacroBinder {
    diagnostics: Vec<MacroDiagnostic>,
    last_segment_records: Vec<LastSegmentRecord>,
}

impl MacroBinder {
    pub fn new() -> Self {
        Self {
            diagnostics: Vec::new(),
            last_segment_records: Vec::new(),
        }
    }

    pub fn bind(mut self, root: &Option<Sequence>) -> Vec<MacroDiagnostic> {
        let mut context = HashMap::new();
        self.bind_sequence(root, &mut context);
        self.diagnostics
    }

    fn bind_sequence(&mut self, sequence: &Option<Sequence>, context: &mut HashMap<String, bool>) {
        let Some(seq) = sequence else { return };
        for node in &seq.nodes {
            if !self.diagnostics.is_empty() {
                break;
            }
            self.bind_node(node, context);
        }
    }

    fn bind_node(&mut self, node: &SingleNode, context: &mut HashMap<String, bool>) {
        match node {
            SingleNode::PlainText(pt) => {
                if pt.text.contains('\\') {
                    self.validate_last_segment_context(context);
                }
            }
            SingleNode::Macro(m) => {
                self.bind_macro(m, context);
            }
        }
    }

    fn bind_macro(&mut self, m: &MacroNode, context: &mut HashMap<String, bool>) {
        let name = &m.name.text;
        let span = m.name.span;
        let formatter_text = m.formatter.as_ref().map(|f| f.text.as_str());
        let formatter_span = m.formatter.as_ref().map(|f| f.span).unwrap_or(span);

        if is_transducer(name) {
            if !is_formatter_valid_for_transducer(name, formatter_text) {
                self.add_diagnostic(
                    MacroDiagnosticKind::InvalidFormatter,
                    formatter_span,
                    vec![
                        name.to_string(),
                        formatter_text.unwrap_or_default().to_string(),
                    ],
                );
                return;
            }
            if m.branches.is_some() {
                self.add_diagnostic(
                    MacroDiagnosticKind::NonParameterizedMacroBearingParameter,
                    span,
                    vec![name.to_string()],
                );
                return;
            }
            if !check_context_restriction(name, context) {
                self.add_diagnostic(
                    MacroDiagnosticKind::MacroContextRestrictionNotSatisfied,
                    span,
                    vec![name.to_string()],
                );
                return;
            }
            if is_last_segment_macro(name) {
                self.last_segment_records.push(LastSegmentRecord {
                    name: name.to_string(),
                    span,
                    context: context.clone(),
                });
            }
        } else if is_predicate(name) {
            if formatter_text.is_some() {
                self.add_diagnostic(
                    MacroDiagnosticKind::InvalidFormatter,
                    formatter_span,
                    vec![
                        name.to_string(),
                        formatter_text.unwrap_or_default().to_string(),
                    ],
                );
                return;
            }
            let Some(branches) = &m.branches else {
                self.add_diagnostic(
                    MacroDiagnosticKind::ConditionalBranchesMissing,
                    span,
                    vec![name.to_string()],
                );
                return;
            };

            let prev = context.insert(name.to_string(), true);
            self.bind_sequence(&branches.when_true, context);
            if let Some(p) = prev {
                context.insert(name.to_string(), p);
            } else {
                context.remove(name);
            }

            let prev = context.insert(name.to_string(), false);
            self.bind_sequence(&branches.when_false, context);
            if let Some(p) = prev {
                context.insert(name.to_string(), p);
            } else {
                context.remove(name);
            }
        } else {
            self.add_diagnostic(
                MacroDiagnosticKind::UnknownMacroName,
                span,
                vec![name.to_string()],
            );
        }
    }

    fn validate_last_segment_context(&mut self, context: &HashMap<String, bool>) {
        for record in &self.last_segment_records {
            if is_context_compatible(&record.context, context) {
                self.add_diagnostic(
                    MacroDiagnosticKind::MacroShouldBeInLastSegment,
                    record.span,
                    vec![record.name.clone()],
                );
                return;
            }
        }
    }

    fn add_diagnostic(
        &mut self,
        kind: MacroDiagnosticKind,
        span: TextSpan,
        arguments: Vec<String>,
    ) {
        if self.diagnostics.is_empty() {
            self.diagnostics
                .push(MacroDiagnostic::new(kind, span, arguments));
        }
    }
}

fn is_context_compatible(expected: &HashMap<String, bool>, actual: &HashMap<String, bool>) -> bool {
    for (k, v) in expected {
        if let Some(act) = actual.get(k) {
            if act != v {
                return false;
            }
        }
    }
    true
}

pub fn is_transducer(name: &str) -> bool {
    matches!(
        name,
        "id" | "artist_id"
            | "artist_name"
            | "work_title"
            | "title"
            | "work_publish_time"
            | "publish_time"
            | "ext"
            | "group_id"
            | "series_id"
            | "series_title"
            | "pic_set_index"
    )
}

pub fn is_predicate(name: &str) -> bool {
    matches!(
        name,
        "is_group"
            | "is_bookmark_group"
            | "is_post_group"
            | "is_series_group"
            | "is_series"
            | "is_pic_set"
            | "is_pic_one"
            | "is_pic_gif"
            | "is_r18"
            | "is_r18g"
            | "is_ai"
            | "is_novel"
    )
}

fn is_last_segment_macro(name: &str) -> bool {
    matches!(name, "ext" | "pic_set_index")
}

fn check_context_restriction(name: &str, context: &HashMap<String, bool>) -> bool {
    match name {
        "pic_set_index" => context.get("is_pic_set").copied().unwrap_or(false),
        "group_id" => {
            context.get("is_group").copied().unwrap_or(false)
                || context.get("is_bookmark_group").copied().unwrap_or(false)
                || context.get("is_post_group").copied().unwrap_or(false)
                || context.get("is_series_group").copied().unwrap_or(false)
        }
        _ => true,
    }
}

fn is_formatter_valid_for_transducer(name: &str, formatter: Option<&str>) -> bool {
    match name {
        "id" | "artist_id" | "artist_name" | "work_title" | "title" | "series_title" | "ext" => {
            is_string_formatter_valid(formatter)
        }
        "group_id" | "series_id" | "pic_set_index" => is_integer_formatter_valid(formatter),
        "work_publish_time" | "publish_time" => is_datetime_formatter_valid(formatter),
        _ => formatter.is_none(),
    }
}

fn is_string_formatter_valid(formatter: Option<&str>) -> bool {
    matches!(formatter, None | Some("u") | Some("l"))
}

fn is_integer_formatter_valid(formatter: Option<&str>) -> bool {
    let Some(f) = formatter else { return true };
    if f.is_empty() || f.chars().any(is_invalid_name_char_in_macro) {
        return false;
    }
    // Check if it consists of 0s (like 00, 000) or valid formatting flags like D, d, x, etc.
    f.chars().all(|c| c.is_ascii_digit())
        || matches!(
            f.to_ascii_lowercase().as_str(),
            "d" | "d1" | "d2" | "d3" | "d4" | "d5" | "d6" | "d7" | "d8"
        )
}

fn is_datetime_formatter_valid(formatter: Option<&str>) -> bool {
    let Some(f) = formatter else { return true };
    if f.is_empty() || f.chars().any(is_invalid_name_char_in_macro) {
        return false;
    }
    // Basic date formatter sanity check
    f.chars().all(|c| {
        c.is_ascii_alphanumeric()
            || matches!(c, '-' | '_' | '.' | ' ' | '/' | ':' | '年' | '月' | '日')
    })
}

fn is_invalid_name_char_in_macro(c: char) -> bool {
    matches!(c, '<' | '>' | ':' | '\\' | '/' | '*' | '?' | '\"' | '|') || (c as u32) < 32
}
