// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Debug, Clone)]
pub struct Utf16Mapping {
    points: Vec<(usize, usize)>,
    total_utf16: usize,
    total_bytes: usize,
}

impl Utf16Mapping {
    pub fn new(text: &str) -> Self {
        let mut points = Vec::with_capacity(text.len() / 2 + 1);
        let mut utf16_offset = 0;
        let mut byte_offset = 0;
        for c in text.chars() {
            points.push((utf16_offset, byte_offset));
            utf16_offset += c.len_utf16();
            byte_offset += c.len_utf8();
        }
        points.push((utf16_offset, byte_offset));
        Self {
            points,
            total_utf16: utf16_offset,
            total_bytes: byte_offset,
        }
    }

    #[inline]
    pub fn total_utf16(&self) -> usize {
        self.total_utf16
    }

    #[inline]
    pub fn total_bytes(&self) -> usize {
        self.total_bytes
    }

    pub fn utf16_to_byte(&self, utf16_pos: usize) -> usize {
        if utf16_pos >= self.total_utf16 {
            return self.total_bytes;
        }
        match self.points.binary_search_by_key(&utf16_pos, |&(u, _)| u) {
            Ok(idx) => self.points[idx].1,
            Err(idx) => {
                if idx > 0 {
                    self.points[idx - 1].1
                } else {
                    0
                }
            }
        }
    }

    pub fn byte_to_utf16(&self, byte_pos: usize) -> usize {
        if byte_pos >= self.total_bytes {
            return self.total_utf16;
        }
        match self.points.binary_search_by_key(&byte_pos, |&(_, b)| b) {
            Ok(idx) => self.points[idx].0,
            Err(idx) => {
                if idx > 0 {
                    self.points[idx - 1].0
                } else {
                    0
                }
            }
        }
    }
}
