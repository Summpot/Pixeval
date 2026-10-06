// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

/// Separates interleaved RGBA pixels into planar layout: [R...][G...][B...][A...].
/// `src` must have length `num_pixels * 4`. `dst` must have length `num_pixels * 4`.
pub fn separate_rgba_to_planar(src: &[u8], dst: &mut [u8]) {
    assert_eq!(src.len(), dst.len());
    let num_pixels = src.len() / 4;
    if num_pixels == 0 {
        return;
    }

    #[cfg(target_arch = "x86_64")]
    {
        if is_x86_feature_detected!("avx2") {
            unsafe {
                avx2_separate_rgba(src, dst);
                return;
            }
        }
    }

    #[cfg(target_arch = "aarch64")]
    {
        unsafe {
            neon_separate_rgba(src, dst);
            return;
        }
    }

    scalar_separate_rgba(src, dst);
}

/// Combines planar RGBA pixels [R...][G...][B...][A...] into interleaved BGRA format (B, G, R, A, ...).
/// Matches Avalonia's PixelFormat.Bgra8888.
pub fn combine_planar_to_bgra(src_planar: &[u8], dst_bgra: &mut [u8]) {
    assert_eq!(src_planar.len(), dst_bgra.len());
    let num_pixels = src_planar.len() / 4;
    if num_pixels == 0 {
        return;
    }

    #[cfg(target_arch = "x86_64")]
    {
        if is_x86_feature_detected!("avx2") {
            unsafe {
                avx2_combine_planar_to_bgra(src_planar, dst_bgra);
                return;
            }
        }
    }

    #[cfg(target_arch = "aarch64")]
    {
        unsafe {
            neon_combine_planar_to_bgra(src_planar, dst_bgra);
            return;
        }
    }

    scalar_combine_planar_to_bgra(src_planar, dst_bgra);
}

fn scalar_separate_rgba(src: &[u8], dst: &mut [u8]) {
    let num_pixels = src.len() / 4;
    let (r, rest) = dst.split_at_mut(num_pixels);
    let (g, rest) = rest.split_at_mut(num_pixels);
    let (b, a) = rest.split_at_mut(num_pixels);

    for i in 0..num_pixels {
        let base = i * 4;
        r[i] = src[base];
        g[i] = src[base + 1];
        b[i] = src[base + 2];
        a[i] = src[base + 3];
    }
}

fn scalar_combine_planar_to_bgra(src: &[u8], dst: &mut [u8]) {
    let num_pixels = src.len() / 4;
    let (r, rest) = src.split_at(num_pixels);
    let (g, rest) = rest.split_at(num_pixels);
    let (b, a) = rest.split_at(num_pixels);

    for i in 0..num_pixels {
        let base = i * 4;
        dst[base] = b[i];     // Blue
        dst[base + 1] = g[i]; // Green
        dst[base + 2] = r[i]; // Red
        dst[base + 3] = a[i]; // Alpha
    }
}

#[cfg(target_arch = "x86_64")]
#[target_feature(enable = "avx2,ssse3")]
unsafe fn avx2_separate_rgba(src: &[u8], dst: &mut [u8]) {
    use std::arch::x86_64::*;

    let num_pixels = src.len() / 4;
    let (r, rest) = dst.split_at_mut(num_pixels);
    let (g, rest) = rest.split_at_mut(num_pixels);
    let (b, a) = rest.split_at_mut(num_pixels);

    let shuffle_mask = _mm_setr_epi8(
        0, 4, 8, 12,    // R
        1, 5, 9, 13,    // G
        2, 6, 10, 14,   // B
        3, 7, 11, 15,   // A
    );

    let mut i = 0;
    while i + 16 <= num_pixels {
        unsafe {
            let v0 = _mm_loadu_si128(src.as_ptr().add(i * 4) as *const __m128i);
            let v1 = _mm_loadu_si128(src.as_ptr().add(i * 4 + 16) as *const __m128i);
            let v2 = _mm_loadu_si128(src.as_ptr().add(i * 4 + 32) as *const __m128i);
            let v3 = _mm_loadu_si128(src.as_ptr().add(i * 4 + 48) as *const __m128i);

            let s0 = _mm_shuffle_epi8(v0, shuffle_mask);
            let s1 = _mm_shuffle_epi8(v1, shuffle_mask);
            let s2 = _mm_shuffle_epi8(v2, shuffle_mask);
            let s3 = _mm_shuffle_epi8(v3, shuffle_mask);

            let t0 = _mm_unpacklo_epi32(s0, s1);
            let t1 = _mm_unpackhi_epi32(s0, s1);
            let t2 = _mm_unpacklo_epi32(s2, s3);
            let t3 = _mm_unpackhi_epi32(s2, s3);

            let res_r = _mm_unpacklo_epi64(t0, t2);
            let res_g = _mm_unpackhi_epi64(t0, t2);
            let res_b = _mm_unpacklo_epi64(t1, t3);
            let res_a = _mm_unpackhi_epi64(t1, t3);

            _mm_storeu_si128(r.as_mut_ptr().add(i) as *mut __m128i, res_r);
            _mm_storeu_si128(g.as_mut_ptr().add(i) as *mut __m128i, res_g);
            _mm_storeu_si128(b.as_mut_ptr().add(i) as *mut __m128i, res_b);
            _mm_storeu_si128(a.as_mut_ptr().add(i) as *mut __m128i, res_a);
        }
        i += 16;
    }

    while i < num_pixels {
        let base = i * 4;
        r[i] = src[base];
        g[i] = src[base + 1];
        b[i] = src[base + 2];
        a[i] = src[base + 3];
        i += 1;
    }
}

#[cfg(target_arch = "x86_64")]
#[target_feature(enable = "avx2,ssse3")]
unsafe fn avx2_combine_planar_to_bgra(src: &[u8], dst: &mut [u8]) {
    use std::arch::x86_64::*;

    let num_pixels = src.len() / 4;
    let (r, rest) = src.split_at(num_pixels);
    let (g, rest) = rest.split_at(num_pixels);
    let (b, a) = rest.split_at(num_pixels);

    let mut i = 0;
    while i + 16 <= num_pixels {
        unsafe {
            let vr = _mm_loadu_si128(r.as_ptr().add(i) as *const __m128i);
            let vg = _mm_loadu_si128(g.as_ptr().add(i) as *const __m128i);
            let vb = _mm_loadu_si128(b.as_ptr().add(i) as *const __m128i);
            let va = _mm_loadu_si128(a.as_ptr().add(i) as *const __m128i);

            // Goal: Interleave into BGRA
            // Interleave B and G:
            let bg_lo = _mm_unpacklo_epi8(vb, vg); // B0 G0 B1 G1 ... B7 G7
            let bg_hi = _mm_unpackhi_epi8(vb, vg); // B8 G8 B9 G9 ... B15 G15

            // Interleave R and A:
            let ra_lo = _mm_unpacklo_epi8(vr, va); // R0 A0 R1 A1 ... R7 A7
            let ra_hi = _mm_unpackhi_epi8(vr, va); // R8 A8 R9 A9 ... R15 A15

            // Now interleave 16-bit words (BG and RA) to get 32-bit BGRA:
            let bgra0 = _mm_unpacklo_epi16(bg_lo, ra_lo); // Pixels 0..3
            let bgra1 = _mm_unpackhi_epi16(bg_lo, ra_lo); // Pixels 4..7
            let bgra2 = _mm_unpacklo_epi16(bg_hi, ra_hi); // Pixels 8..11
            let bgra3 = _mm_unpackhi_epi16(bg_hi, ra_hi); // Pixels 12..15

            _mm_storeu_si128(dst.as_mut_ptr().add(i * 4) as *mut __m128i, bgra0);
            _mm_storeu_si128(dst.as_mut_ptr().add(i * 4 + 16) as *mut __m128i, bgra1);
            _mm_storeu_si128(dst.as_mut_ptr().add(i * 4 + 32) as *mut __m128i, bgra2);
            _mm_storeu_si128(dst.as_mut_ptr().add(i * 4 + 48) as *mut __m128i, bgra3);
        }
        i += 16;
    }

    while i < num_pixels {
        let base = i * 4;
        dst[base] = b[i];
        dst[base + 1] = g[i];
        dst[base + 2] = r[i];
        dst[base + 3] = a[i];
        i += 1;
    }
}

#[cfg(target_arch = "aarch64")]
unsafe fn neon_separate_rgba(src: &[u8], dst: &mut [u8]) {
    use std::arch::aarch64::*;

    let num_pixels = src.len() / 4;
    let (r, rest) = dst.split_at_mut(num_pixels);
    let (g, rest) = rest.split_at_mut(num_pixels);
    let (b, a) = rest.split_at_mut(num_pixels);

    let mut i = 0;
    while i + 16 <= num_pixels {
        unsafe {
            let data = vld4q_u8(src.as_ptr().add(i * 4));
            vst1q_u8(r.as_mut_ptr().add(i), data.0);
            vst1q_u8(g.as_mut_ptr().add(i), data.1);
            vst1q_u8(b.as_mut_ptr().add(i), data.2);
            vst1q_u8(a.as_mut_ptr().add(i), data.3);
        }
        i += 16;
    }

    while i < num_pixels {
        let base = i * 4;
        r[i] = src[base];
        g[i] = src[base + 1];
        b[i] = src[base + 2];
        a[i] = src[base + 3];
        i += 1;
    }
}

#[cfg(target_arch = "aarch64")]
unsafe fn neon_combine_planar_to_bgra(src: &[u8], dst: &mut [u8]) {
    use std::arch::aarch64::*;

    let num_pixels = src.len() / 4;
    let (r, rest) = src.split_at(num_pixels);
    let (g, rest) = rest.split_at(num_pixels);
    let (b, a) = rest.split_at(num_pixels);

    let mut i = 0;
    while i + 16 <= num_pixels {
        unsafe {
            let vr = vst1q_u8(r.as_ptr().add(i));
            let vg = vst1q_u8(g.as_ptr().add(i));
            let vb = vst1q_u8(b.as_ptr().add(i));
            let va = vst1q_u8(a.as_ptr().add(i));

            // Write as BGRA
            let bgra = uint8x16x4_t(vb, vg, vr, va);
            vst4q_u8(dst.as_mut_ptr().add(i * 4), bgra);
        }
        i += 16;
    }

    while i < num_pixels {
        let base = i * 4;
        dst[base] = b[i];
        dst[base + 1] = g[i];
        dst[base + 2] = r[i];
        dst[base + 3] = a[i];
        i += 1;
    }
}
