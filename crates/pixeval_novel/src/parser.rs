// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::ast::{NovelDocument, NovelNode, NovelPage};

const PIXIV_IMAGE_TOKEN: &str = "[pixivimage:";
const UPLOADED_IMAGE_TOKEN: &str = "[uploadimage:";
const CHAPTER_TOKEN: &str = "[chapter:";
const JUMP_TOKEN: &str = "[jump:";
const JUMP_URI_TOKEN: &str = "[[jumpuri:";
const RUBY_TOKEN: &str = "[[rb:";
const NEW_PAGE_TOKEN: &str = "[newpage]";
const SEPARATOR_TOKEN: char = '>';
const END_SINGLE_TOKEN: char = ']';
const END_DOUBLE_TOKEN: &str = "]]";

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum TokenKind {
    None,
    PixivImage,
    UploadedImage,
    Chapter,
    Jump,
    JumpUri,
    Ruby,
    NewPage,
}

pub struct NovelParser;

impl NovelParser {
    pub fn parse(text: &str) -> NovelDocument {
        let mut pages = Vec::new();
        let mut current_nodes = Vec::new();
        let mut current_page_index = 0u32;

        let mut position = 0;
        let mut run_start = 0;
        let bytes = text.as_bytes();
        let len = bytes.len();

        while position < len {
            // Find next '['
            let rel = match text[position..].find('[') {
                Some(idx) => idx,
                None => break,
            };

            let bracket_pos = position + rel;
            if bracket_pos > run_start {
                current_nodes.push(NovelNode::Text {
                    content: text[run_start..bracket_pos].to_string(),
                });
            }
            position = bracket_pos;
            run_start = position;

            let remaining = &text[position..];
            let (kind, token_len) = match_token(remaining);

            match kind {
                TokenKind::None => {
                    position += 1;
                }
                TokenKind::NewPage => {
                    position += token_len;
                    run_start = position;
                    pages.push(NovelPage {
                        page_index: current_page_index,
                        nodes: std::mem::take(&mut current_nodes),
                    });
                    current_page_index += 1;
                }
                TokenKind::Ruby => {
                    if let Some((node, end_offset)) = parse_ruby(remaining, token_len) {
                        current_nodes.push(node);
                        position += end_offset;
                        run_start = position;
                    } else {
                        position += 1;
                    }
                }
                TokenKind::JumpUri => {
                    if let Some((node, end_offset)) = parse_jump_uri(remaining, token_len) {
                        current_nodes.push(node);
                        position += end_offset;
                        run_start = position;
                    } else {
                        position += 1;
                    }
                }
                TokenKind::Jump => {
                    if let Some((node, end_offset)) = parse_jump(remaining, token_len) {
                        current_nodes.push(node);
                        position += end_offset;
                        run_start = position;
                    } else {
                        position += 1;
                    }
                }
                TokenKind::Chapter => {
                    if let Some((node, end_offset)) = parse_chapter(remaining, token_len) {
                        current_nodes.push(node);
                        position += end_offset;
                        run_start = position;
                    } else {
                        position += 1;
                    }
                }
                TokenKind::UploadedImage => {
                    if let Some((node, end_offset)) = parse_uploaded_image(remaining, token_len) {
                        current_nodes.push(node);
                        position += end_offset;
                        run_start = position;
                    } else {
                        position += 1;
                    }
                }
                TokenKind::PixivImage => {
                    if let Some((node, end_offset)) = parse_pixiv_image(remaining, token_len) {
                        current_nodes.push(node);
                        position += end_offset;
                        run_start = position;
                    } else {
                        position += 1;
                    }
                }
            }
        }

        if run_start < len {
            current_nodes.push(NovelNode::Text {
                content: text[run_start..len].to_string(),
            });
        }

        pages.push(NovelPage {
            page_index: current_page_index,
            nodes: current_nodes,
        });

        NovelDocument { pages }
    }
}

fn match_token(s: &str) -> (TokenKind, usize) {
    if s.len() < 2 {
        return (TokenKind::None, 0);
    }

    if s.starts_with(JUMP_URI_TOKEN) {
        return (TokenKind::JumpUri, JUMP_URI_TOKEN.len());
    }
    if s.starts_with(RUBY_TOKEN) {
        return (TokenKind::Ruby, RUBY_TOKEN.len());
    }
    if s.starts_with(PIXIV_IMAGE_TOKEN) {
        return (TokenKind::PixivImage, PIXIV_IMAGE_TOKEN.len());
    }
    if s.starts_with(UPLOADED_IMAGE_TOKEN) {
        return (TokenKind::UploadedImage, UPLOADED_IMAGE_TOKEN.len());
    }
    if s.starts_with("[uploadedimage:") {
        return (TokenKind::UploadedImage, "[uploadedimage:".len());
    }
    if s.starts_with(CHAPTER_TOKEN) {
        return (TokenKind::Chapter, CHAPTER_TOKEN.len());
    }
    if s.starts_with(JUMP_TOKEN) {
        return (TokenKind::Jump, JUMP_TOKEN.len());
    }
    if s.starts_with(NEW_PAGE_TOKEN) {
        return (TokenKind::NewPage, NEW_PAGE_TOKEN.len());
    }

    (TokenKind::None, 0)
}

fn parse_ruby(s: &str, token_len: usize) -> Option<(NovelNode, usize)> {
    let sep_idx = s[token_len..].find(SEPARATOR_TOKEN)?;
    let end_idx = s[token_len + sep_idx..].find(END_DOUBLE_TOKEN)?;

    let kanji_raw = &s[token_len..token_len + sep_idx];
    let ruby_raw = &s[token_len + sep_idx + 1..token_len + sep_idx + end_idx];

    let kanji = kanji_raw.trim().to_string();
    let ruby = ruby_raw.trim().to_string();
    let total_len = token_len + sep_idx + end_idx + END_DOUBLE_TOKEN.len();

    Some((NovelNode::Ruby { kanji, ruby_text: ruby }, total_len))
}

fn parse_jump_uri(s: &str, token_len: usize) -> Option<(NovelNode, usize)> {
    let sep_idx = s[token_len..].find(SEPARATOR_TOKEN)?;
    let end_idx = s[token_len + sep_idx..].find(END_DOUBLE_TOKEN)?;

    let text_raw = &s[token_len..token_len + sep_idx];
    let uri_raw = &s[token_len + sep_idx + 1..token_len + sep_idx + end_idx];

    let text = text_raw.trim().to_string();
    let uri = uri_raw.trim().to_string();

    if !(uri.starts_with("http://") || uri.starts_with("https://")) {
        return None;
    }

    let total_len = token_len + sep_idx + end_idx + END_DOUBLE_TOKEN.len();
    Some((NovelNode::JumpUri { title: text, uri }, total_len))
}

fn parse_jump(s: &str, token_len: usize) -> Option<(NovelNode, usize)> {
    let end_idx = s[token_len..].find(END_SINGLE_TOKEN)?;
    let page_str = &s[token_len..token_len + end_idx];
    let page = page_str.parse::<u32>().ok()?;
    let total_len = token_len + end_idx + 1;
    Some((NovelNode::JumpPage { page }, total_len))
}

fn parse_chapter(s: &str, token_len: usize) -> Option<(NovelNode, usize)> {
    let end_idx = s[token_len..].find(END_SINGLE_TOKEN)?;
    let chapter_title = s[token_len..token_len + end_idx].trim().to_string();
    let total_len = token_len + end_idx + 1;
    Some((NovelNode::Chapter { title: chapter_title }, total_len))
}

fn parse_uploaded_image(s: &str, token_len: usize) -> Option<(NovelNode, usize)> {
    let end_idx = s[token_len..].find(END_SINGLE_TOKEN)?;
    let id_str = &s[token_len..token_len + end_idx];
    let image_id = id_str.parse::<i64>().ok()?;
    let total_len = token_len + end_idx + 1;
    Some((NovelNode::UploadImage { image_id }, total_len))
}

fn parse_pixiv_image(s: &str, token_len: usize) -> Option<(NovelNode, usize)> {
    let end_idx = s[token_len..].find(END_SINGLE_TOKEN)?;
    let inner = &s[token_len..token_len + end_idx];

    let mut parts = inner.split('-');
    let id_part = parts.next()?;
    let page_part = parts.next();

    if parts.next().is_some() {
        return None;
    }

    let illust_id = id_part.parse::<i64>().ok()?;
    let page = match page_part {
        Some(p) => p.parse::<i32>().ok()?,
        None => 1,
    };

    let total_len = token_len + end_idx + 1;
    Some((NovelNode::PixivImage { illust_id, page }, total_len))
}
