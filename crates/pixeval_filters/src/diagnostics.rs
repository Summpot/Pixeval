use crate::text::FilterTextSpan;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, uniffi::Enum)]
pub enum FilterDiagnosticKind {
    UnexpectedToken,
    MissingPredicateAfterNegation,
    MissingTextValue,
    MissingLongValue,
    MissingDoubleValue,
    MissingRangeValue,
    MissingDateValue,
    MissingGroupOperator,
    MissingRightParenthesis,
    InvalidValue,
    MissingStringQuote,
    InvalidLongRangeFormat,
    InvalidDoubleRangeFormat,
    DateRequiresMonthAndDay,
    ExpectedInteger,
    IntegerOutOfRange,
    DenominatorCannotBeZero,
    MissingFractionalPart,
    InvalidDoubleValue,
    DateValueTooLarge,
    NegativeRangeUnsupported,
    RangeMinimumGreaterThanMaximum,
    DoubleRangeOpenIntervalUnsupported,
    InvalidDate,
    UnsupportedValueKind,
    InternalExpectedTextValue,
    InternalExpectedLongValue,
    InternalExpectedDoubleValue,
    InternalExpectedLongRangeValue,
    InternalExpectedDoubleRangeValue,
    InternalExpectedDateValue,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct FilterDiagnostic {
    pub kind: FilterDiagnosticKind,
    pub span: FilterTextSpan,
    pub arguments: Vec<String>,
}

impl FilterDiagnostic {
    pub fn new(kind: FilterDiagnosticKind, span: FilterTextSpan, arguments: Vec<String>) -> Self {
        Self {
            kind,
            span,
            arguments,
        }
    }
}
