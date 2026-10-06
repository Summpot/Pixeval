use crate::text::FilterTextSpan;
use crate::values::{FilterValue, FilterValueKind};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum FilterLogicalOperator {
    #[default]
    And,
    Or,
}

#[derive(Debug, Clone, PartialEq)]
pub enum FilterNode {
    Group(FilterGroupNode),
    Predicate(FilterPredicateNode),
}

impl FilterNode {
    pub fn span(&self) -> FilterTextSpan {
        match self {
            Self::Group(g) => g.span,
            Self::Predicate(p) => p.span,
        }
    }

    pub fn is_negated(&self) -> bool {
        match self {
            Self::Group(g) => g.is_negated,
            Self::Predicate(p) => p.is_negated,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct FilterGroupNode {
    pub operator: FilterLogicalOperator,
    pub children: Vec<FilterNode>,
    pub span: FilterTextSpan,
    pub is_negated: bool,
}

impl FilterGroupNode {
    pub fn new(
        operator: FilterLogicalOperator,
        children: Vec<FilterNode>,
        span: FilterTextSpan,
        is_negated: bool,
    ) -> Self {
        Self {
            operator,
            children,
            span,
            is_negated,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct FilterPredicateNode {
    pub syntax_key: String,
    pub matched_prefix: String,
    pub matched_alias: String,
    pub matched_suffix: String,
    pub value: FilterValue,
    pub span: FilterTextSpan,
    pub is_negated: bool,
}

impl FilterPredicateNode {
    pub fn new(
        syntax_key: impl Into<String>,
        matched_prefix: impl Into<String>,
        matched_alias: impl Into<String>,
        matched_suffix: impl Into<String>,
        value: FilterValue,
        span: FilterTextSpan,
        is_negated: bool,
    ) -> Self {
        Self {
            syntax_key: syntax_key.into(),
            matched_prefix: matched_prefix.into(),
            matched_alias: matched_alias.into(),
            matched_suffix: matched_suffix.into(),
            value,
            span,
            is_negated,
        }
    }
}

#[derive(uniffi::Record, Clone, Debug)]
pub struct FilterAstNode {
    pub is_group: bool,
    pub group_operator: i32, // 0 = And, 1 = Or
    pub is_negated: bool,
    pub span: FilterTextSpan,
    pub syntax_key: String,
    pub matched_prefix: String,
    pub matched_alias: String,
    pub matched_suffix: String,
    pub value_kind: FilterValueKind,
    pub text_content: String,
    pub text_is_exact: bool,
    pub text_content_span: FilterTextSpan,
    pub long_value: i64,
    pub double_value: f64,
    pub range_start_long: i64,
    pub range_end_long: i64,
    pub range_long_is_open_ended: bool,
    pub range_start_double: f64,
    pub range_end_double: f64,
    pub range_double_is_open_ended: bool,
    pub date_has_year: bool,
    pub date_year: i32,
    pub date_month: u32,
    pub date_day: u32,
    pub flag_value: bool,
    pub children: Vec<FilterAstNode>,
}

impl From<&FilterNode> for FilterAstNode {
    fn from(node: &FilterNode) -> Self {
        match node {
            FilterNode::Group(g) => {
                let op = match g.operator {
                    FilterLogicalOperator::And => 0,
                    FilterLogicalOperator::Or => 1,
                };
                Self {
                    is_group: true,
                    group_operator: op,
                    is_negated: g.is_negated,
                    span: g.span,
                    syntax_key: String::new(),
                    matched_prefix: String::new(),
                    matched_alias: String::new(),
                    matched_suffix: String::new(),
                    value_kind: FilterValueKind::None,
                    text_content: String::new(),
                    text_is_exact: false,
                    text_content_span: FilterTextSpan::empty_at(0),
                    long_value: 0,
                    double_value: 0.0,
                    range_start_long: 0,
                    range_end_long: 0,
                    range_long_is_open_ended: false,
                    range_start_double: 0.0,
                    range_end_double: 0.0,
                    range_double_is_open_ended: false,
                    date_has_year: false,
                    date_year: 0,
                    date_month: 0,
                    date_day: 0,
                    flag_value: false,
                    children: g.children.iter().map(Into::into).collect(),
                }
            }
            FilterNode::Predicate(p) => {
                let mut node_dto = Self {
                    is_group: false,
                    group_operator: 0,
                    is_negated: p.is_negated,
                    span: p.span,
                    syntax_key: p.syntax_key.clone(),
                    matched_prefix: p.matched_prefix.clone(),
                    matched_alias: p.matched_alias.clone(),
                    matched_suffix: p.matched_suffix.clone(),
                    value_kind: FilterValueKind::None,
                    text_content: String::new(),
                    text_is_exact: false,
                    text_content_span: FilterTextSpan::empty_at(0),
                    long_value: 0,
                    double_value: 0.0,
                    range_start_long: 0,
                    range_end_long: 0,
                    range_long_is_open_ended: false,
                    range_start_double: 0.0,
                    range_end_double: 0.0,
                    range_double_is_open_ended: false,
                    date_has_year: false,
                    date_year: 0,
                    date_month: 0,
                    date_day: 0,
                    flag_value: false,
                    children: Vec::new(),
                };

                match &p.value {
                    FilterValue::None(_) => {
                        node_dto.value_kind = FilterValueKind::None;
                        node_dto.flag_value = true;
                    }
                    FilterValue::Text(t) => {
                        node_dto.value_kind = FilterValueKind::Text;
                        node_dto.text_content = t.content.clone();
                        node_dto.text_is_exact = t.is_exact;
                        node_dto.text_content_span = t.content_span;
                    }
                    FilterValue::Long(val, _) => {
                        node_dto.value_kind = FilterValueKind::Long;
                        node_dto.long_value = *val;
                    }
                    FilterValue::Double(val, _) => {
                        node_dto.value_kind = FilterValueKind::Double;
                        node_dto.double_value = *val;
                    }
                    FilterValue::LongRange(r, _) => {
                        node_dto.value_kind = FilterValueKind::LongRange;
                        node_dto.range_start_long = r.start;
                        node_dto.range_end_long = r.end;
                        node_dto.range_long_is_open_ended = r.is_open_ended;
                    }
                    FilterValue::DoubleRange(r, _) => {
                        node_dto.value_kind = FilterValueKind::DoubleRange;
                        node_dto.range_start_double = r.start;
                        node_dto.range_end_double = r.end;
                        node_dto.range_double_is_open_ended = r.is_open_ended;
                    }
                    FilterValue::Date(d, _) => {
                        node_dto.value_kind = FilterValueKind::Date;
                        node_dto.date_has_year = d.year.is_some();
                        node_dto.date_year = d.year.unwrap_or(0);
                        node_dto.date_month = d.month;
                        node_dto.date_day = d.day;
                    }
                    FilterValue::Flag(b, _) => {
                        node_dto.value_kind = FilterValueKind::Flag;
                        node_dto.flag_value = *b;
                    }
                }

                node_dto
            }
        }
    }
}

#[derive(uniffi::Object, Debug, Clone, PartialEq)]
pub struct FilterQuery {
    pub root: FilterGroupNode,
}

#[uniffi::export]
impl FilterQuery {
    pub fn has_predicates(&self) -> bool {
        fn check_node(node: &FilterNode) -> bool {
            match node {
                FilterNode::Predicate(_) => true,
                FilterNode::Group(g) => g.children.iter().any(check_node),
            }
        }
        self.root.children.iter().any(check_node)
    }

    pub fn matches_artwork(&self, artwork: crate::eval::ArtworkMetadata) -> bool {
        crate::eval::matches_artwork(self, &artwork)
    }

    pub fn filter_artworks(&self, artworks: Vec<crate::eval::ArtworkMetadata>) -> Vec<bool> {
        crate::eval::filter_artworks(self, &artworks)
    }

    pub fn get_ast(&self) -> FilterAstNode {
        FilterAstNode::from(&FilterNode::Group(self.root.clone()))
    }
}

impl FilterQuery {
    pub fn new(root: FilterGroupNode) -> Self {
        Self { root }
    }
}
