// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::metapath::ast::{
    ConditionalBranches, MacroNode, PlainText, Sequence, SingleNode, TextSpan,
};
use crate::metapath::syntax::{
    MacroDiagnostic, MacroDiagnosticKind, MacroHighlightKind, MacroHighlightSpan,
};

pub struct MacroSyntaxParser<'a> {
    text: &'a str,
    chars: Vec<(usize, char)>,
    position: usize,
    highlights: Vec<MacroHighlightSpan>,
    diagnostics: Vec<MacroDiagnostic>,
}

impl<'a> MacroSyntaxParser<'a> {
    pub fn new(text: &'a str) -> Self {
        let chars: Vec<(usize, char)> = text.char_indices().collect();
        Self {
            text,
            chars,
            position: 0,
            highlights: Vec::new(),
            diagnostics: Vec::new(),
        }
    }

    pub fn parse(
        mut self,
    ) -> (
        Option<Sequence>,
        Vec<MacroHighlightSpan>,
        Vec<MacroDiagnostic>,
    ) {
        let root = self.parse_sequence(false, false, 0);
        if self.diagnostics.is_empty() && !self.is_at_end() {
            let span = self.current_span();
            self.add_diagnostic(MacroDiagnosticKind::UnexpectedToken, span, vec![]);
        }
        (root, self.highlights, self.diagnostics)
    }

    fn parse_sequence(
        &mut self,
        stop_at_colon: bool,
        stop_at_right_brace: bool,
        nesting_depth: i32,
    ) -> Option<Sequence> {
        let mut nodes = Vec::new();
        let mut plain_text_start = self.current_byte_pos();

        while !self.is_at_end() {
            let c = self.current_char();

            if stop_at_right_brace && c == '}' {
                break;
            }

            if stop_at_colon && c == ':' {
                break;
            }

            if c == '}' {
                self.add_plain_text(&mut nodes, plain_text_start, self.current_byte_pos());
                let span = self.current_span();
                self.add_diagnostic(MacroDiagnosticKind::UnexpectedToken, span, vec![]);
                break;
            }

            if c == '@' {
                self.add_plain_text(&mut nodes, plain_text_start, self.current_byte_pos());
                if let Some(macro_node) = self.parse_macro(nesting_depth) {
                    nodes.push(SingleNode::Macro(macro_node));
                }
                plain_text_start = self.current_byte_pos();
                continue;
            }

            if is_invalid_path_char(c) {
                self.add_plain_text(&mut nodes, plain_text_start, self.current_byte_pos());
                let span = self.current_span();
                self.add_diagnostic(MacroDiagnosticKind::UnexpectedToken, span, vec![]);
                self.advance();
                plain_text_start = self.current_byte_pos();
                continue;
            }

            self.advance();
        }

        self.add_plain_text(&mut nodes, plain_text_start, self.current_byte_pos());
        if nodes.is_empty() {
            None
        } else {
            Some(Sequence::new(nodes))
        }
    }

    fn parse_macro(&mut self, nesting_depth: i32) -> Option<MacroNode> {
        let macro_start = self.current_byte_pos();
        self.add_highlight(macro_start, 1, MacroHighlightKind::Delimiter, nesting_depth);
        self.advance();

        if self.is_at_end() || self.current_char() != '{' {
            let span = TextSpan::from_bounds(
                macro_start as i32,
                (macro_start + 1).min(self.text.len()) as i32,
            );
            self.add_diagnostic(MacroDiagnosticKind::ExpectedLeftBraceAfterAt, span, vec![]);
            self.recover_after_broken_macro(false, false);
            return None;
        }

        self.add_highlight(
            self.current_byte_pos(),
            1,
            MacroHighlightKind::Delimiter,
            nesting_depth,
        );
        self.advance();

        let name_start = self.current_byte_pos();
        while !self.is_at_end() && is_macro_name_char(self.current_char()) {
            self.advance();
        }

        if self.current_byte_pos() == name_start {
            let span = TextSpan::from_bounds(
                name_start as i32,
                (name_start + 1).min(self.text.len()) as i32,
            );
            self.add_diagnostic(MacroDiagnosticKind::ExpectedMacroName, span, vec![]);
            self.recover_after_broken_macro(false, true);
            return None;
        }

        let name_end = self.current_byte_pos();
        self.add_highlight(
            name_start,
            name_end - name_start,
            MacroHighlightKind::Name,
            nesting_depth,
        );
        let macro_name = PlainText::new(
            &self.text[name_start..name_end],
            TextSpan::new(name_start as i32, (name_end - name_start) as i32),
        );

        let formatter = self.parse_formatter(nesting_depth);
        let diagnostic_start = self.diagnostics.len();
        let branches = self.parse_conditional_branches(nesting_depth);
        if self.diagnostics.len() > diagnostic_start {
            self.recover_after_broken_macro(false, true);
            return None;
        }

        if self.is_at_end() || self.current_char() != '}' {
            let span = TextSpan::from_bounds(
                macro_start as i32,
                (macro_start + 2).min(self.text.len()) as i32,
            );
            self.add_diagnostic(MacroDiagnosticKind::MissingRightBrace, span, vec![]);
            self.recover_after_broken_macro(false, true);
            return None;
        }

        self.add_highlight(
            self.current_byte_pos(),
            1,
            MacroHighlightKind::Delimiter,
            nesting_depth,
        );
        self.advance();

        Some(MacroNode {
            name: macro_name,
            formatter,
            branches,
        })
    }

    fn parse_formatter(&mut self, nesting_depth: i32) -> Option<PlainText> {
        if self.is_at_end() || self.current_char() != ':' {
            return None;
        }

        self.add_highlight(
            self.current_byte_pos(),
            1,
            MacroHighlightKind::Separator,
            nesting_depth,
        );
        self.advance();

        let formatter_start = self.current_byte_pos();
        while !self.is_at_end() && self.current_char() != '?' && self.current_char() != '}' {
            self.advance();
        }

        let formatter_end = self.current_byte_pos();
        if formatter_end > formatter_start {
            self.add_highlight(
                formatter_start,
                formatter_end - formatter_start,
                MacroHighlightKind::Formatter,
                nesting_depth,
            );
        }

        Some(PlainText::new(
            &self.text[formatter_start..formatter_end],
            TextSpan::new(
                formatter_start as i32,
                (formatter_end - formatter_start) as i32,
            ),
        ))
    }

    fn parse_conditional_branches(&mut self, nesting_depth: i32) -> Option<ConditionalBranches> {
        if self.is_at_end() || self.current_char() != '?' {
            return None;
        }

        self.add_highlight(
            self.current_byte_pos(),
            1,
            MacroHighlightKind::Separator,
            nesting_depth,
        );
        self.advance();

        let diagnostic_start = self.diagnostics.len();
        let when_true = self.parse_sequence(true, true, nesting_depth + 1);
        if self.diagnostics.len() > diagnostic_start {
            return None;
        }

        if self.is_at_end() || self.current_char() != ':' {
            let span = self.current_span();
            self.add_diagnostic(
                MacroDiagnosticKind::MissingConditionalSeparator,
                span,
                vec![],
            );
            return None;
        }

        self.add_highlight(
            self.current_byte_pos(),
            1,
            MacroHighlightKind::Separator,
            nesting_depth,
        );
        self.advance();

        let when_false = self.parse_sequence(false, true, nesting_depth + 1);
        Some(ConditionalBranches {
            when_true,
            when_false,
        })
    }

    fn recover_after_broken_macro(&mut self, stop_at_colon: bool, stop_at_right_brace: bool) {
        while !self.is_at_end() {
            let c = self.current_char();
            if stop_at_right_brace && c == '}' {
                self.add_highlight(self.current_byte_pos(), 1, MacroHighlightKind::Delimiter, 0);
                self.advance();
                return;
            }

            if stop_at_colon && c == ':' {
                return;
            }

            if c == '@' || c == '}' {
                return;
            }

            self.advance();
        }
    }

    fn add_plain_text(&self, nodes: &mut Vec<SingleNode>, start: usize, end: usize) {
        if end > start {
            nodes.push(SingleNode::PlainText(PlainText::new(
                &self.text[start..end],
                TextSpan::new(start as i32, (end - start) as i32),
            )));
        }
    }

    fn add_highlight(
        &mut self,
        start: usize,
        length: usize,
        kind: MacroHighlightKind,
        nesting_depth: i32,
    ) {
        self.highlights.push(MacroHighlightSpan {
            span: TextSpan::new(start as i32, length as i32),
            kind,
            nesting_depth,
        });
    }

    fn add_diagnostic(
        &mut self,
        kind: MacroDiagnosticKind,
        span: TextSpan,
        arguments: Vec<String>,
    ) {
        if self.diagnostics.is_empty() {
            self.diagnostics
                .push(MacroDiagnostic::new(kind, span, arguments));
        }
    }

    fn current_span(&self) -> TextSpan {
        if self.is_at_end() {
            let start = self.text.len().saturating_sub(1);
            TextSpan::from_bounds(start as i32, self.text.len() as i32)
        } else {
            let (pos, c) = self.chars[self.position];
            TextSpan::new(pos as i32, c.len_utf8() as i32)
        }
    }

    fn is_at_end(&self) -> bool {
        self.position >= self.chars.len()
    }

    fn current_char(&self) -> char {
        self.chars[self.position].1
    }

    fn current_byte_pos(&self) -> usize {
        if self.position < self.chars.len() {
            self.chars[self.position].0
        } else {
            self.text.len()
        }
    }

    fn advance(&mut self) {
        if self.position < self.chars.len() {
            self.position += 1;
        }
    }
}

fn is_macro_name_char(c: char) -> bool {
    c.is_ascii_alphanumeric() || c == '_'
}

fn is_invalid_path_char(c: char) -> bool {
    (c as u32) < 32 || matches!(c, '\"' | '<' | '>' | '|' | '\0')
}
