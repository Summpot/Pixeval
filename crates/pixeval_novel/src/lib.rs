uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod ast;
pub mod parser;
pub mod render;

use std::collections::HashMap;
use std::sync::Arc;

pub use ast::*;
pub use parser::*;
pub use render::epub::*;
pub use render::html::*;
pub use render::markdown::*;

#[derive(uniffi::Record, Clone, Debug, Default)]
pub struct NovelImageRenderDto {
    pub image_id: i64,
    pub url: String,
    pub filename: String,
}

#[derive(uniffi::Record, Clone, Debug, Default)]
pub struct NovelIllustRenderDto {
    pub illust_id: i64,
    pub page: i32,
    pub url: String,
    pub app_uri: String,
    pub web_uri: String,
    pub filename: String,
}

#[derive(uniffi::Record, Clone, Debug, Default)]
pub struct NovelEpubMetadataDto {
    pub id: i64,
    pub title: String,
    pub author: String,
    pub description: String,
    pub language: String,
    pub cover_filename: Option<String>,
}

#[derive(uniffi::Error, thiserror::Error, Debug)]
pub enum NovelError {
    #[error("Render error: {message}")]
    RenderError { message: String },
    #[error("EPUB build error: {message}")]
    EpubError { message: String },
    #[error("IO error: {message}")]
    IoError { message: String },
}

#[derive(uniffi::Object, Default)]
pub struct NovelEngine;

#[uniffi::export]
impl NovelEngine {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Arc::new(Self)
    }

    /// Parses novel raw markup text into a structured AST document
    pub fn parse(&self, text: String) -> NovelDocument {
        NovelParser::parse(&text)
    }

    /// Renders markdown pages for Avalonia UI display
    pub fn render_pages_markdown(
        &self,
        text: String,
        images: Vec<NovelImageRenderDto>,
        illusts: Vec<NovelIllustRenderDto>,
    ) -> Vec<String> {
        let doc = NovelParser::parse(&text);
        let mut img_lookup = HashMap::new();
        for img in images {
            img_lookup.insert(img.image_id, img.url);
        }
        let mut ill_lookup = HashMap::new();
        for ill in illusts {
            ill_lookup.insert((ill.illust_id, ill.page), (ill.url, ill.app_uri));
        }
        render_pages_markdown(&doc, &img_lookup, &ill_lookup)
    }

    /// Renders full markdown document for file export
    pub fn render_export_markdown(
        &self,
        doc: &NovelDocument,
        cover_filename: Option<String>,
        images: Vec<NovelImageRenderDto>,
        illusts: Vec<NovelIllustRenderDto>,
    ) -> String {
        let mut img_lookup = HashMap::new();
        for img in images {
            img_lookup.insert(img.image_id, img.filename);
        }
        let mut ill_lookup = HashMap::new();
        for ill in illusts {
            ill_lookup.insert((ill.illust_id, ill.page), (ill.filename, ill.web_uri));
        }
        render_export_markdown(doc, cover_filename.as_deref(), &img_lookup, &ill_lookup)
    }

    /// Renders semantic HTML document for file export
    pub fn render_export_html(
        &self,
        doc: &NovelDocument,
        title: String,
        cover_filename: Option<String>,
        images: Vec<NovelImageRenderDto>,
        illusts: Vec<NovelIllustRenderDto>,
    ) -> String {
        let mut img_lookup = HashMap::new();
        for img in images {
            img_lookup.insert(img.image_id, img.filename);
        }
        let mut ill_lookup = HashMap::new();
        for ill in illusts {
            ill_lookup.insert((ill.illust_id, ill.page), (ill.filename, ill.web_uri));
        }
        render_export_html(doc, &title, cover_filename.as_deref(), &img_lookup, &ill_lookup)
    }

    /// Builds an EPUB 3.0 electronic book
    pub fn build_epub(
        &self,
        metadata: NovelEpubMetadataDto,
        doc: &NovelDocument,
        images: HashMap<String, Vec<u8>>,
    ) -> Result<Vec<u8>, NovelError> {
        let meta = EpubMetadata {
            id: metadata.id,
            title: metadata.title,
            author: metadata.author,
            description: metadata.description,
            language: metadata.language,
            cover_filename: metadata.cover_filename,
        };
        build_epub(meta, doc, &images).map_err(|e| NovelError::EpubError { message: e })
    }

    /// Renders full markdown document for file export directly from text
    pub fn render_export_markdown_from_text(
        &self,
        text: String,
        cover_filename: Option<String>,
        images: Vec<NovelImageRenderDto>,
        illusts: Vec<NovelIllustRenderDto>,
    ) -> String {
        let doc = NovelParser::parse(&text);
        self.render_export_markdown(&doc, cover_filename, images, illusts)
    }

    /// Renders semantic HTML document for file export directly from text
    pub fn render_export_html_from_text(
        &self,
        text: String,
        title: String,
        cover_filename: Option<String>,
        images: Vec<NovelImageRenderDto>,
        illusts: Vec<NovelIllustRenderDto>,
    ) -> String {
        let doc = NovelParser::parse(&text);
        self.render_export_html(&doc, title, cover_filename, images, illusts)
    }

    /// Builds an EPUB 3.0 electronic book directly from text
    pub fn build_epub_from_text(
        &self,
        metadata: NovelEpubMetadataDto,
        text: String,
        images: HashMap<String, Vec<u8>>,
    ) -> Result<Vec<u8>, NovelError> {
        let doc = NovelParser::parse(&text);
        self.build_epub(metadata, &doc, images)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_pixiv_tokens() {
        let text = "第一段文字\n[[rb:博麗霊夢 > はくれいれいむ]]が空を飞ぶ。\n[chapter:第一章]\n[[jumpuri:Pixiv > https://www.pixiv.net]]\n[jump:2]\n[uploadimage:12345]\n[pixivimage:67890-2]\n[newpage]\n第二页内容。";
        let doc = NovelParser::parse(text);

        assert_eq!(doc.pages.len(), 2);
        let page0 = &doc.pages[0];
        assert_eq!(page0.page_index, 0);

        let mut found_ruby = false;
        let mut found_chapter = false;
        let mut found_jumpuri = false;
        let mut found_jump = false;
        let mut found_upload = false;
        let mut found_pixiv = false;

        for node in &page0.nodes {
            match node {
                NovelNode::Ruby { kanji, ruby_text } => {
                    assert_eq!(kanji, "博麗霊夢");
                    assert_eq!(ruby_text, "はくれいれいむ");
                    found_ruby = true;
                }
                NovelNode::Chapter { title } => {
                    assert_eq!(title, "第一章");
                    found_chapter = true;
                }
                NovelNode::JumpUri { title, uri } => {
                    assert_eq!(title, "Pixiv");
                    assert_eq!(uri, "https://www.pixiv.net");
                    found_jumpuri = true;
                }
                NovelNode::JumpPage { page } => {
                    assert_eq!(*page, 2);
                    found_jump = true;
                }
                NovelNode::UploadImage { image_id } => {
                    assert_eq!(*image_id, 12345);
                    found_upload = true;
                }
                NovelNode::PixivImage { illust_id, page } => {
                    assert_eq!(*illust_id, 67890);
                    assert_eq!(*page, 2);
                    found_pixiv = true;
                }
                _ => {}
            }
        }

        assert!(found_ruby);
        assert!(found_chapter);
        assert!(found_jumpuri);
        assert!(found_jump);
        assert!(found_upload);
        assert!(found_pixiv);

        let page1 = &doc.pages[1];
        assert_eq!(page1.page_index, 1);
        assert_eq!(page1.nodes.len(), 1);
        if let NovelNode::Text { content } = &page1.nodes[0] {
            assert!(content.contains("第二页内容"));
        } else {
            panic!("Expected text node on page 1");
        }
    }

    #[test]
    fn test_render_markdown_and_html() {
        let engine = NovelEngine::new();
        let text = "前言[chapter:序章]\n[[rb:漢字 > かんじ]][newpage]第二页";
        let pages_md = engine.render_pages_markdown(text.to_string(), vec![], vec![]);
        assert_eq!(pages_md.len(), 2);
        assert!(pages_md[0].contains("## 序章"));
        assert!(pages_md[0].contains("<ruby>漢字<rp>（</rp><rt>かんじ</rt><rp>）</rp></ruby>"));

        let doc = engine.parse(text.to_string());
        let full_html = engine.render_export_html(
            &doc,
            "测试小说".to_string(),
            Some("cover.jpg".to_string()),
            vec![],
            vec![],
        );
        assert!(full_html.contains("<title>测试小说</title>"));
        assert!(full_html.contains("<h2>序章</h2>"));
        assert!(full_html.contains("<ruby>漢字<rp>（</rp><rt>かんじ</rt><rp>）</rp></ruby>"));
        assert!(full_html.contains("<hr/>"));
    }

    #[test]
    fn test_build_epub() {
        let engine = NovelEngine::new();
        let text = "小说正文[chapter:开端]\n测试EPUB生成[newpage]第二章内容";
        let doc = engine.parse(text.to_string());
        let meta = NovelEpubMetadataDto {
            id: 11111,
            title: "EPUB测试".to_string(),
            author: "测试作者".to_string(),
            description: "描述".to_string(),
            language: "zh".to_string(),
            cover_filename: None,
        };
        let epub_bytes = engine
            .build_epub(meta, &doc, HashMap::new())
            .expect("EPUB build should succeed");
        assert!(!epub_bytes.is_empty());
        // Verify mimetype header in zip
        assert_eq!(&epub_bytes[0..2], b"PK");
    }
}
