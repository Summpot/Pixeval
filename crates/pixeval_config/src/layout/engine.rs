// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::layout::models::{
    CardNormalizationItem, CardNormalizationResult, GridPlacement, GridPosition, HomeCardBounds,
};

pub fn is_within_grid(bounds: HomeCardBounds, row_count: i32, column_count: i32) -> bool {
    bounds.column >= 0
        && bounds.row >= 0
        && bounds.column_span >= 1
        && bounds.row_span >= 1
        && bounds.column + bounds.column_span <= column_count
        && bounds.row + bounds.row_span <= row_count
}

pub fn overlaps(first: HomeCardBounds, second: HomeCardBounds) -> bool {
    first.column < second.column + second.column_span
        && first.column + first.column_span > second.column
        && first.row < second.row + second.row_span
        && first.row + first.row_span > second.row
}

pub fn can_place(
    cards: &[HomeCardBounds],
    moving_index: Option<u32>,
    bounds: HomeCardBounds,
    row_count: i32,
    column_count: i32,
) -> bool {
    if !is_within_grid(bounds, row_count, column_count) {
        return false;
    }

    for (i, &card) in cards.iter().enumerate() {
        if let Some(mv) = moving_index {
            if i == mv as usize {
                continue;
            }
        }
        if overlaps(card, bounds) {
            return false;
        }
    }

    true
}

pub fn try_find_free_position(
    cards: &[HomeCardBounds],
    column_span: i32,
    row_span: i32,
    row_count: i32,
    column_count: i32,
) -> Option<GridPosition> {
    if row_span > row_count || column_span > column_count || row_span <= 0 || column_span <= 0 {
        return None;
    }

    for row in 0..=(row_count - row_span) {
        for column in 0..=(column_count - column_span) {
            let bounds = HomeCardBounds {
                column,
                row,
                column_span,
                row_span,
            };
            if can_place(cards, None, bounds, row_count, column_count) {
                return Some(GridPosition { column, row });
            }
        }
    }
    None
}

pub fn clamp(bounds: HomeCardBounds, row_count: i32, column_count: i32) -> HomeCardBounds {
    let col_count = column_count.max(1);
    let r_count = row_count.max(1);

    let col_span = bounds.column_span.clamp(1, col_count);
    let r_span = bounds.row_span.clamp(1, r_count);

    HomeCardBounds {
        column: bounds.column.clamp(0, col_count - col_span),
        row: bounds.row.clamp(0, r_count - r_span),
        column_span: col_span,
        row_span: r_span,
    }
}

pub fn can_resize_grid(cards: &[HomeCardBounds], row_count: i32, column_count: i32) -> bool {
    cards.iter().all(|&c| is_within_grid(c, row_count, column_count))
}

pub fn try_find_best_fitting_free_position(
    cards: &[HomeCardBounds],
    preferred_column_span: i32,
    preferred_row_span: i32,
    row_count: i32,
    column_count: i32,
) -> Option<GridPlacement> {
    let max_col_span = preferred_column_span.min(column_count);
    let max_row_span = preferred_row_span.min(row_count);

    if max_col_span <= 0 || max_row_span <= 0 {
        return None;
    }

    let mut candidates = Vec::new();
    for h in 1..=max_row_span {
        for w in 1..=max_col_span {
            candidates.push((w, h));
        }
    }

    candidates.sort_by(|&(w1, h1), &(w2, h2)| {
        let area1 = w1 * h1;
        let area2 = w2 * h2;
        area2
            .cmp(&area1)
            .then_with(|| {
                let dist1 = (preferred_column_span - w1).abs() + (preferred_row_span - h1).abs();
                let dist2 = (preferred_column_span - w2).abs() + (preferred_row_span - h2).abs();
                dist1.cmp(&dist2)
            })
            .then_with(|| h2.cmp(&h1))
            .then_with(|| w2.cmp(&w1))
    });

    for (w, h) in candidates {
        if let Some(pos) = try_find_free_position(cards, w, h, row_count, column_count) {
            return Some(GridPlacement {
                column: pos.column,
                row: pos.row,
                column_span: w,
                row_span: h,
            });
        }
    }

    None
}

pub fn normalize_cards(
    cards: Vec<HomeCardBounds>,
    row_count: i32,
    column_count: i32,
) -> CardNormalizationResult {
    let mut changed = false;
    let mut occupied: Vec<HomeCardBounds> = Vec::new();
    let mut placed_cards = Vec::new();
    let mut removed_indices = Vec::new();

    for (idx, original_card) in cards.into_iter().enumerate() {
        let mut card = original_card;
        let clamped = clamp(card, row_count, column_count);
        if clamped != card {
            changed = true;
            card = clamped;
        }

        if can_place(&occupied, None, card, row_count, column_count) {
            occupied.push(card);
            placed_cards.push(CardNormalizationItem {
                index: idx as u32,
                bounds: card,
            });
            continue;
        }

        if let Some(pos) = try_find_free_position(
            &occupied,
            card.column_span,
            card.row_span,
            row_count,
            column_count,
        ) {
            card.column = pos.column;
            card.row = pos.row;
            occupied.push(card);
            placed_cards.push(CardNormalizationItem {
                index: idx as u32,
                bounds: card,
            });
            changed = true;
        } else if let Some(pos) = try_find_free_position(&occupied, 1, 1, row_count, column_count) {
            card.column = pos.column;
            card.row = pos.row;
            card.column_span = 1;
            card.row_span = 1;
            occupied.push(card);
            placed_cards.push(CardNormalizationItem {
                index: idx as u32,
                bounds: card,
            });
            changed = true;
        } else {
            removed_indices.push(idx as u32);
            changed = true;
        }
    }

    CardNormalizationResult {
        placed_cards,
        removed_indices,
        changed,
    }
}
