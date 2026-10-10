// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use pixeval_mako::{Illustration, Novel, NovelContent, UgoiraMetadata};
use pixeval_media::{ImageCodecFormat, MangaArchiveFormat, UgoiraFormat};

use crate::metapath::{MacroContext, reduce};

use super::tokens::{
    change_extension_token, join_base, normalize_final_path, remove_extension_tokens, resolve_file_tokens,
    url_extension,
};

pub const IMAGE_NOT_AVAILABLE: &str = "avares://Pixeval/Assets/image-not-available.png";
pub const ILLUSTRATION_SERIALIZE_KEY: &str = "Mako.Model.Illustration";
pub const NOVEL_SERIALIZE_KEY: &str = "Mako.Model.Novel";

#[derive(Clone, Debug)]
pub struct PlannedFile {
    pub url: String,
    pub destination: String,
}

#[derive(Clone, Debug)]
pub enum AfterDownload {
    None,
    EncodeEach { extension: String },
    PackManga { format: MangaArchiveFormat, archive_path: String },
    SynthesizeUgoira { format: UgoiraFormat, destination: String, folder: String },
    WriteDelayCsv { path: String },
    EncodeUgoira { extension: String, destination: String, folder: String },
    WriteNovel(NovelOutput),
}

#[derive(Clone, Debug)]
pub struct NovelOutput {
    pub novel_file: String,
    pub image_folder: String,
    pub built_in: Option<NovelBuiltIn>,
    pub extension: Option<String>,
    pub delete_images_after: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum NovelBuiltIn {
    Txt,
    Html,
    Markdown,
    Epub,
}

#[derive(Clone, Debug)]
pub struct WorkBlueprint {
    pub artwork_id: String,
    pub title: String,
    pub author: String,
    pub thumbnail_url: String,
    pub website_uri: String,
    pub app_uri: String,
    pub payload_json: String,
    pub serialize_key: String,
    pub destination: String,
    pub open_destination: String,
    pub format_token: String,
    pub files: Vec<PlannedFile>,
    pub after: AfterDownload,
    pub delays_ms: Vec<u32>,
    pub defer_ugoira: bool,
    pub defer_novel: bool,
    pub novel_id: i64,
    pub skip_when_exists: bool,
}

pub fn extension_token(token: &str) -> Option<String> {
    let rest = token.strip_prefix("extension:")?;
    if rest.is_empty() { None } else { Some(rest.to_string()) }
}

pub fn illustration_extension(token: &str) -> Option<String> {
    extension_token(token)
}

pub fn ugoira_extension(token: &str) -> Option<String> {
    if let Some(ext) = extension_token(token) {
        return Some(ext);
    }
    match token {
        "Gif" => Some("gif".to_string()),
        "Apng" => Some("png".to_string()),
        "Webp" => Some("webp".to_string()),
        "Mp4" => Some("mp4".to_string()),
        _ => None,
    }
}

pub fn novel_extension(token: &str) -> String {
    if let Some(ext) = extension_token(token) {
        return ext;
    }
    match token {
        "Html" => "html".to_string(),
        "Md" => "md".to_string(),
        "Epub" => "epub".to_string(),
        _ => "txt".to_string(),
    }
}

pub fn novel_built_in(token: &str) -> Option<NovelBuiltIn> {
    if extension_token(token).is_some() {
        return None;
    }
    Some(match token {
        "Html" => NovelBuiltIn::Html,
        "Md" => NovelBuiltIn::Markdown,
        "Epub" => NovelBuiltIn::Epub,
        _ => NovelBuiltIn::Txt,
    })
}

fn codec_for_extension(extension: &str) -> Option<ImageCodecFormat> {
    match extension.to_ascii_lowercase().as_str() {
        "jpg" | "jpeg" => Some(ImageCodecFormat::Jpeg),
        "png" => Some(ImageCodecFormat::Png),
        "webp" => Some(ImageCodecFormat::Webp),
        "avif" => Some(ImageCodecFormat::Avif),
        _ => None,
    }
}

pub fn ugoira_native_format(extension: &str) -> Option<UgoiraFormat> {
    match extension.to_ascii_lowercase().as_str() {
        "gif" => Some(UgoiraFormat::Gif),
        "png" | "apng" => Some(UgoiraFormat::Apng),
        "webp" => Some(UgoiraFormat::Webp),
        "mp4" => Some(UgoiraFormat::Mp4),
        _ => None,
    }
}

fn archive_format(extension: &str) -> Option<MangaArchiveFormat> {
    match extension.to_ascii_lowercase().as_str() {
        "cbz" => Some(MangaArchiveFormat::Cbz),
        "zip" => Some(MangaArchiveFormat::Zip),
        _ => None,
    }
}

pub fn reduce_destination(
    path_macro: &str,
    base_dir: &str,
    context: &MacroContext,
) -> Result<String, String> {
    // Keep `<ext>` and `<pic_set_index>` until each file path is finalized.
    // Stripping them here would make every manga page share one path.
    let reduced = reduce(path_macro, context).map_err(|err| err.to_string())?;
    Ok(join_base(base_dir, &reduced))
}

pub fn macro_context_for_illustration(
    illust: &Illustration,
    page_index: i32,
    subscription_id: Option<i64>,
    subscription_type: Option<String>,
) -> MacroContext {
    let is_gif = illust.illust_type.eq_ignore_ascii_case("ugoira");
    let is_set = illust.page_count > 1 || page_index > -1;
    let image_type = if is_set {
        "ImageSet"
    } else if is_gif {
        "SingleAnimatedImage"
    } else {
        "SingleImage"
    };
    let (is_r18g, is_r18) = rating_flags(illust.x_restrict, illust.restriction_attributes.as_deref());
    MacroContext {
        artwork_id: illust.id.to_string(),
        title: illust.title.clone(),
        author_ids: vec![illust.user.id.to_string()],
        author_names: vec![illust.user.name.clone()],
        create_date: illust.create_date.clone(),
        image_type: image_type.to_string(),
        set_index: page_index,
        is_ai: illust.illust_ai_type == 2,
        is_r18,
        is_r18g,
        is_novel: false,
        has_series: illust.series.is_some(),
        series_id: illust.series.as_ref().map(|s| s.id.to_string()),
        series_title: illust.series.as_ref().map(|s| s.title.clone()),
        work_subscription_id: subscription_id,
        work_subscription_type: subscription_type,
    }
}

pub fn macro_context_for_novel(
    novel: &Novel,
    subscription_id: Option<i64>,
    subscription_type: Option<String>,
) -> MacroContext {
    let is_r18g = novel.x_restrict == 2;
    let is_r18 = novel.x_restrict == 1 || is_r18g;
    MacroContext {
        artwork_id: novel.id.to_string(),
        title: novel.title.clone(),
        author_ids: vec![novel.user.id.to_string()],
        author_names: vec![novel.user.name.clone()],
        create_date: novel.create_date.clone(),
        image_type: "Other".to_string(),
        set_index: -1,
        is_ai: novel.novel_ai_type == 2,
        is_r18,
        is_r18g,
        is_novel: true,
        has_series: novel.series.is_some(),
        series_id: novel.series.as_ref().map(|s| s.id.to_string()),
        series_title: novel.series.as_ref().map(|s| s.title.clone()),
        work_subscription_id: subscription_id,
        work_subscription_type: subscription_type,
    }
}

fn rating_flags(x_restrict: i32, attributes: Option<&[String]>) -> (bool, bool) {
    let mut guro = x_restrict == 2;
    let mut explicit = x_restrict == 1 || guro;
    if let Some(attributes) = attributes {
        if attributes.iter().any(|a| a.eq_ignore_ascii_case("r18g")) {
            guro = true;
            explicit = true;
        } else if attributes.iter().any(|a| a.eq_ignore_ascii_case("r18")) {
            explicit = true;
        }
    }
    (guro, explicit)
}

pub fn build_illustration(
    illust: &Illustration,
    payload_json: String,
    page_index: i32,
    destination: String,
    format_token: String,
) -> WorkBlueprint {
    let is_gif = illust.illust_type.eq_ignore_ascii_case("ugoira") && page_index < 0;
    let is_manga = (illust.page_count > 1 || illust.meta_pages.len() > 1) && page_index < 0 && !is_gif;
    let thumb = illust
        .image_urls
        .medium
        .clone()
        .or(illust.image_urls.square_medium.clone())
        .or(illust.image_urls.large.clone())
        .unwrap_or_default();

    if is_gif {
        let ext = ugoira_extension(&format_token);
        let (open_destination, _folder, after_kind) = ugoira_locations(&destination, ext.as_deref());
        return WorkBlueprint {
            artwork_id: illust.id.to_string(),
            title: illust.title.clone(),
            author: illust.user.name.clone(),
            thumbnail_url: thumb,
            website_uri: format!("https://www.pixiv.net/artworks/{}", illust.id),
            app_uri: format!("pixeval://illust/{}", illust.id),
            payload_json,
            serialize_key: ILLUSTRATION_SERIALIZE_KEY.to_string(),
            destination,
            open_destination,
            format_token,
            files: Vec::new(),
            after: after_kind,
            delays_ms: Vec::new(),
            defer_ugoira: true,
            defer_novel: false,
            novel_id: 0,
            skip_when_exists: ext.is_some(),
        };
    }

    let pages = illustration_pages(illust, page_index);
    let ext = illustration_extension(&format_token);
    let files = pages
        .iter()
        .map(|(index, url)| PlannedFile {
            destination: finalize_page_path(&destination, url, *index),
            url: url.clone(),
        })
        .collect::<Vec<_>>();
    let open_destination = files
        .first()
        .map(|f| parent_dir(&f.destination))
        .filter(|_| is_manga)
        .unwrap_or_else(|| files.first().map(|f| f.destination.clone()).unwrap_or_else(|| destination.clone()));
    let after = if is_manga {
        match ext.as_deref() {
            Some(extension) if archive_format(extension).is_some() => {
                let format = archive_format(extension).unwrap();
                let archive_path = files
                    .first()
                    .map(|f| {
                        let dir = parent_dir(&f.destination);
                        format!("{dir}/manga.{}", extension.to_ascii_lowercase())
                    })
                    .unwrap_or_default();
                AfterDownload::PackManga { format, archive_path }
            }
            Some(extension) if codec_for_extension(extension).is_some() => {
                AfterDownload::EncodeEach { extension: extension.to_string() }
            }
            Some(extension) => AfterDownload::EncodeEach { extension: extension.to_string() },
            None => AfterDownload::None,
        }
    } else if let Some(extension) = ext {
        AfterDownload::EncodeEach { extension }
    } else {
        AfterDownload::None
    };

    WorkBlueprint {
        artwork_id: illust.id.to_string(),
        title: illust.title.clone(),
        author: illust.user.name.clone(),
        thumbnail_url: thumb,
        website_uri: format!("https://www.pixiv.net/artworks/{}", illust.id),
        app_uri: format!("pixeval://illust/{}", illust.id),
        payload_json,
        serialize_key: ILLUSTRATION_SERIALIZE_KEY.to_string(),
        open_destination: if is_manga { open_destination } else { files.first().map(|f| f.destination.clone()).unwrap_or(destination.clone()) },
        destination,
        format_token,
        files,
        after,
        delays_ms: Vec::new(),
        defer_ugoira: false,
        defer_novel: false,
        novel_id: 0,
        skip_when_exists: false,
    }
}

pub fn fill_ugoira_frames(blueprint: &mut WorkBlueprint, original_url: &str, metadata: &UgoiraMetadata) {
    let folder = match &blueprint.after {
        AfterDownload::SynthesizeUgoira { folder, .. }
        | AfterDownload::EncodeUgoira { folder, .. } => folder.clone(),
        AfterDownload::WriteDelayCsv { path } => parent_dir(path),
        _ => remove_extension_tokens(&blueprint.destination),
    };
    let mut files = Vec::new();
    let mut delays = Vec::new();
    for (index, frame) in metadata.frames.iter().enumerate() {
        let url = original_url.replace("ugoira0", &format!("ugoira{index}"));
        let ext = url_extension(&url);
        let name = if ext.is_empty() {
            index.to_string()
        } else {
            format!("{index}.{ext}")
        };
        files.push(PlannedFile {
            url,
            destination: join_path(&folder, &name),
        });
        delays.push(frame.delay.max(0) as u32);
    }
    blueprint.files = files;
    blueprint.delays_ms = delays;
    blueprint.defer_ugoira = false;
}

fn ugoira_locations(destination: &str, extension: Option<&str>) -> (String, String, AfterDownload) {
    match extension {
        None => {
            let folder = normalize_final_path(&remove_extension_tokens(destination));
            let csv = join_path(&folder, "intervals in milliseconds.csv");
            (folder.clone(), folder, AfterDownload::WriteDelayCsv { path: csv })
        }
        Some(ext) => {
            let dest = normalize_final_path(&change_extension_token(destination, ext));
            let folder = format!("{dest}.pixevaldownloading");
            let after = if let Some(format) = ugoira_native_format(ext) {
                AfterDownload::SynthesizeUgoira {
                    format,
                    destination: dest.clone(),
                    folder: folder.clone(),
                }
            } else {
                AfterDownload::EncodeUgoira {
                    extension: ext.to_string(),
                    destination: dest.clone(),
                    folder: folder.clone(),
                }
            };
            (dest, folder, after)
        }
    }
}

pub fn build_novel(novel: &Novel, payload_json: String, destination: String, format_token: String) -> WorkBlueprint {
    let output = novel_output_paths(&destination, &format_token);
    let thumb = novel
        .image_urls
        .square_medium
        .clone()
        .or(novel.image_urls.medium.clone())
        .or(novel.image_urls.large.clone())
        .unwrap_or_default();
    let skip = output.extension.is_some() || matches!(output.built_in, Some(NovelBuiltIn::Epub));
    WorkBlueprint {
        artwork_id: novel.id.to_string(),
        title: novel.title.clone(),
        author: novel.user.name.clone(),
        thumbnail_url: thumb,
        website_uri: format!("https://www.pixiv.net/novel/show.php?id={}", novel.id),
        app_uri: format!("pixeval://novel/{}", novel.id),
        payload_json,
        serialize_key: NOVEL_SERIALIZE_KEY.to_string(),
        open_destination: output.novel_file.clone(),
        destination,
        format_token,
        files: Vec::new(),
        after: AfterDownload::WriteNovel(output),
        delays_ms: Vec::new(),
        defer_ugoira: false,
        defer_novel: true,
        novel_id: novel.id,
        skip_when_exists: skip,
    }
}

pub fn novel_output_paths(destination: &str, format_token: &str) -> NovelOutput {
    let extension = novel_extension(format_token);
    let built_in = novel_built_in(format_token);
    if built_in.is_some() && built_in != Some(NovelBuiltIn::Epub) {
        let folder = normalize_final_path(&remove_extension_tokens(destination));
        NovelOutput {
            novel_file: join_path(&folder, &format!("novel.{extension}")),
            image_folder: folder,
            built_in,
            extension: None,
            delete_images_after: false,
        }
    } else {
        let novel_file = normalize_final_path(&change_extension_token(destination, &extension));
        NovelOutput {
            image_folder: format!("{novel_file}.pixevaldownloading"),
            novel_file,
            built_in,
            extension: extension_token(format_token),
            delete_images_after: true,
        }
    }
}

pub fn fill_novel_images(blueprint: &mut WorkBlueprint, content: &NovelContent) {
    let AfterDownload::WriteNovel(output) = blueprint.after.clone() else {
        return;
    };
    let mut files = Vec::new();
    let (cover_url, cover_name) = cover_file(&content.cover_url);
    files.push(PlannedFile {
        url: cover_url,
        destination: join_path(&output.image_folder, &cover_name),
    });
    for image in &content.images {
        let ext = url_extension(&image.urls.original);
        let name = if ext.is_empty() {
            image.novel_image_id.to_string()
        } else {
            format!("{}.{}", image.novel_image_id, ext)
        };
        files.push(PlannedFile {
            url: image.urls.original.clone(),
            destination: join_path(&output.image_folder, &name),
        });
    }
    for illust in &content.illusts {
        let url = illust.illust.images.medium.clone();
        let ext = url_extension(&url);
        let stem = format!("{}-{}", illust.id, illust.page);
        let name = if ext.is_empty() { stem } else { format!("{stem}.{ext}") };
        files.push(PlannedFile {
            url,
            destination: join_path(&output.image_folder, &name),
        });
    }
    blueprint.files = files;
    blueprint.defer_novel = false;
    if content.title.is_empty() {
        // keep the novel record title
    } else if blueprint.title.is_empty() {
        blueprint.title = content.title.clone();
    }
}

fn cover_file(url: &str) -> (String, String) {
    let valid = url.starts_with("http://") || url.starts_with("https://");
    let ext = url_extension(url);
    if valid && !ext.is_empty() && url.split(['?', '#']).next().unwrap_or(url).contains('.') {
        (url.to_string(), format!("cover.{ext}"))
    } else if valid && ext.is_empty() {
        (IMAGE_NOT_AVAILABLE.to_string(), "cover.png".to_string())
    } else {
        (IMAGE_NOT_AVAILABLE.to_string(), "cover.png".to_string())
    }
}

pub fn build_external(
    artwork_id: String,
    title: String,
    author: String,
    thumbnail_url: String,
    website_uri: String,
    app_uri: String,
    original_url: String,
    payload_json: String,
    serialize_key: String,
    destination: String,
    format_token: String,
) -> WorkBlueprint {
    let file = finalize_page_path(&destination, &original_url, -1);
    let after = match illustration_extension(&format_token) {
        Some(extension) => AfterDownload::EncodeEach { extension },
        None => AfterDownload::None,
    };
    WorkBlueprint {
        artwork_id,
        title,
        author,
        thumbnail_url,
        website_uri,
        app_uri,
        payload_json,
        serialize_key,
        open_destination: file.clone(),
        destination,
        format_token,
        files: vec![PlannedFile { url: original_url, destination: file }],
        after,
        delays_ms: Vec::new(),
        defer_ugoira: false,
        defer_novel: false,
        novel_id: 0,
        skip_when_exists: false,
    }
}

fn illustration_pages(illust: &Illustration, page_index: i32) -> Vec<(i32, String)> {
    if page_index >= 0 {
        if let Some(page) = illust.meta_pages.get(page_index as usize) {
            return vec![(page_index, page_url(&page.image_urls, &illust.image_urls))];
        }
    }
    if illust.meta_pages.len() > 1 && page_index < 0 {
        return illust
            .meta_pages
            .iter()
            .enumerate()
            .map(|(index, page)| (index as i32, page_url(&page.image_urls, &illust.image_urls)))
            .collect();
    }
    let url = illust
        .meta_single_page
        .original_image_url
        .clone()
        .or(illust.image_urls.original.clone())
        .or(illust.image_urls.large.clone())
        .or(illust.image_urls.medium.clone())
        .unwrap_or_else(|| "about:blank".to_string());
    vec![(page_index, url)]
}

fn page_url(page: &pixeval_mako::ImageUrls, fallback: &pixeval_mako::ImageUrls) -> String {
    page.original
        .clone()
        .or(page.large.clone())
        .or(page.medium.clone())
        .or(fallback.original.clone())
        .or(fallback.large.clone())
        .unwrap_or_else(|| "about:blank".to_string())
}

pub fn finalize_page_path(tokenized: &str, url: &str, set_index: i32) -> String {
    let ext = url_extension(url);
    normalize_final_path(&resolve_file_tokens(tokenized, &ext, set_index))
}

pub fn original_ugoira_url(illust: &Illustration) -> String {
    illust
        .meta_single_page
        .original_image_url
        .clone()
        .or(illust.image_urls.original.clone())
        .unwrap_or_default()
}

pub fn probe_paths(blueprint: &WorkBlueprint) -> Vec<String> {
    let mut paths = Vec::new();
    for file in &blueprint.files {
        paths.push(file.destination.clone());
    }
    match &blueprint.after {
        AfterDownload::WriteNovel(output) => {
            paths.push(output.novel_file.clone());
            for name in ["novel.txt", "novel.html", "novel.md"] {
                paths.push(join_path(&output.image_folder, name));
            }
        }
        AfterDownload::PackManga { archive_path, .. } => paths.push(archive_path.clone()),
        AfterDownload::SynthesizeUgoira { destination, .. }
        | AfterDownload::EncodeUgoira { destination, .. } => paths.push(destination.clone()),
        AfterDownload::WriteDelayCsv { path } => paths.push(path.clone()),
        AfterDownload::None | AfterDownload::EncodeEach { .. } => {
            if blueprint.files.is_empty() && !blueprint.open_destination.is_empty() {
                paths.push(blueprint.open_destination.clone());
            }
        }
    }
    paths.retain(|path| !path.is_empty() && !path.contains('<'));
    paths.sort();
    paths.dedup();
    paths
}

pub fn read_page_index(json: &str) -> i32 {
    let Ok(value) = serde_json::from_str::<serde_json::Value>(json) else {
        return -1;
    };
    value
        .get("set_index")
        .or_else(|| value.get("SetIndex"))
        .and_then(|v| v.as_i64())
        .map(|v| v as i32)
        .unwrap_or(-1)
}

pub fn stamp_page_index(json: &str, page_index: i32) -> String {
    if page_index < 0 {
        return json.to_string();
    }
    let Ok(mut value) = serde_json::from_str::<serde_json::Value>(json) else {
        return json.to_string();
    };
    if let serde_json::Value::Object(map) = &mut value {
        map.insert("set_index".to_string(), serde_json::Value::from(page_index));
    }
    value.to_string()
}

pub fn plan_pixiv(
    payload_json: &str,
    is_novel: bool,
    page_index: i32,
    path_macro: &str,
    base_dir: &str,
    subscription_id: i64,
    subscription_type: &str,
    policy: &super::snapshot::DownloadPolicy,
) -> Result<WorkBlueprint, String> {
    let page_index = if page_index >= 0 {
        page_index
    } else {
        read_page_index(payload_json)
    };
    let stored = stamp_page_index(payload_json, page_index);
    let sub_id = (subscription_id != 0).then_some(subscription_id);
    let sub_type = (!subscription_type.is_empty()).then(|| subscription_type.to_string());
    if is_novel {
        let novel: Novel = flex_deserialize(&stored)?;
        let context = macro_context_for_novel(&novel, sub_id, sub_type);
        let destination = reduce_destination(path_macro, base_dir, &context)?;
        return Ok(build_novel(&novel, stored, destination, policy.novel_format.clone()));
    }

    let illust: Illustration = flex_deserialize(&stored)?;
    let context = macro_context_for_illustration(&illust, page_index, sub_id, sub_type);
    let destination = reduce_destination(path_macro, base_dir, &context)?;
    let is_gif = illust.illust_type.eq_ignore_ascii_case("ugoira") && page_index < 0;
    let mut token = if is_gif {
        policy.ugoira_format.clone()
    } else {
        policy.illustration_format.clone()
    };
    if is_gif && token == "Mp4" && !pixeval_media::is_mp4_supported() {
        token = "Original".to_string();
    }
    Ok(build_illustration(&illust, stored, page_index, destination, token))
}

pub fn replan_stored(
    payload_json: &str,
    destination: &str,
    format_token: &str,
    serialize_key: Option<&str>,
) -> Result<WorkBlueprint, String> {
    let key = serialize_key.unwrap_or("");
    if key.contains("Novel") {
        let novel: Novel = flex_deserialize(payload_json)?;
        return Ok(build_novel(
            &novel,
            payload_json.to_string(),
            destination.to_string(),
            format_token.to_string(),
        ));
    }
    if key.contains("Illustration") || payload_has_illust_type(payload_json) {
        let illust: Illustration = flex_deserialize(payload_json)?;
        let page_index = read_page_index(payload_json);
        let mut token = format_token.to_string();
        if illust.illust_type.eq_ignore_ascii_case("ugoira")
            && page_index < 0
            && token == "Mp4"
            && !pixeval_media::is_mp4_supported()
        {
            token = "Original".to_string();
        }
        return Ok(build_illustration(
            &illust,
            payload_json.to_string(),
            page_index,
            destination.to_string(),
            token,
        ));
    }
    Ok(build_external_from_json(payload_json, destination, format_token, key))
}

fn payload_has_illust_type(json: &str) -> bool {
    let Ok(value) = serde_json::from_str::<serde_json::Value>(json) else {
        return false;
    };
    value.get("type").is_some()
        || value.get("Type").is_some()
        || value.get("illust_type").is_some()
        || value.get("IllustType").is_some()
}

pub fn build_external_from_json(
    payload_json: &str,
    destination: &str,
    format_token: &str,
    serialize_key: &str,
) -> WorkBlueprint {
    let value: serde_json::Value = serde_json::from_str(payload_json).unwrap_or(serde_json::Value::Null);
    let artwork_id = json_string(
        &value,
        &["id", "Id", "artwork_id", "ArtworkId", "raw_id", "RawId", "index_id", "IndexId"],
    );
    let title = json_string(&value, &["title", "Title", "source", "Source", "index_name", "IndexName"]);
    let author = json_string(&value, &["author", "Author", "author_name", "AuthorName", "uploader_name", "UploaderName"]);
    let thumbnail = json_string(
        &value,
        &["thumbnail_url", "ThumbnailUrl", "preview_url", "PreviewUrl", "sample_url", "SampleUrl"],
    );
    let original = json_string(
        &value,
        &[
            "original_url",
            "OriginalUrl",
            "sample_url",
            "SampleUrl",
            "preview_url",
            "PreviewUrl",
            "thumbnail_url",
            "ThumbnailUrl",
        ],
    );
    let website = json_string(&value, &["website_uri", "WebsiteUri", "source_url", "SourceUrl"]);
    let app = json_string(&value, &["app_uri", "AppUri"]);
    let website = if website.is_empty() {
        booru_website(&json_string(&value, &["platform", "Platform"]), &artwork_id)
    } else {
        website
    };
    build_external(
        artwork_id,
        title,
        author,
        thumbnail,
        website,
        app,
        original,
        payload_json.to_string(),
        serialize_key.to_string(),
        destination.to_string(),
        format_token.to_string(),
    )
}

fn json_string(value: &serde_json::Value, names: &[&str]) -> String {
    for name in names {
        if let Some(found) = value.get(*name) {
            if let Some(text) = found.as_str() {
                if !text.is_empty() {
                    return text.to_string();
                }
            } else if let Some(number) = found.as_i64() {
                return number.to_string();
            } else if let Some(number) = found.as_u64() {
                return number.to_string();
            }
        }
    }
    String::new()
}

fn booru_website(platform: &str, id: &str) -> String {
    if id.is_empty() {
        return String::new();
    }
    match platform.to_ascii_lowercase().as_str() {
        "danbooru" | "0" => format!("https://danbooru.donmai.us/posts/{id}"),
        "gelbooru" | "1" => format!("https://gelbooru.com/index.php?page=post&s=view&id={id}"),
        "yandere" | "2" => format!("https://yande.re/post/show/{id}"),
        "sankaku" | "3" => format!("https://chan.sankakucomplex.com/posts/show/{id}"),
        "rule34" | "4" => format!("https://rule34.xxx/index.php?page=post&s=view&id={id}"),
        _ => String::new(),
    }
}

pub fn codec_of(extension: &str) -> Option<ImageCodecFormat> {
    codec_for_extension(extension)
}

fn parent_dir(path: &str) -> String {
    std::path::Path::new(path)
        .parent()
        .map(|p| p.to_string_lossy().to_string())
        .filter(|p| !p.is_empty())
        .unwrap_or_else(|| path.to_string())
}

fn join_path(folder: &str, name: &str) -> String {
    std::path::Path::new(folder).join(name).to_string_lossy().to_string()
}

pub fn flex_deserialize<T: serde::de::DeserializeOwned>(json: &str) -> Result<T, String> {
    let value: serde_json::Value = serde_json::from_str(json).map_err(|err| err.to_string())?;
    let normalized = normalize_json(value);
    serde_json::from_value(normalized).map_err(|err| err.to_string())
}

fn normalize_json(value: serde_json::Value) -> serde_json::Value {
    match value {
        serde_json::Value::Object(map) => {
            let mut next = serde_json::Map::new();
            for (key, child) in map {
                let mut snake = pascal_to_snake(&key);
                if snake == "illust_type" {
                    snake = "type".to_string();
                }
                next.insert(snake, normalize_json(child));
            }
            serde_json::Value::Object(next)
        }
        serde_json::Value::Array(items) => {
            serde_json::Value::Array(items.into_iter().map(normalize_json).collect())
        }
        other => other,
    }
}

fn pascal_to_snake(key: &str) -> String {
    if key.chars().any(|c| c == '_') || key.chars().all(|c| !c.is_ascii_uppercase()) {
        return key.to_string();
    }
    let mut out = String::new();
    for (index, ch) in key.chars().enumerate() {
        if ch.is_ascii_uppercase() {
            if index > 0 {
                out.push('_');
            }
            out.push(ch.to_ascii_lowercase());
        } else {
            out.push(ch);
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;
    use pixeval_mako::{ImageUrls, MetaPage, MetaSinglePage, User};

    fn user() -> User {
        User { id: 9, name: "Author".to_string(), account: "author".to_string(), ..User::default() }
    }

    fn sample_illust() -> Illustration {
        Illustration {
            id: 42,
            title: "Title".to_string(),
            illust_type: "illust".to_string(),
            image_urls: ImageUrls { large: Some("https://i.pximg.net/img-original/img/a.jpg".into()), ..ImageUrls::default() },
            user: user(),
            create_date: "2020-01-02T03:04:05+00:00".into(),
            page_count: 2,
            meta_single_page: MetaSinglePage::default(),
            meta_pages: vec![
                MetaPage { image_urls: ImageUrls { original: Some("https://i.pximg.net/img-original/img/a_p0.jpg".into()), ..ImageUrls::default() } },
                MetaPage { image_urls: ImageUrls { original: Some("https://i.pximg.net/img-original/img/a_p1.png".into()), ..ImageUrls::default() } },
            ],
            ..Illustration::default()
        }
    }

    #[test]
    fn manga_pages_keep_index_and_extension() {
        let illust = sample_illust();
        let plan = build_illustration(&illust, "{}".into(), -1, "D:/art/work.<ext>_p<pic_set_index>".into(), "Original".into());
        assert_eq!(plan.files.len(), 2);
        assert!(plan.files[0].destination.replace('\\', "/").ends_with("work.jpg_p0"));
        assert!(plan.files[1].destination.replace('\\', "/").ends_with("work.png_p1"));
    }

    #[test]
    fn manga_archive_plan() {
        let illust = sample_illust();
        let plan = build_illustration(&illust, "{}".into(), -1, "D:/art/work.<ext>".into(), "extension:cbz".into());
        match plan.after {
            AfterDownload::PackManga { archive_path, .. } => {
                assert!(archive_path.replace('\\', "/").ends_with("manga.cbz"));
            }
            other => panic!("unexpected {other:?}"),
        }
    }

    #[test]
    fn selected_page_is_a_single_file() {
        let illust = sample_illust();
        let plan = build_illustration(&illust, "{}".into(), 1, "D:/art/one.<ext>".into(), "Original".into());
        assert_eq!(plan.files.len(), 1);
        assert!(plan.files[0].url.ends_with("a_p1.png"));
    }

    #[test]
    fn ugoira_frame_urls_follow_ugoira0_replacement() {
        let mut illust = sample_illust();
        illust.illust_type = "ugoira".into();
        illust.page_count = 1;
        illust.meta_pages.clear();
        illust.meta_single_page.original_image_url = Some("https://i.pximg.net/img-original/img/1_ugoira0.jpg".into());
        let mut plan = build_illustration(&illust, "{}".into(), -1, "D:/art/move.<ext>".into(), "Gif".into());
        let metadata = UgoiraMetadata {
            zip_urls: pixeval_mako::UgoiraZipUrls { medium: String::new() },
            frames: vec![
                pixeval_mako::UgoiraFrame { file: "0.jpg".into(), delay: 100 },
                pixeval_mako::UgoiraFrame { file: "1.jpg".into(), delay: 80 },
            ],
        };
        fill_ugoira_frames(&mut plan, illust.meta_single_page.original_image_url.as_deref().unwrap(), &metadata);
        assert_eq!(plan.files.len(), 2);
        assert!(plan.files[1].url.contains("ugoira1"));
        assert_eq!(plan.delays_ms, vec![100, 80]);
        assert!(matches!(plan.after, AfterDownload::SynthesizeUgoira { format: UgoiraFormat::Gif, .. }));
    }

    #[test]
    fn novel_text_and_epub_paths_differ() {
        let text = novel_output_paths("D:/novels/story.<ext>", "OriginalTxt");
        assert!(text.novel_file.replace('\\', "/").ends_with("story/novel.txt") || text.novel_file.replace('\\', "/").contains("/novel.txt"));
        let epub = novel_output_paths("D:/novels/story.<ext>", "Epub");
        assert!(epub.novel_file.replace('\\', "/").ends_with(".epub"));
        assert!(epub.image_folder.ends_with(".pixevaldownloading"));
    }

    #[test]
    fn novel_images_use_context_file_names() {
        let novel = Novel { id: 7, title: "N".into(), user: user(), create_date: "2020-01-01T00:00:00+00:00".into(), ..Novel::default() };
        let mut plan = build_novel(&novel, "{}".into(), "D:/novels/story.<ext>".into(), "Html".into());
        let content = NovelContent {
            cover_url: "https://i.pximg.net/c/cover.jpg".into(),
            images: vec![pixeval_mako::NovelImage {
                novel_image_id: 101,
                urls: pixeval_mako::NovelImageUrls { original: "https://i.pximg.net/img/101.png".into(), ..Default::default() },
                ..Default::default()
            }],
            illusts: vec![pixeval_mako::NovelIllustration {
                id: 202,
                page: 2,
                illust: pixeval_mako::NovelIllustrationInfo {
                    images: pixeval_mako::NovelIllustrationUrls { medium: "https://i.pximg.net/img/202-2.webp".into(), ..Default::default() },
                    ..Default::default()
                },
                ..Default::default()
            }],
            ..NovelContent::default()
        };
        fill_novel_images(&mut plan, &content);
        let names: Vec<_> = plan.files.iter().map(|f| std::path::Path::new(&f.destination).file_name().unwrap().to_string_lossy().to_string()).collect();
        assert_eq!(names, vec!["cover.jpg", "101.png", "202-2.webp"]);
    }

    #[test]
    fn pascal_case_illustration_payload_deserializes() {
        let json = r#"{"Id":5,"Title":"T","IllustType":"illust","ImageUrls":{"Large":"https://example.com/a.jpg"},"User":{"Id":1,"Name":"A","Account":"a"},"CreateDate":"2020-01-01T00:00:00+00:00"}"#;
        let illust: Illustration = flex_deserialize(json).unwrap();
        assert_eq!(illust.id, 5);
        assert_eq!(illust.illust_type, "illust");
        assert_eq!(illust.user.name, "A");
    }
}
