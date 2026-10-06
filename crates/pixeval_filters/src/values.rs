use crate::text::FilterTextSpan;
use chrono::NaiveDate;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, uniffi::Enum)]
pub enum FilterValueKind {
    None,
    Text,
    Long,
    Double,
    LongRange,
    DoubleRange,
    Date,
    Flag,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FilterTextValue {
    pub content: String,
    pub content_span: FilterTextSpan,
    pub is_exact: bool,
    pub span: FilterTextSpan,
}

impl FilterTextValue {
    pub fn new(
        content: impl Into<String>,
        content_span: FilterTextSpan,
        is_exact: bool,
        span: FilterTextSpan,
    ) -> Self {
        Self {
            content: content.into(),
            content_span,
            is_exact,
            span,
        }
    }

    pub fn matches(&self, target: &str) -> bool {
        if self.is_exact {
            target == self.content
        } else {
            target.contains(&self.content)
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub struct FilterLongRange {
    pub start: i64,
    pub end: i64,
    pub is_open_ended: bool,
}

impl FilterLongRange {
    pub fn new(start: i64, end: i64, is_open_ended: bool) -> Self {
        Self {
            start,
            end,
            is_open_ended,
        }
    }

    pub fn contains(&self, value: i64) -> bool {
        value >= self.start && (self.is_open_ended || value <= self.end)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Default)]
pub struct FilterDoubleRange {
    pub start: f64,
    pub end: f64,
    pub is_open_ended: bool,
}

impl FilterDoubleRange {
    pub fn new(start: f64, end: f64, is_open_ended: bool) -> Self {
        Self {
            start,
            end,
            is_open_ended,
        }
    }

    pub fn contains(&self, value: f64) -> bool {
        value >= self.start && (self.is_open_ended || value <= self.end)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct FilterDateLiteral {
    pub year: Option<i32>,
    pub month: u32,
    pub day: u32,
}

impl FilterDateLiteral {
    pub fn new(year: Option<i32>, month: u32, day: u32) -> Self {
        Self { year, month, day }
    }

    pub fn to_naive_date(&self, fallback_year: i32) -> Option<NaiveDate> {
        let y = self.year.unwrap_or(fallback_year);
        NaiveDate::from_ymd_opt(y, self.month, self.day)
    }

    pub fn format_literal(&self) -> String {
        if let Some(year) = self.year {
            format!("{year}-{}-{}", self.month, self.day)
        } else {
            format!("{}-{}", self.month, self.day)
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub enum FilterValue {
    None(FilterTextSpan),
    Text(FilterTextValue),
    Long(i64, FilterTextSpan),
    Double(f64, FilterTextSpan),
    LongRange(FilterLongRange, FilterTextSpan),
    DoubleRange(FilterDoubleRange, FilterTextSpan),
    Date(FilterDateLiteral, FilterTextSpan),
    Flag(bool, FilterTextSpan),
}

impl FilterValue {
    pub fn span(&self) -> FilterTextSpan {
        match self {
            Self::None(span) => *span,
            Self::Text(t) => t.span,
            Self::Long(_, span) => *span,
            Self::Double(_, span) => *span,
            Self::LongRange(_, span) => *span,
            Self::DoubleRange(_, span) => *span,
            Self::Date(_, span) => *span,
            Self::Flag(_, span) => *span,
        }
    }
}
