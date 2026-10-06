// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default, uniffi::Record)]
pub struct HomeCardBounds {
    pub column: i32,
    pub row: i32,
    pub column_span: i32,
    pub row_span: i32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default, uniffi::Record)]
pub struct GridPosition {
    pub column: i32,
    pub row: i32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default, uniffi::Record)]
pub struct GridPlacement {
    pub column: i32,
    pub row: i32,
    pub column_span: i32,
    pub row_span: i32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, uniffi::Record)]
pub struct CardNormalizationItem {
    pub index: u32,
    pub bounds: HomeCardBounds,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct CardNormalizationResult {
    pub placed_cards: Vec<CardNormalizationItem>,
    pub removed_indices: Vec<u32>,
    pub changed: bool,
}
