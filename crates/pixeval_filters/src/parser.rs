use crate::ast::{
    FilterGroupNode, FilterLogicalOperator, FilterNode, FilterPredicateNode, FilterQuery,
};
use crate::diagnostics::{FilterDiagnostic, FilterDiagnosticKind};
use crate::syntax::FilterSyntaxMatch;
use crate::text::FilterTextSpan;
use crate::values::{
    FilterDateLiteral, FilterDoubleRange, FilterLongRange, FilterTextValue, FilterValue,
    FilterValueKind,
};

pub struct Parser<'a> {
    text: &'a str,
    chars: Vec<(usize, usize, char)>, // (byte_offset, utf16_offset, char)
    char_index: usize,
    total_utf16_len: usize,
    matches: &'a [FilterSyntaxMatch],
    default_text_match: Option<&'a FilterSyntaxMatch>,
    special_starters: &'a [char],
    diagnostics: Vec<FilterDiagnostic>,
}

impl<'a> Parser<'a> {
    pub fn new(
        text: &'a str,
        matches: &'a [FilterSyntaxMatch],
        default_text_match: Option<&'a FilterSyntaxMatch>,
        special_starters: &'a [char],
    ) -> Self {
        let mut chars = Vec::with_capacity(text.len());
        let mut utf16_offset = 0;
        let mut byte_offset = 0;
        for c in text.chars() {
            chars.push((byte_offset, utf16_offset, c));
            byte_offset += c.len_utf8();
            utf16_offset += c.len_utf16();
        }
        let total_utf16_len = utf16_offset;

        Self {
            text,
            chars,
            char_index: 0,
            total_utf16_len,
            matches,
            default_text_match,
            special_starters,
            diagnostics: Vec::new(),
        }
    }

    pub fn parse(mut self) -> (Option<FilterQuery>, Vec<FilterDiagnostic>) {
        let children = self.parse_terms(false, -1);
        self.skip_whitespace();
        if self.diagnostics.is_empty() && !self.is_at_end() {
            let span = self.current_token_span();
            self.add_unexpected_token_diagnostic(span);
        }

        let query = if self.diagnostics.is_empty() {
            Some(FilterQuery::new(FilterGroupNode::new(
                FilterLogicalOperator::And,
                children,
                FilterTextSpan::from_bounds(0, self.total_utf16_len as i32),
                false,
            )))
        } else {
            None
        };

        (query, self.diagnostics)
    }

    fn current_pos(&self) -> i32 {
        if self.char_index < self.chars.len() {
            self.chars[self.char_index].1 as i32
        } else {
            self.total_utf16_len as i32
        }
    }

    fn current_byte_pos(&self) -> usize {
        if self.char_index < self.chars.len() {
            self.chars[self.char_index].0
        } else {
            self.text.len()
        }
    }

    fn utf16_to_byte(&self, utf16_pos: usize) -> usize {
        if utf16_pos >= self.total_utf16_len {
            return self.text.len();
        }
        match self.chars.binary_search_by_key(&utf16_pos, |&(_, u, _)| u) {
            Ok(idx) => self.chars[idx].0,
            Err(idx) => {
                if idx < self.chars.len() {
                    self.chars[idx].0
                } else {
                    self.text.len()
                }
            }
        }
    }

    fn slice_utf16(&self, start_utf16: i32, end_utf16: i32) -> &'a str {
        if end_utf16 <= start_utf16 || start_utf16 < 0 {
            return "";
        }
        let b_start = self.utf16_to_byte(start_utf16 as usize);
        let b_end = self.utf16_to_byte(end_utf16 as usize);
        if b_start >= self.text.len() {
            ""
        } else if b_end >= self.text.len() {
            &self.text[b_start..]
        } else {
            &self.text[b_start..b_end]
        }
    }

    fn current_char(&self) -> char {
        if self.char_index < self.chars.len() {
            self.chars[self.char_index].2
        } else {
            '\0'
        }
    }

    fn is_at_end(&self) -> bool {
        self.char_index >= self.chars.len()
    }

    fn advance(&mut self) {
        if self.char_index < self.chars.len() {
            self.char_index += 1;
        }
    }

    fn skip_whitespace(&mut self) {
        while !self.is_at_end() && self.current_char().is_whitespace() {
            self.advance();
        }
    }

    fn try_consume(&mut self, ch: char) -> bool {
        if !self.is_at_end() && self.current_char() == ch {
            self.advance();
            true
        } else {
            false
        }
    }

    fn try_consume_date_separator(&mut self) -> bool {
        if !self.is_at_end() && (self.current_char() == '-' || self.current_char() == '.') {
            self.advance();
            true
        } else {
            false
        }
    }

    fn current_token_span(&self) -> FilterTextSpan {
        let start = self.current_pos();
        let mut idx = self.char_index;
        while idx < self.chars.len() {
            let ch = self.chars[idx].2;
            if ch.is_whitespace() || ch == '(' || ch == ')' {
                break;
            }
            idx += 1;
        }
        let end = if idx < self.chars.len() {
            self.chars[idx].1 as i32
        } else {
            self.total_utf16_len as i32
        };

        let final_end = if end == start {
            (start + 1).min(self.total_utf16_len as i32)
        } else {
            end
        };

        FilterTextSpan::from_bounds(start, final_end)
    }

    fn add_diagnostic(
        &mut self,
        kind: FilterDiagnosticKind,
        span: FilterTextSpan,
        args: Vec<String>,
    ) {
        self.diagnostics
            .push(FilterDiagnostic::new(kind, span, args));
    }

    fn add_unexpected_token_diagnostic(&mut self, span: FilterTextSpan) {
        let arg = span.get_text(self.text).to_string();
        self.add_diagnostic(FilterDiagnosticKind::UnexpectedToken, span, vec![arg]);
    }

    fn parse_terms(&mut self, expect_right_paren: bool, group_start: i32) -> Vec<FilterNode> {
        let mut children = Vec::new();
        loop {
            self.skip_whitespace();
            if self.is_at_end() {
                if expect_right_paren {
                    self.add_diagnostic(
                        FilterDiagnosticKind::MissingRightParenthesis,
                        FilterTextSpan::from_bounds(
                            group_start,
                            (group_start + 1).min(self.text.len() as i32),
                        ),
                        vec!["(".to_string()],
                    );
                }
                break;
            }

            if self.current_char() == ')' {
                break;
            }

            match self.try_parse_term() {
                Some(Some(node)) => children.push(node),
                Some(None) => {}
                None => break,
            }
        }
        children
    }

    fn try_parse_term(&mut self) -> Option<Option<FilterNode>> {
        let term_start = self.current_pos();
        let mut is_negated = false;

        if self.current_char() == '!' {
            is_negated = true;
            self.advance();
            self.skip_whitespace();
            if self.is_at_end() {
                self.add_diagnostic(
                    FilterDiagnosticKind::MissingPredicateAfterNegation,
                    FilterTextSpan::from_bounds(term_start, self.current_pos()),
                    vec!["!".to_string()],
                );
                return None;
            }
        }

        if self.current_char() == '(' {
            return self
                .parse_group(term_start, is_negated)
                .map(|group| Some(FilterNode::Group(group)));
        }

        let diag_count_before = self.diagnostics.len();
        if let Some(syntax_node) = self.try_parse_syntax_term(term_start, is_negated) {
            return if self.diagnostics.len() == diag_count_before {
                Some(Some(syntax_node))
            } else {
                None
            };
        }

        if self.diagnostics.len() > diag_count_before {
            return None;
        }

        if let Some(default_text) = self.default_text_match {
            let value_start = if default_text.header_text.is_empty() {
                term_start
            } else {
                self.current_pos()
            };
            {
                let raw_val = self.try_parse_text_value(default_text, value_start)?;
                let term_span = FilterTextSpan::from_bounds(term_start, self.current_pos());
                return Some(Some(FilterNode::Predicate(FilterPredicateNode::new(
                    &default_text.syntax_key,
                    "",
                    "",
                    "",
                    raw_val,
                    term_span,
                    is_negated,
                ))));
            }
        }

        let span = self.current_token_span();
        self.add_unexpected_token_diagnostic(span);
        None
    }

    fn parse_group(&mut self, term_start: i32, is_negated: bool) -> Option<FilterGroupNode> {
        let group_start = self.current_pos();
        self.advance(); // consume '('
        self.skip_whitespace();
        let operator_start = self.current_pos();
        let group_op = self.parse_group_operator(operator_start)?;

        let children = self.parse_terms(true, group_start);
        if !self.diagnostics.is_empty() {
            return None;
        }

        if self.is_at_end() || self.current_char() != ')' {
            return None;
        }

        self.advance(); // consume ')'
        Some(FilterGroupNode::new(
            group_op,
            children,
            FilterTextSpan::from_bounds(term_start, self.current_pos()),
            is_negated,
        ))
    }

    fn parse_group_operator(&mut self, operator_start: i32) -> Option<FilterLogicalOperator> {
        let word_start = self.current_pos();
        let mut word_str = String::new();
        while !self.is_at_end() && self.current_char().is_alphabetic() {
            word_str.push(self.current_char());
            self.advance();
        }

        if word_str.eq_ignore_ascii_case("and") {
            return Some(FilterLogicalOperator::And);
        }
        if word_str.eq_ignore_ascii_case("or") {
            return Some(FilterLogicalOperator::Or);
        }

        let arg = if !word_str.is_empty() {
            word_str
        } else {
            FilterTextSpan::from_bounds(word_start, (word_start + 1).max(self.current_pos()))
                .get_text(self.text)
                .to_string()
        };

        self.add_diagnostic(
            FilterDiagnosticKind::MissingGroupOperator,
            FilterTextSpan::from_bounds(
                operator_start,
                operator_start
                    .max(self.current_pos())
                    .max(operator_start + 1),
            ),
            vec![arg],
        );
        None
    }

    fn try_parse_syntax_term(&mut self, term_start: i32, is_negated: bool) -> Option<FilterNode> {
        let curr_char = self.current_char();
        let remaining_str = &self.text[self.current_byte_pos()..];

        let mut best_match: Option<&FilterSyntaxMatch> = None;
        for candidate in self.matches {
            if candidate.header_text.is_empty() {
                continue;
            }
            if remaining_str.len() >= candidate.header_text.len()
                && remaining_str.is_char_boundary(candidate.header_text.len())
                && remaining_str[..candidate.header_text.len()]
                    .eq_ignore_ascii_case(&candidate.header_text)
            {
                match best_match {
                    None => best_match = Some(candidate),
                    Some(b) if candidate.header_text.len() > b.header_text.len() => {
                        best_match = Some(candidate);
                    }
                    _ => {}
                }
            }
        }

        let m = match best_match {
            Some(m) => m,
            None => {
                if self.special_starters.contains(&curr_char)
                    || curr_char == ']'
                    || curr_char == ')'
                {
                    let span = self.current_token_span();
                    self.add_unexpected_token_diagnostic(span);
                }
                return None;
            }
        };

        // advance past header_text
        for _ in 0..m.header_text.chars().count() {
            self.advance();
        }

        self.skip_whitespace();
        let raw_val = self.try_parse_value(m, term_start)?;
        let term_span = FilterTextSpan::from_bounds(term_start, self.current_pos());

        Some(FilterNode::Predicate(FilterPredicateNode::new(
            &m.syntax_key,
            &m.prefix,
            &m.alias,
            &m.suffix,
            raw_val,
            term_span,
            is_negated,
        )))
    }

    fn try_parse_value(&mut self, m: &FilterSyntaxMatch, term_start: i32) -> Option<FilterValue> {
        match m.value_kind {
            FilterValueKind::None => Some(FilterValue::None(FilterTextSpan::empty_at(
                self.current_pos(),
            ))),
            FilterValueKind::Text => self.try_parse_text_value(m, term_start),
            FilterValueKind::Long => self.try_parse_long_value(m),
            FilterValueKind::Double => self.try_parse_double_value(m),
            FilterValueKind::LongRange => self.try_parse_long_range_value(m),
            FilterValueKind::DoubleRange => self.try_parse_double_range_value(m),
            FilterValueKind::Date => self.try_parse_date_value(m),
            FilterValueKind::Flag => {
                let flag_val = m.metadata.as_deref() != Some("true");
                Some(FilterValue::Flag(
                    flag_val,
                    FilterTextSpan::empty_at(self.current_pos()),
                ))
            }
        }
    }

    fn try_parse_text_value(
        &mut self,
        m: &FilterSyntaxMatch,
        term_start: i32,
    ) -> Option<FilterValue> {
        if self.is_at_end() || self.current_char() == ')' {
            self.add_diagnostic(
                FilterDiagnosticKind::MissingTextValue,
                FilterTextSpan::from_bounds(term_start, self.current_pos()),
                vec![m.diagnostic_text.clone()],
            );
            return None;
        }

        if self.current_char() == '"' {
            let quoted_start = self.current_pos();
            self.advance();
            let content_start = self.current_pos();
            while !self.is_at_end() && self.current_char() != '"' {
                self.advance();
            }

            if self.is_at_end() {
                let span = FilterTextSpan::from_bounds(quoted_start, self.current_pos());
                self.add_diagnostic(
                    FilterDiagnosticKind::MissingStringQuote,
                    span,
                    vec![span.get_text(self.text).to_string()],
                );
                return None;
            }

            let content_end = self.current_pos();
            self.advance(); // consume closing '"'
            let is_exact = self.try_consume('$');
            let content = self.slice_utf16(content_start, content_end).to_string();
            let content_span = FilterTextSpan::from_bounds(content_start, content_end);
            let span = FilterTextSpan::from_bounds(quoted_start, self.current_pos());
            return Some(FilterValue::Text(FilterTextValue::new(
                content,
                content_span,
                is_exact,
                span,
            )));
        }

        let start = self.current_pos();
        while !self.is_at_end()
            && !self.current_char().is_whitespace()
            && self.current_char() != ')'
        {
            self.advance();
        }

        let end = self.current_pos();
        let unquoted_slice = self.slice_utf16(start, end);
        let is_exact = unquoted_slice.ends_with('$');
        let content_end = if is_exact { end - 1 } else { end };

        if content_end <= start {
            self.add_diagnostic(
                FilterDiagnosticKind::MissingTextValue,
                FilterTextSpan::from_bounds(term_start, end),
                vec![m.diagnostic_text.clone()],
            );
            return None;
        }

        let content = self.slice_utf16(start, content_end).to_string();
        let content_span = FilterTextSpan::from_bounds(start, content_end);
        let span = FilterTextSpan::from_bounds(start, end);
        Some(FilterValue::Text(FilterTextValue::new(
            content,
            content_span,
            is_exact,
            span,
        )))
    }

    fn try_parse_long_value(&mut self, m: &FilterSyntaxMatch) -> Option<FilterValue> {
        let value_start = self.current_pos();
        if self.is_at_end() || self.current_char() == ')' {
            self.add_diagnostic(
                FilterDiagnosticKind::MissingLongValue,
                FilterTextSpan::from_bounds(value_start, self.current_pos()),
                vec![m.diagnostic_text.clone()],
            );
            return None;
        }

        let (val, span) = self.try_read_u64()?;
        Some(FilterValue::Long(val as i64, span))
    }

    fn try_parse_double_value(&mut self, m: &FilterSyntaxMatch) -> Option<FilterValue> {
        let value_start = self.current_pos();
        if self.is_at_end() || self.current_char() == ')' {
            self.add_diagnostic(
                FilterDiagnosticKind::MissingDoubleValue,
                FilterTextSpan::from_bounds(value_start, self.current_pos()),
                vec![m.diagnostic_text.clone()],
            );
            return None;
        }

        let (val, span) = self.try_read_double_literal()?;
        Some(FilterValue::Double(val, span))
    }

    fn try_parse_long_range_value(&mut self, m: &FilterSyntaxMatch) -> Option<FilterValue> {
        let range_start = self.current_pos();
        if self.is_at_end() || self.current_char() == ')' {
            self.add_diagnostic(
                FilterDiagnosticKind::MissingRangeValue,
                FilterTextSpan::from_bounds(range_start, self.current_pos()),
                vec![m.diagnostic_text.clone()],
            );
            return None;
        }

        if self.current_char() == '[' || self.current_char() == '(' {
            let span = FilterTextSpan::from_bounds(range_start, self.current_pos() + 1);
            self.add_diagnostic(
                FilterDiagnosticKind::InvalidLongRangeFormat,
                span,
                vec![
                    m.diagnostic_text.clone(),
                    span.get_text(self.text).to_string(),
                ],
            );
            return None;
        }

        if self.current_char() == '-' {
            self.advance();
            self.skip_whitespace();
            let (upper, _) = self.try_read_u64()?;
            let span = FilterTextSpan::from_bounds(range_start, self.current_pos());
            let range = FilterLongRange::new(0, upper as i64, false);
            return Some(FilterValue::LongRange(range, span));
        }

        let (lower, _) = self.try_read_u64()?;
        self.skip_whitespace();
        if !self.try_consume('-') {
            let span = FilterTextSpan::from_bounds(range_start, self.current_pos());
            self.add_diagnostic(
                FilterDiagnosticKind::InvalidLongRangeFormat,
                span,
                vec![
                    m.diagnostic_text.clone(),
                    span.get_text(self.text).to_string(),
                ],
            );
            return None;
        }

        self.skip_whitespace();
        if self.is_at_end() || self.current_char() == ')' {
            let span = FilterTextSpan::from_bounds(range_start, self.current_pos());
            let range = FilterLongRange::new(lower as i64, 0, true);
            return Some(FilterValue::LongRange(range, span));
        }

        let (upper, _) = self.try_read_u64()?;
        let span = FilterTextSpan::from_bounds(range_start, self.current_pos());
        if (upper as i64) < (lower as i64) {
            self.add_diagnostic(
                FilterDiagnosticKind::RangeMinimumGreaterThanMaximum,
                span,
                vec![
                    m.diagnostic_text.clone(),
                    lower.to_string(),
                    upper.to_string(),
                ],
            );
            return None;
        }

        let range = FilterLongRange::new(lower as i64, upper as i64, false);
        Some(FilterValue::LongRange(range, span))
    }

    fn try_parse_double_range_value(&mut self, m: &FilterSyntaxMatch) -> Option<FilterValue> {
        let range_start = self.current_pos();
        if self.is_at_end() || self.current_char() == ')' {
            self.add_diagnostic(
                FilterDiagnosticKind::MissingRangeValue,
                FilterTextSpan::from_bounds(range_start, self.current_pos()),
                vec![m.diagnostic_text.clone()],
            );
            return None;
        }

        if self.current_char() == '[' || self.current_char() == '(' {
            let span = FilterTextSpan::from_bounds(range_start, self.current_pos() + 1);
            self.add_diagnostic(
                FilterDiagnosticKind::InvalidDoubleRangeFormat,
                span,
                vec![
                    m.diagnostic_text.clone(),
                    span.get_text(self.text).to_string(),
                ],
            );
            return None;
        }

        if self.current_char() == '-' {
            self.advance();
            self.skip_whitespace();
            let (upper, _) = self.try_read_double_literal()?;
            let span = FilterTextSpan::from_bounds(range_start, self.current_pos());
            let range = FilterDoubleRange::new(0.0, upper, false);
            return Some(FilterValue::DoubleRange(range, span));
        }

        let (lower, _) = self.try_read_double_literal()?;
        self.skip_whitespace();
        if !self.try_consume('-') {
            let span = FilterTextSpan::from_bounds(range_start, self.current_pos());
            self.add_diagnostic(
                FilterDiagnosticKind::InvalidDoubleRangeFormat,
                span,
                vec![
                    m.diagnostic_text.clone(),
                    span.get_text(self.text).to_string(),
                ],
            );
            return None;
        }

        self.skip_whitespace();
        if self.is_at_end() || self.current_char() == ')' {
            let span = FilterTextSpan::from_bounds(range_start, self.current_pos());
            let range = FilterDoubleRange::new(lower, 0.0, true);
            return Some(FilterValue::DoubleRange(range, span));
        }

        let (upper, _) = self.try_read_double_literal()?;
        let span = FilterTextSpan::from_bounds(range_start, self.current_pos());
        if upper < lower {
            self.add_diagnostic(
                FilterDiagnosticKind::RangeMinimumGreaterThanMaximum,
                span,
                vec![
                    m.diagnostic_text.clone(),
                    lower.to_string(),
                    upper.to_string(),
                ],
            );
            return None;
        }

        let range = FilterDoubleRange::new(lower, upper, false);
        Some(FilterValue::DoubleRange(range, span))
    }

    fn try_parse_date_value(&mut self, m: &FilterSyntaxMatch) -> Option<FilterValue> {
        let date_start = self.current_pos();
        if self.is_at_end() || self.current_char() == ')' {
            self.add_diagnostic(
                FilterDiagnosticKind::MissingDateValue,
                FilterTextSpan::from_bounds(date_start, self.current_pos()),
                vec![m.diagnostic_text.clone()],
            );
            return None;
        }

        let (first, _) = self.try_read_u64()?;
        if !self.try_consume_date_separator() {
            let span = FilterTextSpan::from_bounds(date_start, self.current_pos());
            self.add_diagnostic(
                FilterDiagnosticKind::DateRequiresMonthAndDay,
                span,
                vec![
                    m.diagnostic_text.clone(),
                    span.get_text(self.text).to_string(),
                ],
            );
            return None;
        }

        let (second, _) = self.try_read_u64()?;
        if self.try_consume_date_separator() {
            let (third, _) = self.try_read_u64()?;
            let span = FilterTextSpan::from_bounds(date_start, self.current_pos());
            let year = self.try_narrow(first)?;
            let month = self.try_narrow(second)? as u32;
            let day = self.try_narrow(third)? as u32;
            let date_lit = FilterDateLiteral::new(Some(year), month, day);
            if date_lit.to_naive_date(2024).is_none() {
                self.add_diagnostic(
                    FilterDiagnosticKind::InvalidDate,
                    span,
                    vec![m.diagnostic_text.clone(), date_lit.format_literal()],
                );
                return None;
            }
            return Some(FilterValue::Date(date_lit, span));
        }

        let span = FilterTextSpan::from_bounds(date_start, self.current_pos());
        let month = self.try_narrow(first)? as u32;
        let day = self.try_narrow(second)? as u32;
        let date_lit = FilterDateLiteral::new(None, month, day);
        if date_lit.to_naive_date(2024).is_none() {
            self.add_diagnostic(
                FilterDiagnosticKind::InvalidDate,
                span,
                vec![m.diagnostic_text.clone(), date_lit.format_literal()],
            );
            return None;
        }
        Some(FilterValue::Date(date_lit, span))
    }

    fn try_read_u64(&mut self) -> Option<(u64, FilterTextSpan)> {
        let start = self.current_pos();
        while !self.is_at_end() && self.current_char().is_ascii_digit() {
            self.advance();
        }

        if start == self.current_pos() {
            let span = FilterTextSpan::empty_at(self.current_pos());
            let arg = self.current_token_span().get_text(self.text).to_string();
            self.add_diagnostic(FilterDiagnosticKind::ExpectedInteger, span, vec![arg]);
            return None;
        }

        let span = FilterTextSpan::from_bounds(start, self.current_pos());
        let slice = self.slice_utf16(start, self.current_pos());
        match slice.parse::<u64>() {
            Ok(v) => Some((v, span)),
            Err(_) => {
                let arg = span.get_text(self.text).to_string();
                self.add_diagnostic(FilterDiagnosticKind::IntegerOutOfRange, span, vec![arg]);
                None
            }
        }
    }

    fn try_read_double_literal(&mut self) -> Option<(f64, FilterTextSpan)> {
        let literal_start = self.current_pos();
        let (integral, _) = self.try_read_u64()?;

        if !self.is_at_end() && self.current_char() == '/' {
            self.advance();
            let (denom, _) = self.try_read_u64()?;
            let span = FilterTextSpan::from_bounds(literal_start, self.current_pos());
            if denom == 0 {
                let arg = span.get_text(self.text).to_string();
                self.add_diagnostic(
                    FilterDiagnosticKind::DenominatorCannotBeZero,
                    span,
                    vec![arg],
                );
                return None;
            }
            return Some((integral as f64 / denom as f64, span));
        }

        if !self.is_at_end() && self.current_char() == '.' {
            self.advance();
            let frac_start = self.current_pos();
            while !self.is_at_end() && self.current_char().is_ascii_digit() {
                self.advance();
            }

            if frac_start == self.current_pos() {
                let span = FilterTextSpan::from_bounds(literal_start, self.current_pos());
                let arg = span.get_text(self.text).to_string();
                self.add_diagnostic(FilterDiagnosticKind::MissingFractionalPart, span, vec![arg]);
                return None;
            }

            let span = FilterTextSpan::from_bounds(literal_start, self.current_pos());
            let slice = self.slice_utf16(literal_start, self.current_pos());
            match slice.parse::<f64>() {
                Ok(v) => return Some((v, span)),

                Err(_) => {
                    let arg = span.get_text(self.text).to_string();
                    self.add_diagnostic(FilterDiagnosticKind::InvalidDoubleValue, span, vec![arg]);
                    return None;
                }
            }
        }

        let span = FilterTextSpan::from_bounds(literal_start, self.current_pos());
        Some((integral as f64, span))
    }

    fn try_narrow(&mut self, val: u64) -> Option<i32> {
        if val > i32::MAX as u64 {
            let span = self.current_token_span();
            self.add_diagnostic(
                FilterDiagnosticKind::DateValueTooLarge,
                span,
                vec![val.to_string()],
            );
            None
        } else {
            Some(val as i32)
        }
    }
}
