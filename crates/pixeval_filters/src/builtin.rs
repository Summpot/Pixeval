use std::collections::HashMap;

use crate::completions::{
    FilterCompletionDefinition, FilterFullCompletionDefinition, FilterLocalization,
};
use crate::syntax::{FilterSyntaxDefinition, FilterSyntaxPattern};
use crate::values::FilterValueKind;

pub fn build_artwork_syntaxes(
    localization: Option<&FilterLocalization>,
) -> Vec<FilterSyntaxDefinition> {
    let loc = localization;
    vec![
        FilterSyntaxDefinition::new(
            "Title",
            FilterValueKind::Text,
            Some("keyword".to_string()),
            vec![
                FilterSyntaxPattern::default_pattern(
                    Some("keyword".to_string()),
                    loc.and_then(|l| l.title_description.clone())
                        .or_else(|| Some("按标题搜索".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "title",
                    ":",
                    Some("keyword".to_string()),
                    loc.and_then(|l| l.title_description.clone())
                        .or_else(|| Some("按标题搜索".to_string())),
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
                    loc.and_then(|l| l.author_description.clone())
                        .or_else(|| Some("按画师搜索".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "a",
                    ":",
                    Some("artist".to_string()),
                    loc.and_then(|l| l.author_description.clone())
                        .or_else(|| Some("按画师搜索".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "artist",
                    ":",
                    Some("artist".to_string()),
                    loc.and_then(|l| l.author_description.clone())
                        .or_else(|| Some("按画师搜索".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "author",
                    ":",
                    Some("artist".to_string()),
                    loc.and_then(|l| l.author_description.clone())
                        .or_else(|| Some("按画师搜索".to_string())),
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
                    loc.and_then(|l| l.tag_description.clone())
                        .or_else(|| Some("按标签搜索".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "t",
                    ":",
                    Some("tag".to_string()),
                    loc.and_then(|l| l.tag_description.clone())
                        .or_else(|| Some("按标签搜索".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "tag",
                    ":",
                    Some("tag".to_string()),
                    loc.and_then(|l| l.tag_description.clone())
                        .or_else(|| Some("按标签搜索".to_string())),
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
                    loc.and_then(|l| l.bookmark_description.clone())
                        .or_else(|| Some("收藏数范围".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "like",
                    ":",
                    Some("100-200".to_string()),
                    loc.and_then(|l| l.bookmark_description.clone())
                        .or_else(|| Some("收藏数范围".to_string())),
                ),
            ],
        ),
        FilterSyntaxDefinition::new(
            "Ratio",
            FilterValueKind::DoubleRange,
            Some("1-2".to_string()),
            vec![
                FilterSyntaxPattern::keyword(
                    "r",
                    ":",
                    Some("1-2".to_string()),
                    loc.and_then(|l| l.ratio_description.clone())
                        .or_else(|| Some("长宽比范围".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "ratio",
                    ":",
                    Some("1-2".to_string()),
                    loc.and_then(|l| l.ratio_description.clone())
                        .or_else(|| Some("长宽比范围".to_string())),
                ),
            ],
        ),
        FilterSyntaxDefinition::new(
            "StartDate",
            FilterValueKind::Date,
            Some("2024-1-1".to_string()),
            vec![
                FilterSyntaxPattern::keyword(
                    "s",
                    ":",
                    Some("2024-1-1".to_string()),
                    loc.and_then(|l| l.start_date_description.clone())
                        .or_else(|| Some("起始日期".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "start",
                    ":",
                    Some("2024-1-1".to_string()),
                    loc.and_then(|l| l.start_date_description.clone())
                        .or_else(|| Some("起始日期".to_string())),
                ),
            ],
        ),
        FilterSyntaxDefinition::new(
            "EndDate",
            FilterValueKind::Date,
            Some("2024-1-1".to_string()),
            vec![
                FilterSyntaxPattern::keyword(
                    "e",
                    ":",
                    Some("2024-1-1".to_string()),
                    loc.and_then(|l| l.end_date_description.clone())
                        .or_else(|| Some("终止日期".to_string())),
                ),
                FilterSyntaxPattern::keyword(
                    "end",
                    ":",
                    Some("2024-1-1".to_string()),
                    loc.and_then(|l| l.end_date_description.clone())
                        .or_else(|| Some("终止日期".to_string())),
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
                    loc.and_then(|l| l.include_ai_description.clone())
                        .or_else(|| Some("仅显示 AI".to_string())),
                ),
                FilterSyntaxPattern::new(
                    "-",
                    vec!["ai".to_string()],
                    "",
                    Some("true".to_string()),
                    None,
                    loc.and_then(|l| l.exclude_ai_description.clone())
                        .or_else(|| Some("排除 AI".to_string())),
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
                    loc.and_then(|l| l.include_r18_description.clone())
                        .or_else(|| Some("仅显示 R18".to_string())),
                ),
                FilterSyntaxPattern::new(
                    "-",
                    vec!["r18".to_string()],
                    "",
                    Some("true".to_string()),
                    None,
                    loc.and_then(|l| l.exclude_r18_description.clone())
                        .or_else(|| Some("排除 R18".to_string())),
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
                    loc.and_then(|l| l.include_r18g_description.clone())
                        .or_else(|| Some("仅显示 R18G".to_string())),
                ),
                FilterSyntaxPattern::new(
                    "-",
                    vec!["r18g".to_string()],
                    "",
                    Some("true".to_string()),
                    None,
                    loc.and_then(|l| l.exclude_r18g_description.clone())
                        .or_else(|| Some("排除 R18G".to_string())),
                ),
            ],
        ),
        FilterSyntaxDefinition::new(
            "Gif",
            FilterValueKind::Flag,
            None,
            vec![
                FilterSyntaxPattern::new(
                    "+",
                    vec!["gif".to_string()],
                    "",
                    Some("false".to_string()),
                    None,
                    loc.and_then(|l| l.include_gif_description.clone())
                        .or_else(|| Some("仅显示动图".to_string())),
                ),
                FilterSyntaxPattern::new(
                    "-",
                    vec!["gif".to_string()],
                    "",
                    Some("true".to_string()),
                    None,
                    loc.and_then(|l| l.exclude_gif_description.clone())
                        .or_else(|| Some("排除动图".to_string())),
                ),
            ],
        ),
    ]
}

pub fn build_artwork_intrinsics(
    localization: Option<&FilterLocalization>,
) -> Vec<FilterCompletionDefinition> {
    let loc = localization;
    vec![
        FilterCompletionDefinition::new(
            "builtin.and",
            "and",
            "and ",
            loc.and_then(|l| l.and_description.clone())
                .or_else(|| Some("逻辑与分组".to_string())),
        ),
        FilterCompletionDefinition::new(
            "builtin.or",
            "or",
            "or ",
            loc.and_then(|l| l.or_description.clone())
                .or_else(|| Some("逻辑或分组".to_string())),
        ),
        FilterCompletionDefinition::new(
            "builtin.not",
            "!",
            "!",
            loc.and_then(|l| l.not_description.clone())
                .or_else(|| Some("逻辑非".to_string())),
        ),
    ]
}

pub fn build_artwork_full_completions(
    localization: Option<&FilterLocalization>,
) -> Vec<FilterFullCompletionDefinition> {
    let loc = localization;
    vec![
        FilterFullCompletionDefinition::new(
            "work.constraint.include",
            "+ai",
            "+",
            loc.and_then(|l| l.include_constraint_description.clone())
                .or_else(|| Some("包含约束".to_string())),
            vec!["+".to_string()],
        ),
        FilterFullCompletionDefinition::new(
            "work.constraint.exclude",
            "-ai",
            "-",
            loc.and_then(|l| l.exclude_constraint_description.clone())
                .or_else(|| Some("排除约束".to_string())),
            vec!["-".to_string()],
        ),
    ]
}

pub fn build_artwork_value_hints() -> HashMap<FilterValueKind, Vec<FilterCompletionDefinition>> {
    let mut map = HashMap::new();
    map.insert(
        FilterValueKind::Text,
        vec![
            FilterCompletionDefinition::new("hint.text.plain", "abc", "", Some("普通字符串".to_string())),
            FilterCompletionDefinition::new("hint.text.quoted", "\"ab# c\"", "", Some("带特殊符号的引号字符串".to_string())),
            FilterCompletionDefinition::new("hint.text.exact", "abc$", "", Some("精确匹配字符串".to_string())),
            FilterCompletionDefinition::new("hint.text.quoted-exact", "\"ab c$\"", "", Some("精确匹配引号字符串".to_string())),
        ],
    );
    map.insert(
        FilterValueKind::Long,
        vec![FilterCompletionDefinition::new("hint.long.plain", "12345", "", Some("整数值".to_string()))],
    );
    map.insert(
        FilterValueKind::Double,
        vec![
            FilterCompletionDefinition::new("hint.double.integer", "2", "", Some("整数形式小数值".to_string())),
            FilterCompletionDefinition::new("hint.double.decimal", "1.5", "", Some("小数形式小数值".to_string())),
            FilterCompletionDefinition::new("hint.double.fraction", "1/2", "", Some("分数形式小数值".to_string())),
        ],
    );
    map.insert(
        FilterValueKind::LongRange,
        vec![
            FilterCompletionDefinition::new("hint.long-range.lower", "2-", "", Some("下限范围 (>=2)".to_string())),
            FilterCompletionDefinition::new("hint.long-range.upper", "-3", "", Some("上限范围 (<=3)".to_string())),
            FilterCompletionDefinition::new("hint.long-range.closed", "2-3", "", Some("闭区间范围 (2~3)".to_string())),
        ],
    );
    map.insert(
        FilterValueKind::DoubleRange,
        vec![
            FilterCompletionDefinition::new("hint.double-range.lower", "2-", "", Some("下限范围 (>=2)".to_string())),
            FilterCompletionDefinition::new("hint.double-range.upper-decimal", "-1.5", "", Some("上限小数范围".to_string())),
            FilterCompletionDefinition::new("hint.double-range.upper-fraction", "-1/2", "", Some("上限分数范围".to_string())),
            FilterCompletionDefinition::new("hint.double-range.closed-fraction", "1/2-3", "", Some("分数闭区间范围".to_string())),
            FilterCompletionDefinition::new("hint.double-range.closed-decimal-fraction", "0.3-1/2", "", Some("混合闭区间范围".to_string())),
        ],
    );
    map.insert(
        FilterValueKind::Date,
        vec![
            FilterCompletionDefinition::new("hint.date.month-day-dash", "MM-dd", "", Some("今年月日 (短横)".to_string())),
            FilterCompletionDefinition::new("hint.date.month-day-dot", "MM.dd", "", Some("今年月日 (点号)".to_string())),
            FilterCompletionDefinition::new("hint.date.full-dash", "yyyy-MM-dd", "", Some("完整年月日 (短横)".to_string())),
            FilterCompletionDefinition::new("hint.date.full-dot", "yyyy.MM.dd", "", Some("完整年月日 (点号)".to_string())),
        ],
    );
    map
}
