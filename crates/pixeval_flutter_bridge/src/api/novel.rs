use crate::state::APP_STATE;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NovelDetailDto {
    pub id: i64,
    pub title: String,
    pub caption: String,
    pub cover_url: String,
    pub user_id: i64,
    pub user_name: String,
    pub user_account: String,
    pub user_avatar_url: String,
    pub page_count: i32,
    pub text_length: i32,
    pub is_bookmarked: bool,
    pub total_bookmarks: i64,
    pub tags: Vec<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NovelChapterDto {
    pub page_index: i32,
    pub title: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NovelContentDto {
    pub id: i64,
    pub pages: Vec<String>,
    pub total_pages: i32,
    pub chapters: Vec<NovelChapterDto>,
}

pub async fn get_novel_detail(novel_id: i64) -> Result<NovelDetailDto, String> {
    let client = &APP_STATE.client;
    let n = client
        .get_novel(novel_id)
        .await
        .map_err(|e| format!("Failed to get novel detail: {e}"))?;

    let tags = n.tags.into_iter().map(|t| t.name).collect();
    let cover_url = n.image_urls.large.or(n.image_urls.medium).unwrap_or_default();
    let user_avatar = n.user.profile_image_urls.medium.unwrap_or_default();

    Ok(NovelDetailDto {
        id: n.id,
        title: n.title,
        caption: n.caption,
        cover_url,
        user_id: n.user.id,
        user_name: n.user.name,
        user_account: n.user.account,
        user_avatar_url: user_avatar,
        page_count: n.page_count,
        text_length: n.text_length,
        is_bookmarked: n.is_bookmarked,
        total_bookmarks: n.total_bookmarks,
        tags,
    })
}

pub async fn get_novel_content_rendered(novel_id: i64) -> Result<NovelContentDto, String> {
    let client = &APP_STATE.client;
    let raw_text = client
        .get_novel_content(novel_id)
        .await
        .map_err(|e| format!("Failed to get novel content: {e}"))?;

    let doc = pixeval_novel::parse_novel(raw_text);
    let mut pages = Vec::new();
    let mut chapters = Vec::new();

    for page in doc.pages {
        let mut page_text = String::new();
        for node in page.nodes {
            match node {
                pixeval_novel::NovelNode::Text { content } => {
                    page_text.push_str(&content);
                }
                pixeval_novel::NovelNode::Ruby { kanji, ruby_text } => {
                    page_text.push_str(&format!("{kanji}({ruby_text})"));
                }
                pixeval_novel::NovelNode::JumpUri { title, uri: _ } => {
                    page_text.push_str(&title);
                }
                pixeval_novel::NovelNode::JumpPage { page } => {
                    page_text.push_str(&format!("(第 {page} 页)"));
                }
                pixeval_novel::NovelNode::Chapter { title } => {
                    chapters.push(NovelChapterDto {
                        page_index: page.page_index as i32,
                        title: title.clone(),
                    });
                    page_text.push_str(&format!("\n\n{title}\n\n"));
                }
                pixeval_novel::NovelNode::UploadImage { image_id } => {
                    page_text.push_str(&format!("\n[插图 #{image_id}]\n"));
                }
                pixeval_novel::NovelNode::PixivImage { illust_id, page } => {
                    page_text.push_str(&format!("\n[插画 #{illust_id} P{page}]\n"));
                }
            }
        }
        let clean = page_text.trim().to_string();
        pages.push(clean);
    }

    if pages.is_empty() {
        pages.push(String::new());
    }

    let total = pages.len() as i32;

    Ok(NovelContentDto {
        id: novel_id,
        pages,
        total_pages: total,
        chapters,
    })
}
