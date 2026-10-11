uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod engine;
pub mod error;
pub mod manga;
pub mod transcode;
pub mod ugoira;

pub use engine::MediaEngine;
pub use error::MediaError;
pub use manga::MangaArchiveFormat;
pub use transcode::{ImageCodecFormat, TranscodeOptions};
pub use ugoira::{UgoiraFormat, is_mp4_supported};

#[uniffi::export]
pub fn media_ping() -> String {
    "media_pong".to_string()
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    fn create_test_jpeg(width: u32, height: u32, r: u8, g: u8, b: u8) -> Vec<u8> {
        let img = image::RgbImage::from_pixel(width, height, image::Rgb([r, g, b]));
        let mut buf = Vec::new();
        image::codecs::jpeg::JpegEncoder::new(&mut buf)
            .encode(
                img.as_raw(),
                width,
                height,
                image::ExtendedColorType::Rgb8,
            )
            .unwrap();
        buf
    }

    #[test]
    fn test_transcode_bytes_all_formats() {
        let jpeg = create_test_jpeg(32, 32, 200, 100, 50);

        // To PNG
        let png = transcode::transcode_bytes(jpeg.clone(), ImageCodecFormat::Png, None).unwrap();
        assert_eq!(image::guess_format(&png).unwrap(), image::ImageFormat::Png);

        // To WebP
        let webp = transcode::transcode_bytes(jpeg.clone(), ImageCodecFormat::Webp, None).unwrap();
        assert_eq!(image::guess_format(&webp).unwrap(), image::ImageFormat::WebP);

        // To AVIF
        let avif = transcode::transcode_bytes(jpeg.clone(), ImageCodecFormat::Avif, None).unwrap();
        assert_eq!(image::guess_format(&avif).unwrap(), image::ImageFormat::Avif);

        // To JPEG
        let new_jpeg = transcode::transcode_bytes(
            png,
            ImageCodecFormat::Jpeg,
            Some(TranscodeOptions {
                quality: Some(90),
                lossless: false,
            }),
        )
        .unwrap();
        assert_eq!(
            image::guess_format(&new_jpeg).unwrap(),
            image::ImageFormat::Jpeg
        );
    }

    fn get_test_temp_dir() -> tempfile::TempDir {
        let repo_tmp = std::path::PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../../target/tmp");
        let _ = std::fs::create_dir_all(&repo_tmp);
        if repo_tmp.exists() {
            tempfile::Builder::new().prefix("test_").tempdir_in(repo_tmp).unwrap()
        } else {
            tempfile::Builder::new().prefix("test_").tempdir().unwrap()
        }
    }

    #[test]
    fn test_pack_manga_zip_and_cbz() {
        let dir = get_test_temp_dir();
        let p1 = dir.path().join("0.jpg");
        let p2 = dir.path().join("1.jpg");
        std::fs::write(&p1, create_test_jpeg(16, 16, 255, 0, 0)).unwrap();
        std::fs::write(&p2, create_test_jpeg(16, 16, 0, 255, 0)).unwrap();

        let files = vec![p1.to_str().unwrap().to_string(), p2.to_str().unwrap().to_string()];

        let cbz_path = dir.path().join("manga.cbz");
        manga::pack_manga(files.clone(), cbz_path.to_str().unwrap(), MangaArchiveFormat::Cbz).unwrap();
        assert!(cbz_path.exists());

        // Verify cbz contents
        let file = std::fs::File::open(&cbz_path).unwrap();
        let mut archive = zip::ZipArchive::new(file).unwrap();
        assert_eq!(archive.len(), 2);
        assert_eq!(archive.by_index(0).unwrap().name(), "0000.jpg");
        assert_eq!(archive.by_index(1).unwrap().name(), "0001.jpg");

        let zip_path = dir.path().join("manga.zip");
        manga::pack_manga(files, zip_path.to_str().unwrap(), MangaArchiveFormat::Zip).unwrap();
        assert!(zip_path.exists());
    }

    #[test]
    fn test_ugoira_synthesis_all_formats() {
        let dir = get_test_temp_dir();
        let zip_path = dir.path().join("ugoira.zip");

        // Create sample ugoira zip archive
        {
            let file = std::fs::File::create(&zip_path).unwrap();
            let mut zip = zip::ZipWriter::new(file);
            let options = zip::write::SimpleFileOptions::default();

            zip.start_file("000000.jpg", options).unwrap();
            zip.write_all(&create_test_jpeg(32, 32, 255, 0, 0)).unwrap();

            zip.start_file("000001.jpg", options).unwrap();
            zip.write_all(&create_test_jpeg(32, 32, 0, 255, 0)).unwrap();

            zip.finish().unwrap();
        }

        let engine = MediaEngine::new();
        let delays = vec![100, 120];

        // Synthesize GIF
        let gif_out = dir.path().join("ugoira.gif");
        engine
            .synthesize_ugoira_from_zip(
                zip_path.to_str().unwrap().to_string(),
                gif_out.to_str().unwrap().to_string(),
                UgoiraFormat::Gif,
                delays.clone(),
            )
            .unwrap();
        assert!(gif_out.exists());
        assert_eq!(
            image::guess_format(&std::fs::read(&gif_out).unwrap()).unwrap(),
            image::ImageFormat::Gif
        );

        // Synthesize APNG
        let apng_out = dir.path().join("ugoira.png");
        engine
            .synthesize_ugoira_from_zip(
                zip_path.to_str().unwrap().to_string(),
                apng_out.to_str().unwrap().to_string(),
                UgoiraFormat::Apng,
                delays.clone(),
            )
            .unwrap();
        assert!(apng_out.exists());
        assert_eq!(
            image::guess_format(&std::fs::read(&apng_out).unwrap()).unwrap(),
            image::ImageFormat::Png
        );

        // Synthesize WebP
        let webp_out = dir.path().join("ugoira.webp");
        engine
            .synthesize_ugoira_from_zip(
                zip_path.to_str().unwrap().to_string(),
                webp_out.to_str().unwrap().to_string(),
                UgoiraFormat::Webp,
                delays.clone(),
            )
            .unwrap();
        assert!(webp_out.exists());
        assert_eq!(
            image::guess_format(&std::fs::read(&webp_out).unwrap()).unwrap(),
            image::ImageFormat::WebP
        );

        // Extract Original format
        let orig_out = dir.path().join("ugoira_loose");
        engine
            .synthesize_ugoira_from_zip(
                zip_path.to_str().unwrap().to_string(),
                orig_out.to_str().unwrap().to_string(),
                UgoiraFormat::Original,
                delays.clone(),
            )
            .unwrap();
        assert!(orig_out.exists());
        assert!(orig_out.join("000000.jpg").exists());
        assert!(orig_out.join("000001.jpg").exists());
        assert!(orig_out.join("intervals in milliseconds.csv").exists());
        let csv = std::fs::read_to_string(orig_out.join("intervals in milliseconds.csv")).unwrap();
        assert_eq!(csv, "100,120");

        // MP4 synthesis test
        let mp4_out = dir.path().join("ugoira.mp4");
        let mp4_res = engine.synthesize_ugoira_from_zip(
            zip_path.to_str().unwrap().to_string(),
            mp4_out.to_str().unwrap().to_string(),
            UgoiraFormat::Mp4,
            delays.clone(),
        );
        if !engine.is_mp4_supported() {
            assert!(mp4_res.is_err());
        } else {
            assert!(mp4_res.is_ok(), "MP4 encoding failed: {:?}", mp4_res);
            assert!(mp4_out.exists());
            let len = std::fs::metadata(&mp4_out).unwrap().len();
            assert!(len > 0, "Generated MP4 file is empty");
        }
    }

    #[test]
    fn test_ugoira_mp4_dedicated() {
        let engine = MediaEngine::new();
        if !engine.is_mp4_supported() {
            println!("MP4 not supported on this platform, skipping dedicated test");
            return;
        }

        let dir = get_test_temp_dir();
        let f1 = dir.path().join("f1.jpg");
        let f2 = dir.path().join("f2.jpg");
        let f3 = dir.path().join("f3.jpg");
        std::fs::write(&f1, create_test_jpeg(100, 100, 255, 0, 0)).unwrap();
        std::fs::write(&f2, create_test_jpeg(100, 100, 0, 255, 0)).unwrap();
        std::fs::write(&f3, create_test_jpeg(100, 100, 0, 0, 255)).unwrap();

        let frames = vec![
            f1.to_str().unwrap().to_string(),
            f2.to_str().unwrap().to_string(),
            f3.to_str().unwrap().to_string(),
        ];
        let delays = vec![80, 120, 150];

        let mp4_out = dir.path().join("output.mp4");
        engine
            .synthesize_ugoira_from_frames(
                frames,
                mp4_out.to_str().unwrap().to_string(),
                UgoiraFormat::Mp4,
                delays,
            )
            .expect("Failed to synthesize MP4 from frames");

        assert!(mp4_out.exists());
        let bytes = std::fs::read(&mp4_out).unwrap();
        assert!(bytes.len() > 16, "File too small");
        // Verify MP4 ftyp box signature in the first 16 bytes
        let has_ftyp = bytes.windows(4).take(16).any(|w| w == b"ftyp");
        assert!(has_ftyp, "MP4 output missing 'ftyp' box");
    }
}
