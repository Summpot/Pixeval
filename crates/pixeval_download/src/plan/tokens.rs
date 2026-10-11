// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

/// Resolves `<ext>`, `<ext:u>`, `<ext:l>`, `<pic_set_index>` and formatted indexes.
/// Tokens are replaced before characters that are illegal in a final path are removed,
/// so a reduced macro such as `work.<ext>` still receives the real extension.
pub fn resolve_file_tokens(path: &str, extension: &str, set_index: i32) -> String {
    let with_ext = replace_token_values(path, "<ext", |formatter| format_string(extension, formatter));
    replace_token_values(&with_ext, "<pic_set_index", |formatter| {
        format_integer(set_index as i64, formatter)
    })
}

pub fn remove_extension_tokens(path: &str) -> String {
    let without_dot = replace_token_values(path, ".<ext", |_| String::new());
    replace_token_values(&without_dot, "<ext", |_| String::new())
}

pub fn change_extension_token(path: &str, extension: &str) -> String {
    resolve_file_tokens(path, extension.trim_start_matches('.'), -1)
}

pub fn normalize_final_path(path: &str) -> String {
    let cleaned: String = path
        .chars()
        .filter(|c| !matches!(c, '*' | '?' | '"' | '|' | '<' | '>') && !c.is_control())
        .collect();
    let trimmed = cleaned.trim().trim_end_matches(['.', ' ', '/']);
    if trimmed.is_empty() {
        return String::new();
    }
    match std::path::absolute(trimmed) {
        Ok(abs) => abs.to_string_lossy().trim_end_matches('.').to_string(),
        Err(_) => trimmed.trim_end_matches('.').to_string(),
    }
}

pub fn join_base(base_dir: &str, relative: &str) -> String {
    let base = base_dir.trim().trim_end_matches(['/', '\\']);
    let rel = relative.trim().trim_start_matches(['/', '\\']);
    if base.is_empty() {
        rel.to_string()
    } else if rel.is_empty() {
        base.to_string()
    } else {
        format!("{base}/{rel}")
    }
}

pub fn url_extension(url: &str) -> String {
    let path = url.split(['?', '#']).next().unwrap_or(url);
    let name = path.rsplit(['/', '\\']).next().unwrap_or(path);
    match name.rsplit_once('.') {
        Some((_, ext))
            if !ext.is_empty()
                && ext.len() <= 8
                && ext.chars().all(|c| c.is_ascii_alphanumeric()) =>
        {
            ext.to_string()
        }
        _ => String::new(),
    }
}

fn format_string(value: &str, formatter: Option<&str>) -> String {
    match formatter {
        Some("u") => value.to_uppercase(),
        Some("l") => value.to_lowercase(),
        _ => value.to_string(),
    }
}

fn format_integer(value: i64, formatter: Option<&str>) -> String {
    match formatter {
        Some(fmt) if !fmt.is_empty() && fmt.chars().all(|c| c == '0') => {
            format!("{:0width$}", value, width = fmt.len())
        }
        _ => value.to_string(),
    }
}

fn replace_token_values(path: &str, token_prefix: &str, mut value_factory: impl FnMut(Option<&str>) -> String) -> String {
    let mut output = String::with_capacity(path.len());
    let bytes = path.as_bytes();
    let prefix = token_prefix.as_bytes();
    let mut position = 0usize;
    while position < bytes.len() {
        if let Some(rel) = path[position..].find(token_prefix) {
            let start = position + rel;
            output.push_str(&path[position..start]);
            let after = start + prefix.len();
            if after >= bytes.len() {
                output.push_str(&path[start..]);
                break;
            }
            match bytes[after] as char {
                '>' => {
                    output.push_str(&value_factory(None));
                    position = after + 1;
                }
                ':' => {
                    let formatter_start = after + 1;
                    if let Some(end_rel) = path[formatter_start..].find('>') {
                        let token_end = formatter_start + end_rel;
                        output.push_str(&value_factory(Some(&path[formatter_start..token_end])));
                        position = token_end + 1;
                    } else {
                        output.push_str(&path[start..]);
                        break;
                    }
                }
                _ => {
                    output.push_str(token_prefix);
                    position = after;
                }
            }
        } else {
            output.push_str(&path[position..]);
            break;
        }
    }
    output
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn replaces_extension_and_set_index() {
        let path = resolve_file_tokens("work.<ext:u>_p<pic_set_index:00>", "jpg", 3);
        assert_eq!(path, "work.JPG_p03");
    }

    #[test]
    fn removes_extension_tokens() {
        assert_eq!(remove_extension_tokens("dir/work.<ext>"), "dir/work");
        assert_eq!(remove_extension_tokens("dir/work<ext:u>"), "dir/work");
    }
}
