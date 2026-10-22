use crate::app::state::APP_STATE;
use std::io::Read;

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
#[boltffi::data]
pub struct UgoiraFrameDto {
    pub file: String,
    pub delay: u32,
}

#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
#[boltffi::data]
pub struct UgoiraMetadataDto {
    pub zip_url: String,
    pub frames: Vec<UgoiraFrameDto>,
}

#[derive(Clone, Debug, uniffi::Record)]
#[boltffi::data]
pub struct UgoiraFramesDto {
    pub frame_bytes: Vec<Vec<u8>>,
    pub delays: Vec<u32>,
}

pub async fn get_ugoira_metadata(illust_id: i64) -> Result<UgoiraMetadataDto, String> {
    let client = &APP_STATE.client;
    let meta = client
        .get_ugoira_metadata(illust_id)
        .await
        .map_err(|e| format!("Failed to get ugoira metadata: {e}"))?;

    let zip_url = meta.zip_urls.medium;

    let frames = meta
        .frames
        .into_iter()
        .map(|f| UgoiraFrameDto {
            file: f.file,
            delay: f.delay as u32,
        })
        .collect();

    Ok(UgoiraMetadataDto { zip_url, frames })
}

pub fn extract_ugoira_frames_from_zip_bytes(
    zip_bytes: Vec<u8>,
    delays: Vec<u32>,
) -> Result<UgoiraFramesDto, String> {
    let reader = std::io::Cursor::new(zip_bytes);
    let mut archive =
        zip::ZipArchive::new(reader).map_err(|e| format!("Invalid zip archive: {e}"))?;

    let mut names: Vec<String> = archive
        .file_names()
        .filter(|s| !s.ends_with('/') && !s.starts_with("__MACOSX"))
        .map(|s| s.to_string())
        .collect();
    names.sort();

    let mut frame_bytes = Vec::with_capacity(names.len());
    for name in &names {
        let mut file = archive
            .by_name(name)
            .map_err(|e| format!("Failed to read frame {name}: {e}"))?;
        let mut buf = Vec::with_capacity(file.size() as usize);
        file.read_to_end(&mut buf)
            .map_err(|e| format!("Failed to extract frame {name}: {e}"))?;
        frame_bytes.push(buf);
    }

    Ok(UgoiraFramesDto {
        frame_bytes,
        delays,
    })
}

pub fn export_ugoira(
    zip_path: String,
    output_path: String,
    format_name: String,
    frame_delays_ms: Vec<u32>,
) -> Result<(), String> {
    let fmt = match format_name.to_lowercase().as_str() {
        "gif" => pixeval_media::UgoiraFormat::Gif,
        "webp" => pixeval_media::UgoiraFormat::Webp,
        "apng" => pixeval_media::UgoiraFormat::Apng,
        "mp4" => pixeval_media::UgoiraFormat::Mp4,
        _ => pixeval_media::UgoiraFormat::Original,
    };

    pixeval_media::synthesize_ugoira_from_zip(zip_path, output_path, fmt, frame_delays_ms)
        .map_err(|e| format!("Ugoira export failed: {e}"))
}

pub mod ffi {

    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn get_ugoira_metadata(
        illust_id: i64,
    ) -> Result<crate::app::api::ugoira::UgoiraMetadataDto, String> {
        async_compat::Compat::new(async move { super::get_ugoira_metadata(illust_id).await }).await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn extract_ugoira_frames_from_zip_bytes(
        zip_bytes: Vec<u8>,
        delays: Vec<u32>,
    ) -> Result<crate::app::api::ugoira::UgoiraFramesDto, String> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || {
                super::extract_ugoira_frames_from_zip_bytes(zip_bytes, delays)
            })
            .await
            .expect("native application worker panicked")
        })
        .await
    }
    #[uniffi::export(async_runtime = "tokio")]
    #[boltffi::export]
    pub async fn export_ugoira(
        zip_path: String,
        output_path: String,
        format_name: String,
        frame_delays_ms: Vec<u32>,
    ) -> Result<(), String> {
        async_compat::Compat::new(async move {
            tokio::task::spawn_blocking(move || {
                super::export_ugoira(zip_path, output_path, format_name, frame_delays_ms)
            })
            .await
            .expect("native application worker panicked")
        })
        .await
    }
}
