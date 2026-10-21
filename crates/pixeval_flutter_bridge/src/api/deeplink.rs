#[derive(Clone, Debug, PartialEq, Eq)]
pub enum ParsedPixivLink {
    Artwork { id: i64 },
    Novel { id: i64 },
    User { id: i64 },
    Unknown { raw_url: String },
}

pub fn parse_pixiv_url(url: String) -> ParsedPixivLink {
    fn positive_id(text: &str) -> Option<i64> {
        text.parse::<i64>().ok().filter(|id| *id > 0)
    }
    if let Ok(uri) = url::Url::parse(url.trim()) {
        let parts: Vec<_> = uri
            .path_segments()
            .into_iter()
            .flatten()
            .filter(|p| !p.is_empty())
            .collect();
        if uri.scheme() == "pixiv" {
            if let Some(id) = parts.first().and_then(|v| positive_id(v)) {
                match uri.host_str() {
                    Some("illusts") => return ParsedPixivLink::Artwork { id },
                    Some("novels") => return ParsedPixivLink::Novel { id },
                    Some("users") => return ParsedPixivLink::User { id },
                    _ => {}
                }
            }
        } else if matches!(uri.scheme(), "https" | "http")
            && matches!(uri.host_str(), Some("pixiv.net" | "www.pixiv.net"))
        {
            for pair in parts.windows(2) {
                if let Some(id) = positive_id(pair[1]) {
                    match pair[0] {
                        "artworks" => return ParsedPixivLink::Artwork { id },
                        "users" => return ParsedPixivLink::User { id },
                        _ => {}
                    }
                }
            }
            if parts.windows(2).any(|p| p == ["novel", "show.php"]) {
                if let Some(id) = uri
                    .query_pairs()
                    .find(|(key, _)| key == "id")
                    .and_then(|(_, v)| positive_id(&v))
                {
                    return ParsedPixivLink::Novel { id };
                }
            }
        }
    }
    ParsedPixivLink::Unknown { raw_url: url }
}
