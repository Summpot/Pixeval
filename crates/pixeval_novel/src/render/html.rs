// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use crate::ast::{NovelDocument, NovelNode};

pub fn render_export_html(
    doc: &NovelDocument,
    title: &str,
    cover_filename: Option<&str>,
    local_images: &HashMap<i64, String>,
    local_illusts: &HashMap<(i64, i32), (String, String)>,
) -> String {
    let mut out = String::new();
    out.push_str("<!DOCTYPE html>\n<html lang=\"ja\">\n<head>\n<meta charset=\"utf-8\"/>\n");
    out.push_str(&format!("<title>{}</title>\n", html_escape(title)));
    out.push_str("<style>\n");
    out.push_str("body { font-family: sans-serif; line-height: 1.8; margin: 40px auto; max-width: 800px; padding: 0 20px; color: #222; }\n");
    out.push_str("img { max-width: 100%; height: auto; display: block; margin: 20px auto; }\n");
    out.push_str("h2 { border-bottom: 1px solid #ccc; padding-bottom: 8px; margin: 30px 0; }\n");
    out.push_str("hr { border: 0; border-top: 1px dashed #ccc; margin: 40px 0; }\n");
    out.push_str("ruby rt { font-size: 0.65em; }\n");
    out.push_str("</style>\n</head>\n<body>\n");

    if let Some(cover) = cover_filename {
        out.push_str(&format!("<img src=\"{}\" alt=\"cover\" /><br/><br/>\n", html_escape(cover)));
    }

    for (i, page) in doc.pages.iter().enumerate() {
        if i > 0 {
            out.push_str("\n<hr/>\n");
        }
        out.push_str(&format!("<div id=\"page{}\" /><br/>\n", page.page_index));

        for node in &page.nodes {
            match node {
                NovelNode::Text { content } => {
                    let escaped = html_escape(content);
                    let with_breaks = escaped.replace("\r\n", "<br/>\n").replace('\r', "<br/>\n").replace('\n', "<br/>\n");
                    out.push_str(&format!("<span>{with_breaks}</span>"));
                }
                NovelNode::Ruby { kanji, ruby_text } => {
                    out.push_str(&format!(
                        "<ruby>{}<rp>（</rp><rt>{}</rt><rp>）</rp></ruby>",
                        html_escape(kanji),
                        html_escape(ruby_text)
                    ));
                }
                NovelNode::JumpUri { title, uri } => {
                    out.push_str(&format!(
                        "<a href=\"{}\">{}</a>",
                        html_escape(uri),
                        html_escape(title)
                    ));
                }
                NovelNode::JumpPage { page } => {
                    let page_idx = if *page > 0 { page - 1 } else { 0 };
                    out.push_str(&format!("<a href=\"page{page_idx}\">第 {page} 页</a>"));
                }
                NovelNode::Chapter { title } => {
                    out.push_str(&format!(
                        "<br/><br/>\n<h2>{}</h2>\n<br/><br/>\n",
                        html_escape(title)
                    ));
                }
                NovelNode::UploadImage { image_id } => {
                    let filename = local_images
                        .get(image_id)
                        .cloned()
                        .unwrap_or_else(|| format!("{image_id}.jpg"));
                    out.push_str(&format!(
                        "<br/><br/>\n<img src=\"{}\" alt=\"{image_id}\" />\n<br/><br/>\n",
                        html_escape(&filename)
                    ));
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
                        "<br/><br/>\n<a href=\"{}\"><img src=\"{}\" alt=\"{illust_id}-{page}\" /></a>\n<br/><br/>\n",
                        html_escape(&web_uri),
                        html_escape(&filename)
                    ));
                }
            }
        }
    }

    out.push_str("\n</body>\n</html>\n");
    out
}

fn html_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
}
