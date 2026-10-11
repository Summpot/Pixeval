// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use image::AnimationDecoder;
use std::io::Cursor;
use std::sync::Arc;
use zune_core::bytestream::ZCursor;
use zune_core::colorspace::ColorSpace;
use zune_core::options::DecoderOptions;
use zune_jpeg::JpegDecoder;

pub const DEFAULT_PREVIEW_DIMENSION: u32 = 1024;
pub const DEFAULT_MAXIMUM_PIXEL_COUNT: u64 = 4 * 1024 * 1024;
pub const MAXIMUM_IMAGE_DIMENSION_AREA: u64 = 64 * 1024 * 1024;

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct DecodedPreviewFrame {
    pub width: u32,
    pub height: u32,
    pub bgra_data: Vec<u8>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ImageFormatKind {
    Jpeg,
    Png,
    Gif,
    Webp,
    Zip,
    Unknown,
}

pub fn detect_image_format(data: &[u8]) -> ImageFormatKind {
    if data.len() < 2 {
        return ImageFormatKind::Unknown;
    }
    if data[0] == 0xFF && data[1] == 0xD8 {
        return ImageFormatKind::Jpeg;
    }
    if data.starts_with(b"\x89PNG\r\n\x1a\n") {
        return ImageFormatKind::Png;
    }
    if data.starts_with(b"GIF87a") || data.starts_with(b"GIF89a") {
        return ImageFormatKind::Gif;
    }
    if data.len() >= 12 && &data[0..4] == b"RIFF" && &data[8..12] == b"WEBP" {
        return ImageFormatKind::Webp;
    }
    if data.starts_with(b"PK\x03\x04") {
        return ImageFormatKind::Zip;
    }
    ImageFormatKind::Unknown
}

/// Core progressive and incremental image decoder state machine.
pub struct ProgressiveDecoder {
    max_dimension: u32,
    max_pixel_count: u64,
    unsupported: bool,
    complete: bool,
    last_emitted_hash: u64,
    last_emitted_frame: i32,
    last_stream_len: usize,
}

impl Default for ProgressiveDecoder {
    fn default() -> Self {
        Self::new(None)
    }
}

impl ProgressiveDecoder {
    pub fn new(max_dimension: Option<u32>) -> Self {
        Self {
            max_dimension: max_dimension.unwrap_or(DEFAULT_PREVIEW_DIMENSION),
            max_pixel_count: DEFAULT_MAXIMUM_PIXEL_COUNT,
            unsupported: false,
            complete: false,
            last_emitted_hash: 0,
            last_emitted_frame: -1,
            last_stream_len: 0,
        }
    }

    pub fn reset(&mut self) {
        self.unsupported = false;
        self.complete = false;
        self.last_emitted_hash = 0;
        self.last_emitted_frame = -1;
        self.last_stream_len = 0;
    }

    pub fn decode(&mut self, data: &[u8]) -> Option<DecodedPreviewFrame> {
        if self.unsupported || self.complete || data.len() < 8 {
            return None;
        }

        // If the data shrunk, a new stream was started.
        if data.len() < self.last_stream_len {
            self.reset();
        }
        self.last_stream_len = data.len();

        let format = detect_image_format(data);
        match format {
            ImageFormatKind::Jpeg => self.decode_jpeg(data),
            ImageFormatKind::Png => self.decode_png(data),
            ImageFormatKind::Gif => self.decode_gif(data),
            ImageFormatKind::Webp => self.decode_webp(data),
            ImageFormatKind::Zip => self.decode_zip(data),
            ImageFormatKind::Unknown => None,
        }
    }

    fn check_dimensions(&mut self, width: u32, height: u32, is_unscalable_codec: bool) -> bool {
        let area = (width as u64) * (height as u64);
        if area == 0 || area > MAXIMUM_IMAGE_DIMENSION_AREA {
            self.unsupported = true;
            return false;
        }
        // PNG and GIF decoders allocate their full-size canvas during progressive decoding.
        if is_unscalable_codec && area > self.max_pixel_count {
            self.unsupported = true;
            return false;
        }
        true
    }

    // ==========================================
    // JPEG Progressive & Scanline Decoding
    // ==========================================

    fn decode_jpeg(&mut self, data: &[u8]) -> Option<DecodedPreviewFrame> {
        let slice = data;

        let options = DecoderOptions::default()
            .jpeg_set_out_colorspace(ColorSpace::BGRA)
            .set_strict_mode(false);

        let mut decoder = JpegDecoder::new_with_options(ZCursor::new(slice), options);
        if decoder.decode_headers().is_err() {
            return None;
        }

        let info = decoder.info()?;
        let orig_w = info.width as u32;
        let orig_h = info.height as u32;

        if !self.check_dimensions(orig_w, orig_h, false) {
            return None;
        }

        let mut pixels = decoder.decode().ok()?;
        let expected_len = (orig_w * orig_h * 4) as usize;
        if pixels.len() < expected_len {
            pixels.resize(expected_len, 0);
        }

        let row_bytes = (orig_w * 4) as usize;
        let mut first_undecoded_row = orig_h;
        for row in (0..orig_h).rev() {
            let row_start = (row as usize) * row_bytes;
            let row_end = row_start + row_bytes;
            let row_slice = &pixels[row_start..row_end];
            let has_undecoded = row_slice.chunks_exact(4).any(|p| p[3] != 255);
            if has_undecoded {
                first_undecoded_row = row;
            } else {
                break;
            }
        }

        if first_undecoded_row < orig_h {
            let undecoded_start = (first_undecoded_row as usize) * row_bytes;
            pixels[undecoded_start..].fill(0);
        }

        let is_complete = data.ends_with(&[0xFF, 0xD9]);
        if is_complete && first_undecoded_row == orig_h {
            self.complete = true;
            return self.scale_and_publish_forced(orig_w, orig_h, pixels);
        }

        self.scale_and_publish(orig_w, orig_h, pixels)
    }

    // ==========================================
    // PNG Incremental Scanline Decoding
    // ==========================================

    fn decode_png(&mut self, data: &[u8]) -> Option<DecodedPreviewFrame> {
        let cursor = Cursor::new(data);
        let mut decoder = png::Decoder::new(cursor);
        decoder.ignore_checksums(true);

        let mut reader = match decoder.read_info() {
            Ok(r) => r,
            Err(_) => return None,
        };

        let (w, h) = (reader.info().width, reader.info().height);
        if !self.check_dimensions(w, h, true) {
            return None;
        }

        let mut canvas = vec![0u8; (w * h * 4) as usize];
        let mut rows_decoded = 0;
        let info = reader.info().clone();

        while let Ok(Some(row)) = reader.next_row() {
            let row_idx = rows_decoded as usize;
            let row_start = row_idx * (w as usize) * 4;
            let row_end = row_start + (w as usize) * 4;
            if row_end <= canvas.len() {
                copy_png_row_to_rgba(row.data(), &mut canvas[row_start..row_end], &info);
                rows_decoded += 1;
            }
        }

        if rows_decoded == 0 {
            // If reader.next_row() didn't produce rows yet because of chunk buffering,
            // check if standard image load succeeds on full data.
            if let Ok(img) = image::load_from_memory_with_format(data, image::ImageFormat::Png) {
                let rgba = img.to_rgba8();
                let (iw, ih) = (rgba.width(), rgba.height());
                if !self.check_dimensions(iw, ih, true) {
                    return None;
                }
                let bgra = rgba_to_bgra(rgba.as_raw());
                self.complete = true;
                return self.scale_and_publish(iw, ih, bgra);
            }
            return None;
        }

        if rows_decoded == h {
            self.complete = true;
            let bgra = rgba_to_bgra(&canvas);
            return self.scale_and_publish_forced(w, h, bgra);
        }

        let bgra = rgba_to_bgra(&canvas);
        self.scale_and_publish(w, h, bgra)
    }

    // ==========================================
    // GIF Animated Frame Sniffing
    // ==========================================

    fn decode_gif(&mut self, data: &[u8]) -> Option<DecodedPreviewFrame> {
        let mut data_with_trailer;
        let slice = if data.ends_with(&[0x3B]) {
            data
        } else {
            data_with_trailer = Vec::with_capacity(data.len() + 1);
            data_with_trailer.extend_from_slice(data);
            data_with_trailer.push(0x3B);
            &data_with_trailer[..]
        };

        let cursor = Cursor::new(slice);
        let decoder = image::codecs::gif::GifDecoder::new(cursor).ok()?;

        let mut latest_frame = None;
        let mut frame_count = 0;
        for frame_res in decoder.into_frames() {
            match frame_res {
                Ok(frame) => {
                    latest_frame = Some(frame);
                    frame_count += 1;
                }
                Err(_) => break,
            }
        }

        if frame_count > 1 {
            println!("decode_gif at len={}: frame_count={frame_count}, last_emitted={}", data.len(), self.last_emitted_frame);
        }

        if frame_count > self.last_emitted_frame && latest_frame.is_some() {
            self.last_emitted_frame = frame_count;
            let frame = latest_frame.unwrap();
            let rgba = frame.into_buffer();
            let (w, h) = (rgba.width(), rgba.height());
            if !self.check_dimensions(w, h, true) {
                return None;
            }
            let bgra = rgba_to_bgra(rgba.as_raw());
            return self.scale_and_publish_forced(w, h, bgra);
        }

        None
    }

    // ==========================================
    // WebP Frame Sniffing & Decoding
    // ==========================================

    fn decode_webp(&mut self, data: &[u8]) -> Option<DecodedPreviewFrame> {
        // Check if WebP is animated (VP8X chunk with animation bit set)
        let is_animated = data.len() >= 21 && &data[12..16] == b"VP8X" && (data[20] & 2) != 0;

        if is_animated {
            return self.sniff_webp_animated_frame(data);
        }

        // Standard standalone single-frame WebP
        if let Ok(img) = image::load_from_memory_with_format(data, image::ImageFormat::WebP) {
            let rgba = img.to_rgba8();
            let (w, h) = (rgba.width(), rgba.height());
            if !self.check_dimensions(w, h, false) {
                return None;
            }
            let bgra = rgba_to_bgra(rgba.as_raw());
            self.complete = true;
            return self.scale_and_publish(w, h, bgra);
        }

        None
    }

    fn sniff_webp_animated_frame(&mut self, data: &[u8]) -> Option<DecodedPreviewFrame> {
        if data.len() < 30 {
            return None;
        }

        let mut offset = 12; // Skip RIFF + len + WEBP
        let mut frame_index = 0;
        let mut latest_frame_subchunk = None;

        while offset + 8 <= data.len() {
            let tag = &data[offset..offset + 4];
            let chunk_len = u32::from_le_bytes([
                data[offset + 4],
                data[offset + 5],
                data[offset + 6],
                data[offset + 7],
            ]) as usize;

            let chunk_data_start = offset + 8;
            let chunk_data_end = chunk_data_start + chunk_len;
            let padded_chunk_end = (chunk_data_end + 1) & !1;

            if data.len() < chunk_data_end {
                break;
            }

            if tag == b"ANMF" && chunk_len >= 16 {
                let sub_data = &data[chunk_data_start + 16..chunk_data_end];
                latest_frame_subchunk = Some((frame_index, sub_data));
                frame_index += 1;
            }

            offset = padded_chunk_end;
        }

        if let Some((idx, subchunk)) = latest_frame_subchunk {
            if idx > self.last_emitted_frame {
                // Wrap subchunk into a valid standalone WebP RIFF container
                let mut standalone_webp = Vec::with_capacity(12 + subchunk.len());
                standalone_webp.extend_from_slice(b"RIFF");
                standalone_webp.extend_from_slice(&((4 + subchunk.len()) as u32).to_le_bytes());
                standalone_webp.extend_from_slice(b"WEBP");
                standalone_webp.extend_from_slice(subchunk);

                if let Ok(img) = image::load_from_memory(&standalone_webp) {
                    self.last_emitted_frame = idx;
                    let rgba = img.to_rgba8();
                    let (w, h) = (rgba.width(), rgba.height());
                    if !self.check_dimensions(w, h, false) {
                        return None;
                    }
                    let bgra = rgba_to_bgra(rgba.as_raw());
                    return self.scale_and_publish_forced(w, h, bgra);
                }
            }
        }

        None
    }

    // ==========================================
    // Ugoira Zip Sniffing & Frame Decoding
    // ==========================================

    fn decode_zip(&mut self, data: &[u8]) -> Option<DecodedPreviewFrame> {
        let mut cursor = 0;
        let mut latest_entry = None;

        while data.len().saturating_sub(cursor) >= 30 {
            if data[cursor..cursor + 4] != [0x50, 0x4b, 0x03, 0x04] {
                break;
            }
            let flags = u16::from_le_bytes([data[cursor + 6], data[cursor + 7]]);
            let method = u16::from_le_bytes([data[cursor + 8], data[cursor + 9]]);
            let compressed_size = u32::from_le_bytes([
                data[cursor + 18],
                data[cursor + 19],
                data[cursor + 20],
                data[cursor + 21],
            ]) as usize;
            let uncompressed_size = u32::from_le_bytes([
                data[cursor + 22],
                data[cursor + 23],
                data[cursor + 24],
                data[cursor + 25],
            ]) as usize;
            let filename_len = u16::from_le_bytes([data[cursor + 26], data[cursor + 27]]) as usize;
            let extra_len = u16::from_le_bytes([data[cursor + 28], data[cursor + 29]]) as usize;

            if (flags & 9) != 0 || (method != 0 && method != 8) || compressed_size > 16 * 1024 * 1024 {
                break;
            }

            let data_start = cursor + 30 + filename_len + extra_len;
            let data_end = data_start + compressed_size;
            if data.len() < data_end {
                break;
            }

            cursor = data_end;
            let entry_bytes = &data[data_start..data_end];
            if method == 0 {
                latest_entry = Some(entry_bytes.to_vec());
            } else if method == 8 {
                use std::io::Read;
                let mut deflater = flate2::read::DeflateDecoder::new(entry_bytes);
                let mut decompressed = Vec::with_capacity(uncompressed_size);
                if deflater.read_to_end(&mut decompressed).is_ok() {
                    latest_entry = Some(decompressed);
                }
            }
        }

        if let Some(entry_data) = latest_entry {
            if let Ok(img) = image::load_from_memory(&entry_data) {
                let rgba = img.to_rgba8();
                let (w, h) = (rgba.width(), rgba.height());
                if !self.check_dimensions(w, h, false) {
                    return None;
                }
                let bgra = rgba_to_bgra(rgba.as_raw());
                return self.scale_and_publish(w, h, bgra);
            }
        }

        None
    }

    // ==========================================
    // Scaling & Emission
    // ==========================================

    fn scale_and_publish(
        &mut self,
        orig_w: u32,
        orig_h: u32,
        bgra_pixels: Vec<u8>,
    ) -> Option<DecodedPreviewFrame> {
        self.scale_and_publish_impl(orig_w, orig_h, bgra_pixels, false)
    }

    fn scale_and_publish_forced(
        &mut self,
        orig_w: u32,
        orig_h: u32,
        bgra_pixels: Vec<u8>,
    ) -> Option<DecodedPreviewFrame> {
        self.scale_and_publish_impl(orig_w, orig_h, bgra_pixels, true)
    }

    fn scale_and_publish_impl(
        &mut self,
        orig_w: u32,
        orig_h: u32,
        bgra_pixels: Vec<u8>,
        force: bool,
    ) -> Option<DecodedPreviewFrame> {
        let max_side = orig_w.max(orig_h);
        let scale = if max_side > self.max_dimension {
            self.max_dimension as f64 / max_side as f64
        } else {
            1.0
        };

        let target_w = ((orig_w as f64 * scale).round() as u32).max(1);
        let target_h = ((orig_h as f64 * scale).round() as u32).max(1);

        if (target_w as u64) * (target_h as u64) > self.max_pixel_count {
            self.unsupported = true;
            return None;
        }

        let final_bgra = if scale < 0.999 {
            if let Some(img) = image::ImageBuffer::<image::Rgba<u8>, _>::from_raw(orig_w, orig_h, bgra_pixels) {
                let resized = image::imageops::resize(&img, target_w, target_h, image::imageops::FilterType::Triangle);
                resized.into_raw()
            } else {
                return None;
            }
        } else {
            bgra_pixels
        };

        let hash = compute_pixel_hash(&final_bgra);
        if !force && hash == self.last_emitted_hash {
            return None;
        }
        self.last_emitted_hash = hash;

        Some(DecodedPreviewFrame {
            width: target_w,
            height: target_h,
            bgra_data: final_bgra,
        })
    }
}

fn compute_pixel_hash(data: &[u8]) -> u64 {
    // Fast 64-bit sampling hash
    let mut h: u64 = 0xcbf29ce484222325;
    let step = (data.len() / 128).max(1);
    for &byte in data.iter().step_by(step) {
        h ^= byte as u64;
        h = h.wrapping_mul(0x100000001b3);
    }
    for &byte in data.iter().rev().take(64) {
        h ^= byte as u64;
        h = h.wrapping_mul(0x100000001b3);
    }
    h ^= data.len() as u64;
    h
}

fn rgba_to_bgra(rgba: &[u8]) -> Vec<u8> {
    let mut bgra = rgba.to_vec();
    for chunk in bgra.chunks_exact_mut(4) {
        chunk.swap(0, 2); // R <-> B
    }
    bgra
}

fn copy_png_row_to_rgba(src_row: &[u8], dst_row: &mut [u8], info: &png::Info) {
    if info.bit_depth != png::BitDepth::Eight {
        return;
    }

    match info.color_type {
        png::ColorType::Rgba => {
            let len = src_row.len().min(dst_row.len());
            dst_row[..len].copy_from_slice(&src_row[..len]);
        }
        png::ColorType::Rgb => {
            let pixels = src_row.len() / 3;
            for i in 0..pixels {
                let si = i * 3;
                let di = i * 4;
                if di + 3 < dst_row.len() {
                    dst_row[di] = src_row[si];
                    dst_row[di + 1] = src_row[si + 1];
                    dst_row[di + 2] = src_row[si + 2];
                    dst_row[di + 3] = 255;
                }
            }
        }
        png::ColorType::Grayscale => {
            for (i, &gray) in src_row.iter().enumerate() {
                let di = i * 4;
                if di + 3 < dst_row.len() {
                    dst_row[di] = gray;
                    dst_row[di + 1] = gray;
                    dst_row[di + 2] = gray;
                    dst_row[di + 3] = 255;
                }
            }
        }
        png::ColorType::GrayscaleAlpha => {
            let pixels = src_row.len() / 2;
            for i in 0..pixels {
                let si = i * 2;
                let di = i * 4;
                if di + 3 < dst_row.len() {
                    let gray = src_row[si];
                    let alpha = src_row[si + 1];
                    dst_row[di] = gray;
                    dst_row[di + 1] = gray;
                    dst_row[di + 2] = gray;
                    dst_row[di + 3] = alpha;
                }
            }
        }
        _ => {}
    }
}

// ==========================================
// UniFFI Export Object
// ==========================================

#[derive(uniffi::Object)]
pub struct ProgressiveImageDecoder {
    inner: parking_lot::Mutex<ProgressiveDecoder>,
}

#[uniffi::export]
impl ProgressiveImageDecoder {
    #[uniffi::constructor]
    pub fn new(max_dimension: Option<u32>) -> Arc<Self> {
        Arc::new(Self {
            inner: parking_lot::Mutex::new(ProgressiveDecoder::new(max_dimension)),
        })
    }

    pub fn decode(&self, data: Vec<u8>) -> Option<DecodedPreviewFrame> {
        self.inner.lock().decode(&data)
    }

    pub fn reset(&self) {
        self.inner.lock().reset();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    fn fixture(name: &str) -> PathBuf {
        PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .parent()
            .unwrap()
            .parent()
            .unwrap()
            .join("src")
            .join("Pixeval.Tests")
            .join("Fixtures")
            .join("ProgressiveImages")
            .join(name)
    }

    #[test]
    fn test_progressive_jpeg_changes_as_scans_arrive() {
        let encoded = std::fs::read(fixture("progressive.jpg")).expect("read progressive.jpg");
        let mut decoder = ProgressiveDecoder::new(None);

        let third_len = encoded.len() / 3;
        let partial = decoder.decode(&encoded[..third_len]);
        assert!(partial.is_some(), "Partial progressive JPEG should decode");
        let partial_frame = partial.unwrap();
        let before_bytes = partial_frame.bgra_data.clone();

        let complete = decoder.decode(&encoded);
        assert!(complete.is_some(), "Complete progressive JPEG should decode");
        let complete_frame = complete.unwrap();
        assert_ne!(before_bytes, complete_frame.bgra_data);
    }

    #[test]
    fn test_partial_baseline_jpeg_does_not_publish_undecoded_rows() {
        use image::{ImageBuffer, Rgb};
        let mut rng_state: u64 = 42;
        let mut next_byte = || -> u8 {
            rng_state = rng_state.wrapping_mul(6364136223846793005).wrapping_add(1);
            (rng_state >> 33) as u8
        };
        let img: ImageBuffer<Rgb<u8>, _> = ImageBuffer::from_fn(256, 256, |_x, _y| {
            Rgb([next_byte(), next_byte(), next_byte()])
        });
        let mut jpeg_bytes = Vec::new();
        let mut encoder = image::codecs::jpeg::JpegEncoder::new(&mut jpeg_bytes);
        encoder.encode_image(&img).unwrap();

        let mut decoder = ProgressiveDecoder::new(None);
        let partial = decoder.decode(&jpeg_bytes[..jpeg_bytes.len() / 2]);
        assert!(partial.is_some(), "Partial baseline JPEG should decode");
        let frame = partial.unwrap();

        // Top-left pixel should have Alpha == 255
        let top_alpha = frame.bgra_data[3];
        assert_eq!(top_alpha, 255, "Top row must be decoded with alpha 255");

        // Bottom row should be transparent (all components 0)
        let w = frame.width as usize;
        let h = frame.height as usize;
        let bottom_row_start = (h - 1) * w * 4;
        let bottom_row = &frame.bgra_data[bottom_row_start..bottom_row_start + w * 4];
        for (i, &b) in bottom_row.iter().enumerate() {
            assert_eq!(b, 0, "Undecoded bottom row byte at {i} must be 0");
        }
    }

    #[test]
    fn test_animated_gif_shows_new_frames() {
        let encoded = std::fs::read(fixture("frames.gif")).expect("read frames.gif");
        let mut decoder = ProgressiveDecoder::new(None);
        let mut saw_red = false;
        let mut saw_green = false;

        for index in 0..encoded.len() {
            if let Some(frame) = decoder.decode(&encoded[..=index]) {
                let b = frame.bgra_data[0];
                let g = frame.bgra_data[1];
                let r = frame.bgra_data[2];
                let a = frame.bgra_data[3];
                println!("GIF index {index}: r={r}, g={g}, b={b}, a={a}");
                if r > 200 && g < 20 {
                    saw_red = true;
                }
                if g > 100 && r < 20 {
                    saw_green = true;
                }
            }
        }

        assert!(saw_red, "No first frame for frames.gif");
        assert!(saw_green, "No next frame before EOF for frames.gif");
    }

    #[test]
    fn test_animated_webp_shows_new_frames() {
        let encoded = std::fs::read(fixture("frames.webp")).expect("read frames.webp");
        let mut decoder = ProgressiveDecoder::new(None);
        let mut saw_red = false;
        let mut saw_green = false;

        for index in 0..encoded.len() {
            if let Some(frame) = decoder.decode(&encoded[..=index]) {
                let b = frame.bgra_data[0];
                let g = frame.bgra_data[1];
                let r = frame.bgra_data[2];
                let a = frame.bgra_data[3];
                println!("At index {index}: B={b}, G={g}, R={r}, A={a}");
                if r > 200 && g < 20 {
                    saw_red = true;
                }
                if g > 100 && r < 20 {
                    saw_green = true;
                }
            }
        }

        assert!(saw_red, "No first frame for frames.webp");
        assert!(saw_green, "No next frame before EOF for frames.webp");
    }

    #[test]
    fn test_png_incremental_small_chunks() {
        use image::{ImageBuffer, Rgba};
        let mut rng_state: u64 = 42;
        let mut next_byte = || -> u8 {
            rng_state = rng_state.wrapping_mul(6364136223846793005).wrapping_add(1);
            (rng_state >> 33) as u8
        };
        let img: ImageBuffer<Rgba<u8>, _> = ImageBuffer::from_fn(128, 128, |_x, _y| {
            Rgba([next_byte(), next_byte(), next_byte(), 255])
        });
        let mut png_bytes = Vec::new();
        use image::ImageEncoder;
        let encoder = image::codecs::png::PngEncoder::new(&mut png_bytes);
        encoder.write_image(img.as_raw(), 128, 128, image::ExtendedColorType::Rgba8).unwrap();

        let mut decoder = ProgressiveDecoder::new(None);
        let mut updates = 0;
        let mut last_frame = None;

        for offset in (0..png_bytes.len()).step_by(997) {
            let end = (offset + 997).min(png_bytes.len());
            let chunk = &png_bytes[..end];
            if let Some(frame) = decoder.decode(chunk) {
                updates += 1;
                last_frame = Some(frame);
            }
        }

        assert!(updates >= 2, "Expected multiple updates, got {updates}");
        assert!(last_frame.is_some());
    }

    #[test]
    fn test_oversized_image_does_not_allocate() {
        // Construct a PNG header for 2049x2049
        let mut png_data = Vec::new();
        {
            let mut encoder = png::Encoder::new(&mut png_data, 2049, 2049);
            encoder.set_color(png::ColorType::Rgba);
            encoder.set_depth(png::BitDepth::Eight);
            let _ = encoder.write_header();
        }

        let mut decoder = ProgressiveDecoder::new(None);
        let res = decoder.decode(&png_data);
        assert!(res.is_none(), "Oversized unscalable image should return None");
    }

    #[test]
    fn test_short_header_and_reset() {
        let encoded = std::fs::read(fixture("progressive.jpg")).expect("read progressive.jpg");
        let mut decoder = ProgressiveDecoder::new(None);

        let r1 = decoder.decode(&encoded[..6]);
        let r2 = decoder.decode(&encoded[..encoded.len() / 3]);
        assert!(r1.is_none());
        assert!(r2.is_some());

        decoder.reset();
        let r3 = decoder.decode(&encoded[..encoded.len() * 2 / 3]);
        assert!(r3.is_some());
    }
}
