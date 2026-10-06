uniffi::setup_scaffolding!();

pub mod ast;
pub mod completions;
pub mod diagnostics;
pub mod eval;
pub mod language;
pub mod parser;
pub mod syntax;
pub mod text;
pub mod values;

pub use ast::*;
pub use completions::*;
pub use diagnostics::*;
pub use eval::*;
pub use language::*;
pub use parser::*;
pub use syntax::*;
pub use text::*;
pub use values::*;

#[cfg(test)]
mod tests {
    use super::*;

    fn create_test_language() -> FilterLanguage {
        let syntaxes = vec![
            FilterSyntaxDefinition::new(
                "Title",
                FilterValueKind::Text,
                Some("keyword".to_string()),
                vec![
                    FilterSyntaxPattern::default_pattern(
                        Some("keyword".to_string()),
                        Some("按标题搜索".to_string()),
                    ),
                    FilterSyntaxPattern::keyword(
                        "title",
                        ":",
                        Some("keyword".to_string()),
                        Some("按标题搜索".to_string()),
                    ),
                ],
            ),
            FilterSyntaxDefinition::new(
                "Author",
                FilterValueKind::Text,
                Some("artist".to_string()),
                vec![
                    FilterSyntaxPattern::prefix_only(
                        "@",
                        Some("artist".to_string()),
                        Some("按画师搜索".to_string()),
                    ),
                    FilterSyntaxPattern::keyword(
                        "a",
                        ":",
                        Some("artist".to_string()),
                        Some("按画师搜索".to_string()),
                    ),
                    FilterSyntaxPattern::keyword(
                        "artist",
                        ":",
                        Some("artist".to_string()),
                        Some("按画师搜索".to_string()),
                    ),
                    FilterSyntaxPattern::keyword(
                        "author",
                        ":",
                        Some("artist".to_string()),
                        Some("按画师搜索".to_string()),
                    ),
                ],
            ),
            FilterSyntaxDefinition::new(
                "Tag",
                FilterValueKind::Text,
                Some("tag".to_string()),
                vec![
                    FilterSyntaxPattern::prefix_only(
                        "#",
                        Some("tag".to_string()),
                        Some("按标签搜索".to_string()),
                    ),
                    FilterSyntaxPattern::keyword(
                        "t",
                        ":",
                        Some("tag".to_string()),
                        Some("按标签搜索".to_string()),
                    ),
                    FilterSyntaxPattern::keyword(
                        "tag",
                        ":",
                        Some("tag".to_string()),
                        Some("按标签搜索".to_string()),
                    ),
                ],
            ),
            FilterSyntaxDefinition::new(
                "Bookmark",
                FilterValueKind::LongRange,
                Some("100-200".to_string()),
                vec![
                    FilterSyntaxPattern::keyword(
                        "l",
                        ":",
                        Some("100-200".to_string()),
                        Some("收藏数范围".to_string()),
                    ),
                    FilterSyntaxPattern::keyword(
                        "like",
                        ":",
                        Some("100-200".to_string()),
                        Some("收藏数范围".to_string()),
                    ),
                ],
            ),
            FilterSyntaxDefinition::new(
                "Ai",
                FilterValueKind::Flag,
                None,
                vec![
                    FilterSyntaxPattern::new(
                        "+",
                        vec!["ai".to_string()],
                        "",
                        Some("false".to_string()),
                        None,
                        Some("仅显示 AI".to_string()),
                    ),
                    FilterSyntaxPattern::new(
                        "-",
                        vec!["ai".to_string()],
                        "",
                        Some("true".to_string()),
                        None,
                        Some("排除 AI".to_string()),
                    ),
                ],
            ),
            FilterSyntaxDefinition::new(
                "R18",
                FilterValueKind::Flag,
                None,
                vec![
                    FilterSyntaxPattern::new(
                        "+",
                        vec!["r18".to_string()],
                        "",
                        Some("false".to_string()),
                        None,
                        Some("仅显示 R18".to_string()),
                    ),
                    FilterSyntaxPattern::new(
                        "-",
                        vec!["r18".to_string()],
                        "",
                        Some("true".to_string()),
                        None,
                        Some("排除 R18".to_string()),
                    ),
                ],
            ),
            FilterSyntaxDefinition::new(
                "R18G",
                FilterValueKind::Flag,
                None,
                vec![
                    FilterSyntaxPattern::new(
                        "+",
                        vec!["r18g".to_string()],
                        "",
                        Some("false".to_string()),
                        None,
                        Some("仅显示 R18G".to_string()),
                    ),
                    FilterSyntaxPattern::new(
                        "-",
                        vec!["r18g".to_string()],
                        "",
                        Some("true".to_string()),
                        None,
                        Some("排除 R18G".to_string()),
                    ),
                ],
            ),
            FilterSyntaxDefinition::new(
                "Ratio",
                FilterValueKind::DoubleRange,
                Some("1.5..2.0".to_string()),
                vec![
                    FilterSyntaxPattern::keyword(
                        "ratio",
                        ":",
                        Some("1.5..2.0".to_string()),
                        Some("长宽比范围".to_string()),
                    ),
                    FilterSyntaxPattern::keyword(
                        "r",
                        ":",
                        Some("1.5..2.0".to_string()),
                        Some("长宽比范围".to_string()),
                    ),
                ],
            ),
            FilterSyntaxDefinition::new(
                "StartDate",
                FilterValueKind::Date,
                Some("2024-01-01".to_string()),
                vec![
                    FilterSyntaxPattern::keyword(
                        "start",
                        ":",
                        Some("2024-01-01".to_string()),
                        Some("起始日期".to_string()),
                    ),
                    FilterSyntaxPattern::keyword(
                        "s",
                        ":",
                        Some("01-01".to_string()),
                        Some("起始日期".to_string()),
                    ),
                ],
            ),
        ];

        FilterLanguage::new(syntaxes, None, None, None)
    }

    #[test]
    fn test_simple_text_parse() {
        let lang = create_test_language();
        let result = lang.analyze("Blue Hour", -1, None);
        assert!(result.is_success);
        assert!(result.query.is_some());
        let q = result.query.unwrap();
        assert_eq!(q.root.children.len(), 2);
    }

    #[test]
    fn test_prefix_syntax_parse() {
        let lang = create_test_language();
        let result = lang.analyze("#sky @Alice", -1, None);
        assert!(result.is_success);
        let q = result.query.unwrap();
        assert_eq!(q.root.children.len(), 2);
    }

    #[test]
    fn test_group_and_negation() {
        let lang = create_test_language();
        let result = lang.analyze("!(or #sky #water)", -1, None);
        assert!(result.is_success);
        let q = result.query.unwrap();
        assert_eq!(q.root.children.len(), 1);
        if let FilterNode::Group(g) = &q.root.children[0] {
            assert!(g.is_negated);
            assert_eq!(g.operator, FilterLogicalOperator::Or);
            assert_eq!(g.children.len(), 2);
        } else {
            panic!("Expected group node");
        }
    }

    #[test]
    fn test_artwork_matching() {
        let lang = create_test_language();
        let result = lang.analyze("#sky +ai", -1, None);
        assert!(result.is_success);
        let q = result.query.unwrap();

        let artwork = ArtworkMetadata {
            id: "1".to_string(),
            title: "Blue Sky".to_string(),
            author_name: "Alice".to_string(),
            author_account: "alice".to_string(),
            tags: vec![ArtworkTag::new("sky", Some("天空".to_string()))],
            total_bookmarks: 150,
            create_date_timestamp: 1700000000,
            width: 1920,
            height: 1080,
            x_restrict: 0,
            ai_type: 1,
            illustration_type: 0,
        };

        assert!(matches_artwork(&q, &artwork));

        let non_ai = ArtworkMetadata {
            ai_type: 0,
            ..artwork.clone()
        };
        assert!(!matches_artwork(&q, &non_ai));

        // Test R18 matching semantics: +r18 matches both R18 and R18G
        let q_r18 = lang.analyze("+r18", -1, None).query.unwrap();
        let r18_art = ArtworkMetadata {
            x_restrict: 1,
            ..artwork.clone()
        };
        let r18g_art = ArtworkMetadata {
            x_restrict: 2,
            ..artwork.clone()
        };
        assert!(matches_artwork(&q_r18, &r18_art));
        assert!(matches_artwork(&q_r18, &r18g_art));
        assert!(!matches_artwork(&q_r18, &artwork));

        // Test -r18 excludes both R18 and R18G
        let q_no_r18 = lang.analyze("-r18", -1, None).query.unwrap();
        assert!(matches_artwork(&q_no_r18, &artwork));
        assert!(!matches_artwork(&q_no_r18, &r18_art));
        assert!(!matches_artwork(&q_no_r18, &r18g_art));

        // Test +r18g matches only R18G
        let q_r18g = lang.analyze("+r18g", -1, None).query.unwrap();
        assert!(!matches_artwork(&q_r18g, &r18_art));
        assert!(matches_artwork(&q_r18g, &r18g_art));

        // Test ratio filter does not exclude novels (height == 0)
        let q_ratio = lang.analyze("ratio:1.5-2.0", -1, None).query.unwrap();
        let novel_art = ArtworkMetadata {
            width: 0,
            height: 0,
            ..artwork.clone()
        };
        assert!(matches_artwork(&q_ratio, &novel_art));
    }

    #[test]
    fn test_author_search_and_case_insensitive() {
        let lang = create_test_language();
        let res = lang.analyze("author:Alice", -1, None);
        assert!(res.is_success);
        let q = res.query.unwrap();

        let artwork = ArtworkMetadata {
            id: "1".to_string(),
            title: "Art".to_string(),
            author_name: "alice".to_string(),
            author_account: "alice_pixiv".to_string(),
            tags: vec![],
            total_bookmarks: 10,
            create_date_timestamp: 1700000000,
            width: 100,
            height: 100,
            x_restrict: 0,
            ai_type: 0,
            illustration_type: 0,
        };
        assert!(matches_artwork(&q, &artwork));

        let res_exact = lang.analyze("author:ALICE$", -1, None);
        assert!(res_exact.is_success);
        let q_exact = res_exact.query.unwrap();
        assert!(matches_artwork(&q_exact, &artwork));

        let res_ws = lang.analyze("@ Alice ", -1, None);
        assert!(res_ws.is_success);
        let q_ws = res_ws.query.unwrap();
        assert!(matches_artwork(&q_ws, &artwork));
    }

    #[test]
    fn test_leap_day_validation() {
        use chrono::Datelike;
        let lang = create_test_language();
        let current_year = chrono::Utc::now().year();
        let is_leap_year = chrono::NaiveDate::from_ymd_opt(current_year, 2, 29).is_some();
        let res = lang.analyze("s:2-29", -1, None);
        if is_leap_year {
            assert!(res.is_success);
        } else {
            assert!(!res.is_success);
            assert!(res.diagnostics.iter().any(|d| d.kind == FilterDiagnosticKind::InvalidDate));
        }

        // Leap year explicitly specified (2024-2-29) should always succeed
        let res_leap = lang.analyze("s:2024-2-29", -1, None);
        assert!(res_leap.is_success);

        // Non-leap year explicitly specified (2025-2-29) should always fail
        let res_non_leap = lang.analyze("s:2025-2-29", -1, None);
        assert!(!res_non_leap.is_success);
        assert!(res_non_leap.diagnostics.iter().any(|d| d.kind == FilterDiagnosticKind::InvalidDate));
    }
}
