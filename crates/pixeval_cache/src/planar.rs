// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use crate::error::CacheError;
use crate::simd::{combine_planar_to_bgra, separate_rgba_to_planar};

pub const PLANAR_MAGIC: &[u8; 4] = b"PLZ4";
pub const HEADER_SIZE: usize = 12; // 4 bytes magic + 4 bytes width + 4 bytes height

pub fn is_planar_lz4(data: &[u8]) -> bool {
    data.len() >= HEADER_SIZE && &data[0..4] == PLANAR_MAGIC
}

/// Encodes raw interleaved RGBA pixels into LZ4-compressed Planar RGBA format.
pub fn encode_planar_lz4(width: u32, height: u32, rgba: &[u8]) -> Result<Vec<u8>, CacheError> {
    let expected_len = (width as usize) * (height as usize) * 4;
    if rgba.len() != expected_len {
        return Err(CacheError::Codec {
            message: format!(
                "Invalid RGBA buffer length: expected {} bytes for {}x{}, got {}",
                expected_len,
                width,
                height,
                rgba.len()
            ),
        });
    }

    // 1. Separate channels via SIMD into planar layout
    let mut planar = vec![0u8; expected_len];
    separate_rgba_to_planar(rgba, &mut planar);

    // 2. Compress planar bytes with LZ4
    let mut encoder = lz4::EncoderBuilder::new()
        .level(4)
        .build(Vec::with_capacity(expected_len / 2))
        .map_err(|e| CacheError::Codec {
            message: format!("LZ4 encoder build failed: {e}"),
        })?;

    use std::io::Write;
    encoder.write_all(&planar).map_err(|e| CacheError::Codec {
        message: format!("LZ4 write failed: {e}"),
    })?;

    let (compressed_payload, res) = encoder.finish();
    res.map_err(|e| CacheError::Codec {
        message: format!("LZ4 finish failed: {e}"),
    })?;

    // 3. Assemble header + compressed payload
    let mut out = Vec::with_capacity(HEADER_SIZE + compressed_payload.len());
    out.extend_from_slice(PLANAR_MAGIC);
    out.extend_from_slice(&width.to_le_bytes());
    out.extend_from_slice(&height.to_le_bytes());
    out.extend_from_slice(&compressed_payload);

    Ok(out)
}

/// Decompresses LZ4-compressed Planar RGBA and writes directly into destination BGRA buffer using SIMD.
pub fn decode_planar_lz4_to_bgra(compressed: &[u8], dst_bgra: &mut [u8]) -> Result<(u32, u32), CacheError> {
    if compressed.len() < HEADER_SIZE || &compressed[0..4] != PLANAR_MAGIC {
        return Err(CacheError::CorruptedData);
    }

    let width = u32::from_le_bytes(
        compressed[4..8]
            .try_into()
            .map_err(|_| CacheError::CorruptedData)?,
    );
    let height = u32::from_le_bytes(
        compressed[8..12]
            .try_into()
            .map_err(|_| CacheError::CorruptedData)?,
    );

    let raw_len = (width as usize) * (height as usize) * 4;
    if dst_bgra.len() < raw_len {
        return Err(CacheError::Codec {
            message: format!(
                "Destination buffer too small: expected >= {} bytes, got {}",
                raw_len,
                dst_bgra.len()
            ),
        });
    }

    // Decompress LZ4 payload into planar buffer
    let payload = &compressed[HEADER_SIZE..];
    let mut decoder = lz4::Decoder::new(payload).map_err(|e| CacheError::Codec {
        message: format!("LZ4 decoder failed: {e}"),
    })?;

    let mut planar = Vec::with_capacity(raw_len);
    use std::io::Read;
    decoder.read_to_end(&mut planar).map_err(|e| CacheError::Codec {
        message: format!("LZ4 read failed: {e}"),
    })?;

    if planar.len() != raw_len {
        return Err(CacheError::CorruptedData);
    }

    // Recombine channels with SIMD directly into destination BGRA buffer
    combine_planar_to_bgra(&planar, &mut dst_bgra[..raw_len]);

    Ok((width, height))
}

/// Decompresses LZ4-compressed Planar RGBA into a newly allocated BGRA vector.
pub fn decode_planar_lz4_to_bgra_vec(compressed: &[u8]) -> Result<(u32, u32, Vec<u8>), CacheError> {
    if compressed.len() < HEADER_SIZE || &compressed[0..4] != PLANAR_MAGIC {
        return Err(CacheError::CorruptedData);
    }

    let width = u32::from_le_bytes(compressed[4..8].try_into().map_err(|_| CacheError::CorruptedData)?);
    let height = u32::from_le_bytes(compressed[8..12].try_into().map_err(|_| CacheError::CorruptedData)?);
    let raw_len = (width as usize) * (height as usize) * 4;

    let mut dst = vec![0u8; raw_len];
    decode_planar_lz4_to_bgra(compressed, &mut dst)?;
    Ok((width, height, dst))
}
