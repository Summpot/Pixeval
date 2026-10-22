use crate::app::state::APP_STATE;
pub async fn toggle_bookmark(illust_id: i64, is_bookmarked: bool) -> Result<bool, String> {
    let client = &APP_STATE.client;
    if is_bookmarked {
        client
            .remove_bookmark(false, illust_id)
            .await
            .map_err(|e| format!("Remove bookmark failed: {e}"))?;
        Ok(false)
    } else {
        client
            .post_bookmark(false, illust_id, "public".to_string(), None)
            .await
            .map_err(|e| format!("Add bookmark failed: {e}"))?;
        Ok(true)
    }
}

pub async fn get_illust_detail(illust_id: i64) -> Result<crate::Illustration, String> {
    let client = &APP_STATE.client;
    let resp = client
        .get_illustration(illust_id)
        .await
        .map_err(|e| format!("Get illustration detail failed: {e}"))?;
    Ok(resp)
}

pub mod ffi {

    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn toggle_bookmark(illust_id: i64, is_bookmarked: bool) -> Result<bool, String> {
        async_compat::Compat::new(
            async move { super::toggle_bookmark(illust_id, is_bookmarked).await },
        )
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn get_illust_detail(illust_id: i64) -> Result<crate::Illustration, String> {
        async_compat::Compat::new(async move { super::get_illust_detail(illust_id).await }).await
    }
}
