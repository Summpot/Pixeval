use crate::ArtworkMetadata;
use pixeval_filters::engine::FilterSession;
use std::sync::LazyLock;

static FILTER_SESSION: LazyLock<std::sync::Arc<FilterSession>> = LazyLock::new(FilterSession::new);

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
#[boltffi::data]
pub struct FilterCompletionDto {
    pub display_text: String,
    pub insert_text: String,
    pub completed_text: String,
    pub description: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
#[boltffi::data]
pub struct FilterDiagnosticDto {
    pub message: String,
    pub severity: String,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
#[boltffi::data]
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
    artworks: Vec<ArtworkMetadata>,
) -> Result<Vec<bool>, String> {
    if query.trim().is_empty() {
        return Ok(vec![true; artworks.len()]);
    }
    let analysis = FILTER_SESSION.analyze(query, -1);
    if !analysis.is_success {
        return Err("Invalid filter expression".into());
    }
    let query = analysis.query_handle.ok_or("Missing filter expression")?;
    Ok(pixeval_filters::eval::filter_artworks(&query, &artworks))
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

pub mod ffi {

    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn analyze_filter_query(
        query: String,
        caret_pos: i32,
    ) -> crate::app::api::filters::FilterAnalysisResultDto {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::analyze_filter_query(query, caret_pos))
                .await
                .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn filter_artworks_by_query(
        query: String,
        artworks: Vec<crate::ArtworkMetadata>,
    ) -> Result<Vec<bool>, String> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || super::filter_artworks_by_query(query, artworks))
                .await
                .expect("native application worker panicked")
        })
        .await
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn query_planning_preserves_short_forms_and_boolean_semantics() {
        for text in ["#cat", "tag:cat"] {
            let plan=plan_search_query(text).unwrap();
            assert_eq!(plan.remote_word,"cat");
            assert_eq!(plan.filter_query.as_deref(),Some(text));
        }
        for text in ["@Alice", "+r18", "-ai", "!(or #cat #dog)", "(or #cat #dog)"] {
            let plan=plan_search_query(text).unwrap();
            assert!(plan.filter_query.is_some(),"{text}");
            assert!(plan.remote_word.is_empty(),"{text}");
        }
        let plain=plan_search_query("cat dog").unwrap();
        assert_eq!(plain.remote_word,"cat dog");
        assert!(plain.filter_query.is_none());
        assert!(plan_search_query("(tag:cat").is_err());
    }

    #[test]
    fn core_metadata_filter_returns_a_position_preserving_mask() {
        let first=ArtworkMetadata{id:"1".into(),title:"Cat portrait".into(),author_name:"Alice".into(),author_account:"alice".into(),tags:vec![crate::ArtworkTag{name:"cat".into(),translated_name:Some("猫".into())}],total_bookmarks:42,create_date_timestamp:1792540800,width:640,height:480,x_restrict:0,ai_type:0,illustration_type:0};
        let second=ArtworkMetadata{id:"2".into(),x_restrict:1,ai_type:1,..first.clone()};
        let items=vec![first,second];
        for text in ["+r18", "+ai", "@Alice +ai", "#猫 +ai"] {
            assert_eq!(filter_artworks_by_query(text.into(),items.clone()).unwrap(),vec![false,true],"{text}");
        }
        assert_eq!(filter_artworks_by_query("".into(),items.clone()).unwrap(),vec![true,true]);
        assert!(filter_artworks_by_query("(tag:cat".into(),items).is_err());
    }
}
