use chrono::{DateTime, Datelike, Utc};

use crate::ast::{FilterLogicalOperator, FilterNode, FilterPredicateNode, FilterQuery};
use crate::values::FilterValue;

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct ArtworkTag {
    pub name: String,
    pub translated_name: Option<String>,
}

impl ArtworkTag {
    pub fn new(name: impl Into<String>, translated_name: Option<String>) -> Self {
        Self {
            name: name.into(),
            translated_name,
        }
    }
}

#[derive(Debug, Clone, PartialEq, uniffi::Record)]
pub struct ArtworkMetadata {
    pub id: String,
    pub title: String,
    pub author_name: String,
    pub author_account: String,
    pub tags: Vec<ArtworkTag>,
    pub total_bookmarks: i64,
    pub create_date_timestamp: i64, // Unix timestamp in seconds
    pub width: i32,
    pub height: i32,
    pub x_restrict: i32,        // 0 = Safe, 1 = R18, 2 = R18G
    pub ai_type: i32,           // 0 = None, 1 = AiGenerated
    pub illustration_type: i32, // 0 = Illust, 1 = Manga, 2 = Ugoira
}

pub fn matches_artwork(query: &FilterQuery, artwork: &ArtworkMetadata) -> bool {
    eval_group(&query.root, artwork)
}

pub fn filter_artworks(query: &FilterQuery, artworks: &[ArtworkMetadata]) -> Vec<bool> {
    artworks.iter().map(|a| matches_artwork(query, a)).collect()
}

fn eval_node(node: &FilterNode, artwork: &ArtworkMetadata) -> bool {
    match node {
        FilterNode::Group(g) => eval_group(g, artwork),
        FilterNode::Predicate(p) => eval_predicate(p, artwork) ^ p.is_negated,
    }
}

fn eval_group(group: &crate::ast::FilterGroupNode, artwork: &ArtworkMetadata) -> bool {
    if group.children.is_empty() {
        return !group.is_negated;
    }
    let matched = match group.operator {
        FilterLogicalOperator::And => group.children.iter().all(|c| eval_node(c, artwork)),
        FilterLogicalOperator::Or => group.children.iter().any(|c| eval_node(c, artwork)),
    };
    matched ^ group.is_negated
}

fn eval_predicate(predicate: &FilterPredicateNode, artwork: &ArtworkMetadata) -> bool {
    let key = predicate.syntax_key.as_str();

    match &predicate.value {
        FilterValue::Text(tv) => match key {
            "Title" => tv.matches(&artwork.title),
            "Author" => tv.matches(&artwork.author_name) || tv.matches(&artwork.author_account),
            "Tag" => artwork.tags.iter().any(|t| {
                tv.matches(&t.name)
                    || t.translated_name
                        .as_deref()
                        .is_some_and(|tn| tv.matches(tn))
            }),
            _ => true,
        },
        FilterValue::Long(lv, _) => match key {
            "Bookmark" => artwork.total_bookmarks == *lv,
            _ => true,
        },
        FilterValue::Double(dv, _) => match key {
            "Ratio" => {
                if artwork.height > 0 {
                    let ratio = artwork.width as f64 / artwork.height as f64;
                    (ratio - *dv).abs() < f64::EPSILON
                } else {
                    true
                }
            }
            _ => true,
        },
        FilterValue::LongRange(range, _) => match key {
            "Bookmark" => range.contains(artwork.total_bookmarks),
            _ => true,
        },
        FilterValue::DoubleRange(range, _) => match key {
            "Ratio" => {
                if artwork.height > 0 {
                    let ratio = artwork.width as f64 / artwork.height as f64;
                    range.contains(ratio)
                } else {
                    true
                }
            }
            _ => true,
        },
        FilterValue::Date(dl, _) => {
            let artwork_date = match DateTime::from_timestamp(artwork.create_date_timestamp, 0) {
                Some(dt) => dt.date_naive(),
                None => return false,
            };

            let fallback_year = Utc::now().year();
            let target_date = match dl.to_naive_date(fallback_year) {
                Some(d) => d,
                None => return false,
            };

            match key {
                "StartDate" => artwork_date >= target_date,
                "EndDate" => artwork_date <= target_date,
                _ => true,
            }
        }
        FilterValue::Flag(expected, _) => match key {
            "Ai" => (artwork.ai_type == 1) == *expected,
            "R18" => (artwork.x_restrict >= 1) == *expected,
            "R18G" => (artwork.x_restrict == 2) == *expected,
            "Gif" => (artwork.illustration_type == 2) == *expected,
            _ => true,
        },
        FilterValue::None(_) => true,
    }
}
