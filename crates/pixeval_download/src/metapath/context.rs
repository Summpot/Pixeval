// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub enum MacroImageType {
    SingleImage,
    ImageSet,
    SingleAnimatedImage,
    Other,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct MacroContext {
    pub artwork_id: String,
    pub title: String,
    pub author_ids: Vec<String>,
    pub author_names: Vec<String>,
    pub create_date: String,
    pub image_type: String,
    pub set_index: i32,
    pub is_ai: bool,
    pub is_r18: bool,
    pub is_r18g: bool,
    pub is_novel: bool,
    pub has_series: bool,
    pub series_id: Option<String>,
    pub series_title: Option<String>,
    pub work_subscription_id: Option<i64>,
    pub work_subscription_type: Option<String>,
}

impl MacroContext {
    pub fn image_type(&self) -> MacroImageType {
        match self.image_type.as_str() {
            "ImageSet" => MacroImageType::ImageSet,
            "SingleAnimatedImage" => MacroImageType::SingleAnimatedImage,
            "Other" => MacroImageType::Other,
            _ => MacroImageType::SingleImage,
        }
    }
}

impl Default for MacroContext {
    fn default() -> Self {
        Self {
            artwork_id: String::new(),
            title: String::new(),
            author_ids: Vec::new(),
            author_names: Vec::new(),
            create_date: String::new(),
            image_type: "SingleImage".to_string(),
            set_index: -1,
            is_ai: false,
            is_r18: false,
            is_r18g: false,
            is_novel: false,
            has_series: false,
            series_id: None,
            series_title: None,
            work_subscription_id: None,
            work_subscription_type: None,
        }
    }
}
