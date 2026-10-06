// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use chrono::{DateTime, Utc};
use thiserror::Error;

use crate::metapath::ast::{MacroNode, Sequence, SingleNode};
use crate::metapath::binder::{MacroBinder, is_predicate, is_transducer};
use crate::metapath::context::{MacroContext, MacroImageType};
use crate::metapath::parser::MacroSyntaxParser;
use crate::metapath::syntax::{MacroAnalysisResult, MacroDiagnosticKind};

#[derive(Debug, Error, Clone, PartialEq, Eq)]
pub enum MacroEvalError {
    #[error("Syntax or semantic error: {kind:?}")]
    Diagnostic {
        kind: MacroDiagnosticKind,
        arguments: Vec<String>,
    },
    #[error("Macro result is empty")]
    ResultIsEmpty,
    #[error("Macro reduction not completed: {names:?}")]
    ReductionNotCompleted { names: Vec<String> },
}

pub fn analyze(text: &str) -> MacroAnalysisResult {
    if text.trim().is_empty() {
        return MacroAnalysisResult::default();
    }

    let (root, highlights, mut diagnostics) = MacroSyntaxParser::new(text).parse();
    if diagnostics.is_empty() {
        let binder_diagnostics = MacroBinder::new().bind(&root);
        if !binder_diagnostics.is_empty() {
            diagnostics = binder_diagnostics;
        }
    }

    let is_success = diagnostics.is_empty();
    MacroAnalysisResult {
        is_success,
        diagnostics,
        highlights,
    }
}

pub fn reduce(raw: &str, context: &MacroContext) -> Result<String, MacroEvalError> {
    let (root, _, diagnostics) = MacroSyntaxParser::new(raw).parse();
    if let Some(first) = diagnostics.into_iter().next() {
        return Err(MacroEvalError::Diagnostic {
            kind: first.kind,
            arguments: first.arguments,
        });
    }

    let binder_diagnostics = MacroBinder::new().bind(&root);
    if let Some(first) = binder_diagnostics.into_iter().next() {
        return Err(MacroEvalError::Diagnostic {
            kind: first.kind,
            arguments: first.arguments,
        });
    }

    let Some(sequence) = root else {
        return Err(MacroEvalError::ResultIsEmpty);
    };

    let mut output = String::new();
    evaluate_sequence(&sequence, context, &mut output)?;

    if output.trim().is_empty() {
        return Err(MacroEvalError::ResultIsEmpty);
    }

    Ok(output)
}

fn evaluate_sequence(
    sequence: &Sequence,
    context: &MacroContext,
    output: &mut String,
) -> Result<(), MacroEvalError> {
    for node in &sequence.nodes {
        evaluate_node(node, context, output)?;
    }
    Ok(())
}

fn evaluate_node(
    node: &SingleNode,
    context: &MacroContext,
    output: &mut String,
) -> Result<(), MacroEvalError> {
    match node {
        SingleNode::PlainText(pt) => {
            output.push_str(&pt.text);
            Ok(())
        }
        SingleNode::Macro(m) => evaluate_macro(m, context, output),
    }
}

fn evaluate_macro(
    m: &MacroNode,
    context: &MacroContext,
    output: &mut String,
) -> Result<(), MacroEvalError> {
    let name = &m.name.text;
    let formatter = m.formatter.as_ref().map(|f| f.text.as_str());

    if is_transducer(name) {
        let (val, include_token) = substitute_transducer(name, formatter, context)?;
        let normalized = normalize_path_segment_in_macro(&val, include_token);
        output.push_str(&normalized);
        Ok(())
    } else if is_predicate(name) {
        let matched = evaluate_predicate(name, context);
        if let Some(branches) = &m.branches {
            if matched {
                if let Some(when_true) = &branches.when_true {
                    evaluate_sequence(when_true, context, output)?;
                }
            } else if let Some(when_false) = &branches.when_false {
                evaluate_sequence(when_false, context, output)?;
            }
        }
        Ok(())
    } else {
        Err(MacroEvalError::ReductionNotCompleted {
            names: vec![name.clone()],
        })
    }
}

fn substitute_transducer(
    name: &str,
    formatter: Option<&str>,
    context: &MacroContext,
) -> Result<(String, bool), MacroEvalError> {
    match name {
        "id" => Ok((format_string(&context.artwork_id, formatter), false)),
        "artist_id" => {
            let joined = context.author_ids.join(",");
            Ok((format_string(&joined, formatter), false))
        }
        "artist_name" => {
            let joined = context.author_names.join(",");
            Ok((format_string(&joined, formatter), false))
        }
        "work_title" | "title" => Ok((format_string(&context.title, formatter), false)),
        "work_publish_time" | "publish_time" => {
            let formatted = format_datetime(&context.create_date, formatter);
            Ok((formatted, false))
        }
        "ext" => {
            let token = create_token("ext", formatter);
            Ok((token, true))
        }
        "pic_set_index" => {
            let is_set = context.image_type() == MacroImageType::ImageSet;
            let token = if is_set {
                create_token("pic_set_index", formatter)
            } else {
                String::new()
            };
            Ok((token, is_set))
        }
        "group_id" => {
            let formatted = context
                .work_subscription_id
                .map(|id| format_integer(id as i64, formatter))
                .unwrap_or_default();
            Ok((formatted, false))
        }
        "series_id" => {
            let id = context
                .series_id
                .as_deref()
                .and_then(|s| s.parse::<i64>().ok())
                .unwrap_or(0);
            Ok((format_integer(id, formatter), false))
        }
        "series_title" => {
            let title = context.series_title.as_deref().unwrap_or("");
            Ok((format_string(title, formatter), false))
        }
        _ => Err(MacroEvalError::ReductionNotCompleted {
            names: vec![name.to_string()],
        }),
    }
}

fn evaluate_predicate(name: &str, context: &MacroContext) -> bool {
    match name {
        "is_group" => context.work_subscription_id.is_some(),
        "is_bookmark_group" => context.work_subscription_type.as_deref() == Some("Bookmarks"),
        "is_post_group" => context.work_subscription_type.as_deref() == Some("Posts"),
        "is_series_group" => context.work_subscription_type.as_deref() == Some("Series"),
        "is_series" => context.has_series,
        "is_pic_set" => context.image_type() == MacroImageType::ImageSet,
        "is_pic_one" => context.image_type() == MacroImageType::SingleImage,
        "is_pic_gif" => context.image_type() == MacroImageType::SingleAnimatedImage,
        "is_r18" => context.is_r18 || context.is_r18g,
        "is_r18g" => context.is_r18g,
        "is_ai" => context.is_ai,
        "is_novel" => context.is_novel,
        _ => false,
    }
}

fn format_string(s: &str, formatter: Option<&str>) -> String {
    match formatter {
        Some("u") => s.to_uppercase(),
        Some("l") => s.to_lowercase(),
        _ => s.to_string(),
    }
}

fn format_integer(value: i64, formatter: Option<&str>) -> String {
    match formatter {
        Some(f) if f.chars().all(|c| c == '0') => {
            let width = f.len();
            format!("{:0width$}", value, width = width)
        }
        _ => value.to_string(),
    }
}

fn format_datetime(date_str: &str, formatter: Option<&str>) -> String {
    let fmt = formatter.unwrap_or("yyyy-MM-dd");
    let parsed = DateTime::parse_from_rfc3339(date_str)
        .map(|dt| dt.with_timezone(&Utc))
        .or_else(|_| {
            DateTime::parse_from_str(date_str, "%Y-%m-%d %H:%M:%S").map(|dt| dt.with_timezone(&Utc))
        })
        .or_else(|_| {
            DateTime::parse_from_str(date_str, "%Y-%m-%d").map(|dt| dt.with_timezone(&Utc))
        });

    if let Ok(dt) = parsed {
        let mut result = String::new();
        let chars: Vec<char> = fmt.chars().collect();
        let mut i = 0;
        while i < chars.len() {
            if i + 4 <= chars.len() && &chars[i..i + 4] == ['y', 'y', 'y', 'y'] {
                result.push_str(&dt.format("%Y").to_string());
                i += 4;
            } else if i + 2 <= chars.len() && &chars[i..i + 2] == ['y', 'y'] {
                result.push_str(&dt.format("%y").to_string());
                i += 2;
            } else if i + 2 <= chars.len() && &chars[i..i + 2] == ['M', 'M'] {
                result.push_str(&dt.format("%m").to_string());
                i += 2;
            } else if chars[i] == 'M' {
                result.push_str(&dt.format("%-m").to_string());
                i += 1;
            } else if i + 2 <= chars.len() && &chars[i..i + 2] == ['d', 'd'] {
                result.push_str(&dt.format("%d").to_string());
                i += 2;
            } else if chars[i] == 'd' {
                result.push_str(&dt.format("%-d").to_string());
                i += 1;
            } else if i + 2 <= chars.len() && &chars[i..i + 2] == ['H', 'H'] {
                result.push_str(&dt.format("%H").to_string());
                i += 2;
            } else if chars[i] == 'H' {
                result.push_str(&dt.format("%-H").to_string());
                i += 1;
            } else if i + 2 <= chars.len() && &chars[i..i + 2] == ['m', 'm'] {
                result.push_str(&dt.format("%M").to_string());
                i += 2;
            } else if chars[i] == 'm' {
                result.push_str(&dt.format("%-M").to_string());
                i += 1;
            } else if i + 2 <= chars.len() && &chars[i..i + 2] == ['s', 's'] {
                result.push_str(&dt.format("%S").to_string());
                i += 2;
            } else if chars[i] == 's' {
                result.push_str(&dt.format("%-S").to_string());
                i += 1;
            } else {
                result.push(chars[i]);
                i += 1;
            }
        }
        result
    } else {
        date_str.to_string()
    }
}

fn create_token(name: &str, formatter: Option<&str>) -> String {
    match formatter {
        Some(f) => format!("<{name}:{f}>"),
        None => format!("<{name}>"),
    }
}

pub fn normalize_path_segment_in_macro(path: &str, include_token: bool) -> String {
    let mut result = String::with_capacity(path.len());
    for c in path.chars() {
        if include_token {
            // Strip \/*?"| and control chars (<> and : are allowed for tokens)
            if !matches!(c, '\\' | '/' | '*' | '?' | '\"' | '|') && (c as u32) >= 32 {
                result.push(c);
            }
        } else {
            // Strip <>\/*?"|: and control chars
            if !matches!(
                c,
                '<' | '>' | ':' | '\\' | '/' | '*' | '?' | '\"' | '|'
            ) && (c as u32) >= 32
            {
                result.push(c);
            }
        }
    }
    result.trim_end_matches('.').to_string()
}
