// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod epub;
pub mod html;
pub mod markdown;

// Render emphasis by grapheme so emoji and combining characters stay together.
fn render_emphasis(content: &str, mark: &str) -> String {
    use unicode_segmentation::UnicodeSegmentation;
    content.graphemes(true).map(|element| {
        if element.chars().all(char::is_whitespace) {
            element.to_string()
        } else {
            format!("<ruby style=\"ruby-position: under\">{}<rp>（</rp><rt>{}</rt><rp>）</rp></ruby>",
                markdown::html_escape(element), markdown::html_escape(mark))
        }
    }).collect()
}
