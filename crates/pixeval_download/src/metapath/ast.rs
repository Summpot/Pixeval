// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(uniffi::Record, Clone, Copy, Debug, PartialEq, Eq)]
pub struct MacroTextSpan {
    pub start: i32,
    pub length: i32,
}

pub type TextSpan = MacroTextSpan;

impl TextSpan {
    pub fn new(start: i32, length: i32) -> Self {
        Self {
            start,
            length: length.max(0),
        }
    }

    pub fn from_bounds(start: i32, end: i32) -> Self {
        Self {
            start,
            length: (end - start).max(0),
        }
    }

    pub fn end(&self) -> i32 {
        self.start + self.length
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PlainText {
    pub text: String,
    pub span: TextSpan,
}

impl PlainText {
    pub fn new(text: impl Into<String>, span: TextSpan) -> Self {
        Self {
            text: text.into(),
            span,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ConditionalBranches {
    pub when_true: Option<Sequence>,
    pub when_false: Option<Sequence>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct MacroNode {
    pub name: PlainText,
    pub formatter: Option<PlainText>,
    pub branches: Option<ConditionalBranches>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum SingleNode {
    PlainText(PlainText),
    Macro(MacroNode),
}

#[derive(Clone, Debug, PartialEq, Eq, Default)]
pub struct Sequence {
    pub nodes: Vec<SingleNode>,
}

impl Sequence {
    pub fn new(nodes: Vec<SingleNode>) -> Self {
        Self { nodes }
    }
}
