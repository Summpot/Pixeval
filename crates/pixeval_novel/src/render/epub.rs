// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashMap;
use std::io::{Cursor, Write};
use zip::write::SimpleFileOptions;
use zip::CompressionMethod;
use zip::ZipWriter;

use crate::ast::{NovelDocument, NovelNode};

#[derive(Clone, Debug, Default)]
pub struct EpubMetadata {
    pub id: i64,
    pub title: String,
    pub author: String,
    pub description: String,
    pub language: String,
    pub cover_filename: Option<String>,
}

pub fn build_epub(
    metadata: EpubMetadata,
    doc: &NovelDocument,
    images: &HashMap<String, Vec<u8>>,
) -> Result<Vec<u8>, String> {
    let mut buffer = Cursor::new(Vec::new());
    let mut zip = ZipWriter::new(&mut buffer);

    let stored_opts = SimpleFileOptions::default().compression_method(CompressionMethod::Stored);
    let deflated_opts = SimpleFileOptions::default().compression_method(CompressionMethod::Deflated);

    // 1. mimetype (Must be first, stored uncompressed)
    zip.start_file("mimetype", stored_opts)
        .map_err(|e| format!("Failed to write mimetype: {e}"))?;
    zip.write_all(b"application/epub+zip")
        .map_err(|e| format!("Failed to write mimetype bytes: {e}"))?;

    // 2. META-INF/container.xml
    zip.start_file("META-INF/container.xml", deflated_opts)
        .map_err(|e| format!("Failed to start container.xml: {e}"))?;
    let container_xml = r#"<?xml version="1.0" encoding="UTF-8"?>
<container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
  <rootfiles>
    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
  </rootfiles>
</container>"#;
    zip.write_all(container_xml.as_bytes())
        .map_err(|e| format!("Failed to write container.xml: {e}"))?;

    // 3. OEBPS/style.css
    zip.start_file("OEBPS/style.css", deflated_opts)
        .map_err(|e| format!("Failed to start style.css: {e}"))?;
    let style_css = r#"body {
  font-family: serif;
  line-height: 1.8;
  margin: 5%;
  padding: 0;
  text-align: justify;
}
p {
  text-indent: 2em;
  margin: 0.5em 0;
}
h2 {
  text-align: center;
  margin: 1.5em 0 1em 0;
}
img {
  max-width: 100%;
  height: auto;
  display: block;
  margin: 1em auto;
}
.cover {
  text-align: center;
  padding: 0;
  margin: 0;
}
ruby rt {
  font-size: 0.65em;
}"#;
    zip.write_all(style_css.as_bytes())
        .map_err(|e| format!("Failed to write style.css: {e}"))?;

    // 4. Write image assets into OEBPS/images/
    let mut image_manifest_entries = Vec::new();
    for (img_name, bytes) in images {
        let zip_path = format!("OEBPS/images/{img_name}");
        zip.start_file(&zip_path, deflated_opts)
            .map_err(|e| format!("Failed to start image {img_name}: {e}"))?;
        zip.write_all(bytes)
            .map_err(|e| format!("Failed to write image {img_name}: {e}"))?;

        let mime = detect_mime(img_name);
        let is_cover = metadata
            .cover_filename
            .as_deref()
            .map(|c| c == img_name)
            .unwrap_or(false);
        image_manifest_entries.push((img_name.clone(), mime, is_cover));
    }

    // 5. Write pages (OEBPS/page_{i}.xhtml)
    let page_count = doc.pages.len().max(1);
    let mut chapter_titles = Vec::new();

    for (i, page) in doc.pages.iter().enumerate() {
        let page_filename = format!("OEBPS/page_{i}.xhtml");
        zip.start_file(&page_filename, deflated_opts)
            .map_err(|e| format!("Failed to start {page_filename}: {e}"))?;

        let mut page_title = format!("Page {}", i + 1);
        let mut xhtml = String::new();
        xhtml.push_str("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        xhtml.push_str("<!DOCTYPE html>\n");
        xhtml.push_str("<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\">\n");
        xhtml.push_str("<head>\n<meta charset=\"utf-8\"/>\n");
        xhtml.push_str("<link rel=\"stylesheet\" type=\"text/css\" href=\"style.css\"/>\n");
        xhtml.push_str(&format!("<title>Page {}</title>\n</head>\n<body>\n", i + 1));

        xhtml.push_str("<section epub:type=\"chapter\">\n");

        for node in &page.nodes {
            match node {
                NovelNode::Text { content } => {
                    let escaped = xml_escape(content);
                    for line in escaped.lines() {
                        let trimmed = line.trim();
                        if !trimmed.is_empty() {
                            xhtml.push_str(&format!("<p>{trimmed}</p>\n"));
                        }
                    }
                }
                NovelNode::Ruby { kanji, ruby_text } => {
                    xhtml.push_str(&format!(
                        "<ruby>{}<rp>（</rp><rt>{}</rt><rp>）</rp></ruby>",
                        xml_escape(kanji),
                        xml_escape(ruby_text)
                    ));
                }
                NovelNode::JumpUri { title, uri } => {
                    xhtml.push_str(&format!(
                        "<a href=\"{}\">{}</a>",
                        xml_escape(uri),
                        xml_escape(title)
                    ));
                }
                NovelNode::JumpPage { page: jump_p } => {
                    let target_idx = if *jump_p > 0 { jump_p - 1 } else { 0 };
                    xhtml.push_str(&format!(
                        "<a href=\"page_{target_idx}.xhtml\">第 {jump_p} 页</a>"
                    ));
                }
                NovelNode::Chapter { title } => {
                    page_title = title.clone();
                    xhtml.push_str(&format!("<h2>{}</h2>\n", xml_escape(title)));
                }
                NovelNode::UploadImage { image_id } => {
                    let img_name = format!("{image_id}.jpg");
                    if images.contains_key(&img_name) {
                        xhtml.push_str(&format!(
                            "<img src=\"images/{img_name}\" alt=\"{image_id}\"/>\n"
                        ));
                    }
                }
                NovelNode::PixivImage { illust_id, page: img_p } => {
                    let img_name = format!("{illust_id}-{img_p}.jpg");
                    if images.contains_key(&img_name) {
                        xhtml.push_str(&format!(
                            "<img src=\"images/{img_name}\" alt=\"{illust_id}-{img_p}\"/>\n"
                        ));
                    }
                }
            }
        }

        xhtml.push_str("</section>\n</body>\n</html>\n");
        zip.write_all(xhtml.as_bytes())
            .map_err(|e| format!("Failed to write page {i}: {e}"))?;
        chapter_titles.push(page_title);
    }

    // 6. Write OEBPS/nav.xhtml
    zip.start_file("OEBPS/nav.xhtml", deflated_opts)
        .map_err(|e| format!("Failed to start nav.xhtml: {e}"))?;
    let mut nav_xhtml = String::new();
    nav_xhtml.push_str("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE html>\n");
    nav_xhtml.push_str("<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\">\n");
    nav_xhtml.push_str("<head><meta charset=\"utf-8\"/><title>Table of Contents</title></head>\n<body>\n");
    nav_xhtml.push_str("<nav epub:type=\"toc\" id=\"toc\">\n<h1>目录</h1>\n<ol>\n");
    for (i, title) in chapter_titles.iter().enumerate() {
        nav_xhtml.push_str(&format!(
            "<li><a href=\"page_{i}.xhtml\">{}</a></li>\n",
            xml_escape(title)
        ));
    }
    nav_xhtml.push_str("</ol>\n</nav>\n</body>\n</html>\n");
    zip.write_all(nav_xhtml.as_bytes())
        .map_err(|e| format!("Failed to write nav.xhtml: {e}"))?;

    // 7. Write OEBPS/toc.ncx
    zip.start_file("OEBPS/toc.ncx", deflated_opts)
        .map_err(|e| format!("Failed to start toc.ncx: {e}"))?;
    let mut ncx = String::new();
    ncx.push_str("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
    ncx.push_str("<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\">\n");
    ncx.push_str("<head>\n");
    ncx.push_str(&format!(
        "<meta name=\"dtb:uid\" content=\"urn:pixiv:novel:{}\"/>\n",
        metadata.id
    ));
    ncx.push_str("<meta name=\"dtb:depth\" content=\"1\"/>\n");
    ncx.push_str("<meta name=\"dtb:totalPageCount\" content=\"0\"/>\n");
    ncx.push_str("<meta name=\"dtb:maxPageNumber\" content=\"0\"/>\n");
    ncx.push_str("</head>\n");
    ncx.push_str(&format!(
        "<docTitle><text>{}</text></docTitle>\n<navMap>\n",
        xml_escape(&metadata.title)
    ));
    for (i, title) in chapter_titles.iter().enumerate() {
        ncx.push_str(&format!(
            "<navPoint id=\"nav_{i}\" playOrder=\"{}\">\n<navLabel><text>{}</text></navLabel>\n<content src=\"page_{i}.xhtml\"/>\n</navPoint>\n",
            i + 1,
            xml_escape(title)
        ));
    }
    ncx.push_str("</navMap>\n</ncx>\n");
    zip.write_all(ncx.as_bytes())
        .map_err(|e| format!("Failed to write toc.ncx: {e}"))?;

    // 8. Write OEBPS/content.opf
    zip.start_file("OEBPS/content.opf", deflated_opts)
        .map_err(|e| format!("Failed to start content.opf: {e}"))?;
    let mut opf = String::new();
    opf.push_str("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
    opf.push_str("<package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\" unique-identifier=\"pub-id\">\n");
    opf.push_str("<metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n");
    opf.push_str(&format!(
        "<dc:identifier id=\"pub-id\">urn:pixiv:novel:{}</dc:identifier>\n",
        metadata.id
    ));
    opf.push_str(&format!(
        "<dc:title>{}</dc:title>\n",
        xml_escape(&metadata.title)
    ));
    opf.push_str(&format!(
        "<dc:creator>{}</dc:creator>\n",
        xml_escape(&metadata.author)
    ));
    opf.push_str(&format!(
        "<dc:description>{}</dc:description>\n",
        xml_escape(&metadata.description)
    ));
    let lang = if metadata.language.is_empty() {
        "ja"
    } else {
        &metadata.language
    };
    opf.push_str(&format!("<dc:language>{lang}</dc:language>\n"));
    opf.push_str("<meta property=\"dcterms:modified\">2026-10-06T00:00:00Z</meta>\n");
    opf.push_str("</metadata>\n");

    opf.push_str("<manifest>\n");
    opf.push_str("<item id=\"style\" href=\"style.css\" media-type=\"text/css\"/>\n");
    opf.push_str("<item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>\n");
    opf.push_str("<item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/>\n");

    for i in 0..page_count {
        opf.push_str(&format!(
            "<item id=\"page_{i}\" href=\"page_{i}.xhtml\" media-type=\"application/xhtml+xml\"/>\n"
        ));
    }

    for (idx, (name, mime, is_cover)) in image_manifest_entries.iter().enumerate() {
        let props = if *is_cover {
            " properties=\"cover-image\""
        } else {
            ""
        };
        opf.push_str(&format!(
            "<item id=\"img_{idx}\" href=\"images/{name}\" media-type=\"{mime}\"{props}/>\n"
        ));
    }
    opf.push_str("</manifest>\n");

    opf.push_str("<spine toc=\"ncx\">\n");
    for i in 0..page_count {
        opf.push_str(&format!("<itemref idref=\"page_{i}\"/>\n"));
    }
    opf.push_str("</spine>\n");
    opf.push_str("</package>\n");

    zip.write_all(opf.as_bytes())
        .map_err(|e| format!("Failed to write content.opf: {e}"))?;

    zip.finish().map_err(|e| format!("Failed to finalize zip: {e}"))?;

    Ok(buffer.into_inner())
}

fn detect_mime(filename: &str) -> &'static str {
    if filename.ends_with(".png") {
        "image/png"
    } else if filename.ends_with(".webp") {
        "image/webp"
    } else if filename.ends_with(".gif") {
        "image/gif"
    } else {
        "image/jpeg"
    }
}

fn xml_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
        .replace('\'', "&apos;")
}
