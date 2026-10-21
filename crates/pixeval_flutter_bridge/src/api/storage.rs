use crate::state::APP_STATE;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SearchHistoryItemDto {
    pub value: String,
    pub translated_name: Option<String>,
    pub time: String,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BrowseHistoryItemDto {
    pub id: String,
    pub title: String,
    pub preview_url: String,
    pub author_name: String,
    pub work_type: String,
    pub time: String,
}

pub fn init_storage(db_path: String) -> Result<(), String> {
    APP_STATE.init_storage_with_path(db_path)
}

pub fn add_search_history(keyword: String, translated_name: Option<String>) -> Result<(), String> {
    let storage = APP_STATE.get_or_init_storage()?;
    let time = chrono::Utc::now().to_rfc3339();
    storage
        .history()
        .upsert_search_history(keyword, translated_name, time)
        .map_err(|e| format!("Failed to record search history: {e}"))?;
    Ok(())
}

pub fn get_search_history(limit: u32) -> Result<Vec<SearchHistoryItemDto>, String> {
    let storage = APP_STATE.get_or_init_storage()?;
    let recs = storage
        .history()
        .query_search_history_suggestions("", limit)
        .map_err(|e| format!("Failed to query search history: {e}"))?;

    Ok(recs
        .into_iter()
        .map(|r| SearchHistoryItemDto {
            value: r.value,
            translated_name: r.translated_name,
            time: r.time,
        })
        .collect())
}

pub fn delete_search_history_item(keyword: String) -> Result<bool, String> {
    let storage = APP_STATE.get_or_init_storage()?;
    storage
        .history()
        .try_delete_search_history_by_value(keyword)
        .map_err(|e| format!("Failed to delete search history item: {e}"))
}

pub fn clear_all_search_history() -> Result<(), String> {
    let storage = APP_STATE.get_or_init_storage()?;
    storage
        .history()
        .clear_search_history()
        .map_err(|e| format!("Failed to clear search history: {e}"))
}

pub fn record_browse_history(
    work_id: String,
    title: String,
    preview_url: String,
    author_name: String,
    work_type: Option<String>,
) -> Result<(), String> {
    let storage = APP_STATE.get_or_init_storage()?;
    let actual_type = work_type.unwrap_or_else(|| "illustration".to_string());
    let payload = serde_json::json!({
        "id": work_id,
        "title": title,
        "preview_url": preview_url,
        "author_name": author_name,
        "work_type": actual_type,
        "time": chrono::Utc::now().to_rfc3339(),
    })
    .to_string();

    let work_key = format!("{actual_type}_{work_id}");
    storage
        .history()
        .add_or_replace_browse_history(work_id, Some(actual_type), work_key, payload)
        .map_err(|e| format!("Failed to record browse history: {e}"))?;
    Ok(())
}

pub fn get_browse_history(skip: u32, take: u32) -> Result<Vec<BrowseHistoryItemDto>, String> {
    let storage = APP_STATE.get_or_init_storage()?;
    let records = storage
        .history()
        .stream_browse_history(skip, take)
        .map_err(|e| format!("Failed to query browse history: {e}"))?;

    let mut result = Vec::new();
    for r in records {
        if let Some(json_str) = r.payload_json {
            if let Ok(val) = serde_json::from_str::<serde_json::Value>(&json_str) {
                let id = val["id"].as_str().unwrap_or(&r.id).to_string();
                let title = val["title"].as_str().unwrap_or("").to_string();
                let preview_url = val["preview_url"].as_str().unwrap_or("").to_string();
                let author_name = val["author_name"].as_str().unwrap_or("").to_string();
                let work_type = val["work_type"]
                    .as_str()
                    .unwrap_or("illustration")
                    .to_string();
                let time = val["time"].as_str().unwrap_or("").to_string();

                result.push(BrowseHistoryItemDto {
                    id,
                    title,
                    preview_url,
                    author_name,
                    work_type,
                    time,
                });
            }
        }
    }
    Ok(result)
}

pub fn clear_all_browse_history() -> Result<(), String> {
    let storage = APP_STATE.get_or_init_storage()?;
    storage
        .history()
        .clear_browse_history()
        .map_err(|e| format!("Failed to clear browse history: {e}"))
}
