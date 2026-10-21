use crate::api::feed::IllustSummary;
use pixeval_filters::engine::FilterSession;
use pixeval_filters::eval::{ArtworkMetadata, ArtworkTag};
use std::sync::LazyLock;

static FILTER_SESSION: LazyLock<std::sync::Arc<FilterSession>> = LazyLock::new(FilterSession::new);

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct FilterCompletionDto {
    pub display_text: String,
    pub insert_text: String,
    pub completed_text: String,
    pub description: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct FilterDiagnosticDto {
    pub message: String,
    pub severity: String,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct FilterAnalysisResultDto {
    pub is_valid: bool,
    pub completions: Vec<FilterCompletionDto>,
    pub diagnostics: Vec<FilterDiagnosticDto>,
}

pub fn analyze_filter_query(query: String, caret_pos: i32) -> FilterAnalysisResultDto {
    let result = FILTER_SESSION.analyze(query, caret_pos);

    let completions = result
        .completions
        .into_iter()
        .map(|c| FilterCompletionDto {
            display_text: c.display_text,
            insert_text: c.insert_text,
            completed_text: c.completed_text,
            description: c.description,
        })
        .collect();

    let diagnostics = result
        .diagnostics
        .into_iter()
        .map(|d| FilterDiagnosticDto {
            message: format!("{:?}", d.kind),
            severity: "Error".to_string(),
        })
        .collect();

    FilterAnalysisResultDto {
        is_valid: result.is_success,
        completions,
        diagnostics,
    }
}

pub fn filter_artworks_by_query(
    query: String,
    artworks: Vec<IllustSummary>,
) -> Result<Vec<IllustSummary>, String> {
    if query.trim().is_empty() {
        return Ok(artworks);
    }

    let analysis = FILTER_SESSION.analyze(query, -1);
    if !analysis.is_success {
        return Err("Invalid filter expression".to_string());
    }
    let query = analysis.query_handle.ok_or("Missing filter expression")?;
    let meta: Vec<ArtworkMetadata> = artworks
        .iter()
        .map(|a| ArtworkMetadata {
            id: a.id.to_string(),
            title: a.title.clone(),
            author_name: a.user_name.clone(),
            author_account: a.user_account.clone(),
            tags: a
                .tags
                .iter()
                .enumerate()
                .map(|(index, t)| ArtworkTag {
                    name: t.clone(),
                    translated_name: a
                        .filter_metadata
                        .as_ref()
                        .and_then(|m| m.translated_tags.get(index))
                        .cloned()
                        .flatten(),
                })
                .collect(),
            total_bookmarks: a.total_bookmarks,
            create_date_timestamp: a
                .filter_metadata
                .as_ref()
                .map_or(0, |m| m.create_date_timestamp),
            width: a.width,
            height: a.height,
            x_restrict: a
                .filter_metadata
                .as_ref()
                .map_or(if a.is_r18 { 1 } else { 0 }, |m| m.x_restrict),
            ai_type: a.filter_metadata.as_ref().map_or(0, |m| m.ai_type),
            illustration_type: a
                .filter_metadata
                .as_ref()
                .map_or(if a.is_ugoira { 2 } else { 0 }, |m| m.illustration_type),
        })
        .collect();

    let matches = pixeval_filters::eval::filter_artworks(&query, &meta);

    Ok(artworks
        .into_iter()
        .zip(matches)
        .filter_map(|(item, is_match)| if is_match { Some(item) } else { None })
        .collect())
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub(crate) struct SearchQueryPlan {
    pub remote_word: String,
    pub remote_target: Option<String>,
    pub filter_query: Option<String>,
}

/// Only a positive conjunct can narrow the candidate set safely. OR/negation
/// without such a conjunct filters the recommendation feed instead.
pub(crate) fn plan_search_query(text: &str) -> Result<SearchQueryPlan, String> {
    use pixeval_filters::ast::{FilterAstNode, FilterLogicalOperator, FilterNode};
    use pixeval_filters::values::FilterValue;
    let analysis = FILTER_SESSION.analyze(text.to_string(), -1);
    if !analysis.is_success {
        return Err("Invalid filter expression".to_string());
    }
    fn explicit(node: &FilterAstNode) -> bool {
        node.is_negated
            || (node.is_group && node.children.iter().any(explicit))
            || (!node.is_group
                && (!node.matched_prefix.is_empty()
                    || !node.matched_alias.is_empty()
                    || !node.matched_suffix.is_empty()))
    }
    let is_expression =
        analysis.ast_root.as_ref().is_some_and(explicit) || text.trim_start().starts_with('(');
    if !is_expression {
        return Ok(SearchQueryPlan {
            remote_word: text.trim().to_string(),
            remote_target: None,
            filter_query: None,
        });
    }
    fn seed(node: &FilterNode) -> Option<(String, String)> {
        match node {
            FilterNode::Group(g) if !g.is_negated && g.operator == FilterLogicalOperator::And => {
                g.children.iter().find_map(seed)
            }
            FilterNode::Predicate(p) if !p.is_negated => {
                if let FilterValue::Text(value) = &p.value {
                    let target = match p.syntax_key.as_str() {
                        "Tag" => "partial_match_for_tags",
                        "Title" => "title_and_caption",
                        _ => return None,
                    };
                    return Some((value.content.clone(), target.to_string()));
                }
                None
            }
            _ => None,
        }
    }
    let query = analysis.query_handle.ok_or("Missing filter query")?;
    let candidate = if query.root.is_negated || query.root.operator != FilterLogicalOperator::And {
        None
    } else {
        query.root.children.iter().find_map(seed)
    };
    let (remote_word, remote_target) =
        candidate.map_or((String::new(), None), |(w, t)| (w, Some(t)));
    Ok(SearchQueryPlan {
        remote_word,
        remote_target,
        filter_query: Some(text.to_string()),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use pixeval_mako::models::{Illustration, Tag};

    fn fixture(id: i64, r18: bool, ai: bool) -> IllustSummary {
        let mut work = Illustration::default();
        work.id = id;
        work.title = "Cat portrait".into();
        work.user.name = "Alice".into();
        work.create_date = "2026-10-21T12:00:00+09:00".into();
        work.illust_ai_type = if ai { 2 } else { 1 };
        work.x_restrict = if r18 {
            pixeval_mako::catalog::XRestrict::R18
        } else {
            pixeval_mako::catalog::XRestrict::Ordinary
        };
        work.tags = vec![Tag {
            name: "cat".into(),
            translated_name: Some("猫".into()),
        }];
        work.into()
    }

    #[test]
    fn query_planning_covers_short_forms_without_forwarding_filter_syntax() {
        for text in ["#cat", "tag:cat"] {
            let plan = plan_search_query(text).unwrap();
            assert_eq!(plan.remote_word, "cat");
            assert_eq!(plan.filter_query.as_deref(), Some(text));
        }
        for text in ["@Alice", "+r18", "-ai", "!(or #cat #dog)", "(or #cat #dog)"] {
            let plan = plan_search_query(text).unwrap();
            assert!(plan.filter_query.is_some(), "{text}");
            assert!(plan.remote_word.is_empty(), "{text}");
        }
        let plain = plan_search_query("cat dog").unwrap();
        assert_eq!(plain.remote_word, "cat dog");
        assert!(plain.filter_query.is_none());
        assert!(plan_search_query("(tag:cat").is_err());
    }

    #[test]
    fn filters_use_real_ai_rating_date_and_translated_tag_metadata() {
        let items = vec![fixture(1, false, false), fixture(2, true, true)];
        for query in ["+r18", "+ai", "@Alice +ai", "#猫 +ai"] {
            let filtered = filter_artworks_by_query(query.into(), items.clone()).unwrap();
            assert_eq!(
                filtered.iter().map(|i| i.id).collect::<Vec<_>>(),
                vec![2],
                "{query}"
            );
        }
        assert_eq!(
            items[0]
                .filter_metadata
                .as_ref()
                .unwrap()
                .create_date_timestamp,
            chrono::DateTime::parse_from_rfc3339("2026-10-21T12:00:00+09:00")
                .unwrap()
                .timestamp()
        );
        assert!(filter_artworks_by_query("(tag:cat".into(), items).is_err());
    }
}
