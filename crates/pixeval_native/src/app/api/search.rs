use crate::app::state::APP_STATE;

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
#[boltffi::data]
pub struct AutocompleteTagDto {
    pub name: String,
    pub translated_name: Option<String>,
}

pub async fn get_search_autocomplete(word: String) -> Result<Vec<AutocompleteTagDto>, String> {
    let client = &APP_STATE.client;
    let tags = client
        .search_autocomplete(word)
        .await
        .map_err(|e| format!("Autocomplete failed: {e}"))?;

    Ok(tags
        .into_iter()
        .map(|t| AutocompleteTagDto {
            name: t.name,
            translated_name: t.translated_name,
        })
        .collect())
}

pub mod ffi {

    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn get_search_autocomplete(
        word: String,
    ) -> Result<Vec<crate::app::api::search::AutocompleteTagDto>, String> {
        async_compat::Compat::new(async move { super::get_search_autocomplete(word).await }).await
    }
}
