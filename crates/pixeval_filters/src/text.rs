#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default, uniffi::Record)]
pub struct FilterTextSpan {
    pub start: i32,
    pub length: i32,
}

impl FilterTextSpan {
    pub const fn new(start: i32, length: i32) -> Self {
        Self { start, length }
    }

    pub const fn from_bounds(start: i32, end: i32) -> Self {
        Self {
            start,
            length: if end >= start { end - start } else { 0 },
        }
    }

    pub const fn empty_at(position: i32) -> Self {
        Self {
            start: position,
            length: 0,
        }
    }

    pub const fn end(&self) -> i32 {
        self.start + self.length
    }

    pub fn get_text<'a>(&self, source: &'a str) -> &'a str {
        if self.length <= 0 || self.start < 0 {
            return "";
        }
        let mapping = Utf16Mapping::new(source);
        mapping.slice(source, *self)
    }
}

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

    pub fn slice<'a>(&self, text: &'a str, span: FilterTextSpan) -> &'a str {
        if span.length <= 0 || span.start < 0 {
            return "";
        }
        let b_start = self.utf16_to_byte(span.start as usize);
        let b_end = self.utf16_to_byte(span.end() as usize);
        if b_start >= text.len() {
            ""
        } else if b_end >= text.len() {
            &text[b_start..]
        } else {
            &text[b_start..b_end]
        }
    }
}
