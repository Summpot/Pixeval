// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use serde::{Deserialize, Serialize};

#[derive(uniffi::Enum, Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
pub enum NovelNode {
    Text { content: String },
    Ruby { kanji: String, ruby_text: String },
    JumpUri { title: String, uri: String },
    JumpPage { page: u32 },
    Chapter { title: String },
    UploadImage { image_id: i64 },
    PixivImage { illust_id: i64, page: i32 },
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelPage {
    pub page_index: u32,
    pub nodes: Vec<NovelNode>,
}

#[derive(uniffi::Record, Clone, Debug, Serialize, Deserialize, Default, PartialEq, Eq)]
pub struct NovelDocument {
    pub pages: Vec<NovelPage>,
}
