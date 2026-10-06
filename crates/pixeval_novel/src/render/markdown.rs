// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use crate::ast::{NovelDocument, NovelNode, NovelPage};

pub fn render_pages_markdown(
    doc: &NovelDocument,
    image_lookup: &HashMap<i64, String>,
    illust_lookup: &HashMap<(i64, i32), (String, String)>,
) -> Vec<String> {
    if doc.pages.is_empty() {
        return vec![String::new()];
    }

    doc.pages
        .iter()
        .map(|page| render_single_page_markdown(page, image_lookup, illust_lookup))
        .collect()
}

fn render_single_page_markdown(
    page: &NovelPage,
    image_lookup: &HashMap<i64, String>,
    illust_lookup: &HashMap<(i64, i32), (String, String)>,
) -> String {
    let mut out = String::new();
    out.push_str(&format!("<div id=\"page{}\" />\n\n", page.page_index));

    for node in &page.nodes {
        match node {
            NovelNode::Text { content } => {
                let replaced = normalize_newlines(content, "\n\n");
                out.push_str(&replaced);
            }
            NovelNode::Ruby { kanji, ruby_text } => {
                out.push_str(&format!(
                    "<ruby>{}<rp>（</rp><rt>{}</rt><rp>）</rp></ruby>",
                    html_escape(kanji),
                    html_escape(ruby_text)
                ));
            }
            NovelNode::JumpUri { title, uri } => {
                out.push_str(&format!("[{title}]({uri})"));
            }
            NovelNode::JumpPage { page } => {
                let page_idx = if *page > 0 { page - 1 } else { 0 };
                out.push_str(&format!("[第 {page} 页](page{page_idx})"));
            }
            NovelNode::Chapter { title } => {
                out.push_str(&format!("\n\n## {title}\n\n"));
            }
            NovelNode::UploadImage { image_id } => {
                if let Some(url) = image_lookup.get(image_id) {
                    out.push_str(&format!("\n\n![{image_id}]({url})\n\n"));
                }
            }
            NovelNode::PixivImage { illust_id, page } => {
                if let Some((thumb_url, app_uri)) = illust_lookup.get(&(*illust_id, *page)) {
                    out.push_str(&format!(
                        "\n\n[![{illust_id}-{page}]({thumb_url})]({app_uri})\n\n"
                    ));
                }
            }
        }
    }

    out
}

pub fn render_export_markdown(
    doc: &NovelDocument,
    cover_filename: Option<&str>,
    local_images: &HashMap<i64, String>,
    local_illusts: &HashMap<(i64, i32), (String, String)>,
) -> String {
    let mut out = String::new();

    if let Some(cover) = cover_filename {
        out.push_str(&format!("![cover]({cover})\n\n"));
    }

    for (i, page) in doc.pages.iter().enumerate() {
        if i > 0 {
            out.push_str("\n\n---\n\n");
        }
        out.push_str(&format!("<div id=\"page{}\" />\n\n", page.page_index));

        for node in &page.nodes {
            match node {
                NovelNode::Text { content } => {
                    let replaced = normalize_newlines(content, "\n\n");
                    out.push_str(&replaced);
                }
                NovelNode::Ruby { kanji, ruby_text } => {
                    out.push_str(&format!(
                        "<ruby>{}<rp>（</rp><rt>{}</rt><rp>）</rp></ruby>",
                        html_escape(kanji),
                        html_escape(ruby_text)
                    ));
                }
                NovelNode::JumpUri { title, uri } => {
                    out.push_str(&format!("[{title}]({uri})"));
                }
                NovelNode::JumpPage { page } => {
                    let page_idx = if *page > 0 { page - 1 } else { 0 };
                    out.push_str(&format!("[第 {page} 页](page{page_idx})"));
                }
                NovelNode::Chapter { title } => {
                    out.push_str(&format!("\n\n## {title}\n\n"));
                }
                NovelNode::UploadImage { image_id } => {
                    let filename = local_images
                        .get(image_id)
                        .cloned()
                        .unwrap_or_else(|| format!("{image_id}.jpg"));
                    out.push_str(&format!("\n\n![{image_id}]({filename})\n\n"));
                }
                NovelNode::PixivImage { illust_id, page } => {
                    let (filename, web_uri) = local_illusts
                        .get(&(*illust_id, *page))
                        .cloned()
                        .unwrap_or_else(|| {
                            (
                                format!("{illust_id}-{page}.jpg"),
                                format!("https://www.pixiv.net/artworks/{illust_id}"),
                            )
                        });
                    out.push_str(&format!(
                        "\n\n[![{illust_id}-{page}]({filename})]({web_uri})\n\n"
                    ));
                }
            }
        }
    }

    out
}

fn normalize_newlines(s: &str, replacement: &str) -> String {
    let s = s.replace("\r\n", "\n").replace('\r', "\n");
    s.replace('\n', replacement)
}

fn html_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
}
