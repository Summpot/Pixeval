// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use pixeval_booru::{BooruPost};
use pixeval_mako::{Illustration, Novel, WorkEntry};
use pixeval_saucenao::SauceNaoItem;
use crate::error::StorageError;

pub const ILLUSTRATION_SERIALIZE_KEY: &str = "Mako.Model.Illustration";
pub const NOVEL_SERIALIZE_KEY: &str = "Mako.Model.Novel";
pub const BOORU_SERIALIZE_KEY: &str = "Imouto.BooruParser.Post";
pub const SAUCENAO_SERIALIZE_KEY: &str = "Pixeval.SauceNao.SauceNaoItem";

#[boltffi::data]
#[derive(uniffi::Enum, Clone, Debug, PartialEq)]
pub enum ArtworkPayload {
    Work { entry: WorkEntry },
    Booru { post: BooruPost },
    SauceNao { item: SauceNaoItem },
}

pub fn serialize_key(payload: &ArtworkPayload) -> &'static str {
    match payload {
        ArtworkPayload::Work { entry: WorkEntry::Illust { .. } } => ILLUSTRATION_SERIALIZE_KEY,
        ArtworkPayload::Work { entry: WorkEntry::NovelWork { .. } } => NOVEL_SERIALIZE_KEY,
        ArtworkPayload::Booru { .. } => BOORU_SERIALIZE_KEY,
        ArtworkPayload::SauceNao { .. } => SAUCENAO_SERIALIZE_KEY,
    }
}

pub fn artwork_id(payload: &ArtworkPayload) -> String {
    match payload {
        ArtworkPayload::Work { entry: WorkEntry::Illust { illustration } } => illustration.id.to_string(),
        ArtworkPayload::Work { entry: WorkEntry::NovelWork { novel } } => novel.id.to_string(),
        ArtworkPayload::Booru { post } => post.id.clone(),
        ArtworkPayload::SauceNao { item } => item
            .artwork_id
            .clone()
            .filter(|id| !id.is_empty())
            .unwrap_or_else(|| item.index_id.to_string()),
    }
}

pub fn work_key(payload: &ArtworkPayload) -> String {
    format!("{}:{}", serialize_key(payload), artwork_id(payload))
}

pub fn serialize(payload: &ArtworkPayload) -> Result<(String, String), StorageError> {
    let json = match payload {
        ArtworkPayload::Work { entry: WorkEntry::Illust { illustration } } => serde_json::to_string(illustration),
        ArtworkPayload::Work { entry: WorkEntry::NovelWork { novel } } => serde_json::to_string(novel),
        ArtworkPayload::Booru { post } => serde_json::to_string(post),
        ArtworkPayload::SauceNao { item } => serde_json::to_string(item),
    }
    ?;
    Ok((serialize_key(payload).to_string(), json))
}

pub fn hydrate(key: Option<&str>, json: &str) -> Option<ArtworkPayload> {
    match key? {
        ILLUSTRATION_SERIALIZE_KEY => hydrate_illustration(json),
        NOVEL_SERIALIZE_KEY => hydrate_novel(json),
        BOORU_SERIALIZE_KEY => hydrate_booru(json),
        SAUCENAO_SERIALIZE_KEY => hydrate_saucenao(json),
        _ => None,
    }
}

#[boltffi::export]
#[uniffi::export]
pub fn artwork_work_key(payload: ArtworkPayload) -> String {
    work_key(&payload)
}

#[boltffi::export]
#[uniffi::export]
pub fn artwork_serialize_key(payload: ArtworkPayload) -> String {
    serialize_key(&payload).to_string()
}

#[boltffi::export]
#[uniffi::export]
pub fn artwork_payload_id(payload: ArtworkPayload) -> String {
    artwork_id(&payload)
}

fn hydrate_illustration(json: &str) -> Option<ArtworkPayload> {
    let illustration: Illustration = serde_json::from_str(json).ok()?;
    illustration_has_payload(&illustration).then_some(ArtworkPayload::Work {
        entry: WorkEntry::Illust { illustration },
    })
}

fn hydrate_novel(json: &str) -> Option<ArtworkPayload> {
    let novel: Novel = serde_json::from_str(json).ok()?;
    (novel.id != 0).then_some(ArtworkPayload::Work {
        entry: WorkEntry::NovelWork { novel },
    })
}

fn hydrate_booru(json: &str) -> Option<ArtworkPayload> {
    let post: BooruPost = serde_json::from_str(json).ok()?;
    (!post.id.is_empty()).then_some(ArtworkPayload::Booru { post })
}

fn hydrate_saucenao(json: &str) -> Option<ArtworkPayload> {
    let item: SauceNaoItem = serde_json::from_str(json).ok()?;
    let has_payload = item.index_id != 0
        || item.artwork_id.as_ref().is_some_and(|id| !id.is_empty())
        || !item.thumbnail_url.is_empty()
        || !item.title.is_empty();
    has_payload.then_some(ArtworkPayload::SauceNao { item })
}

fn illustration_has_payload(illustration: &Illustration) -> bool {
    illustration.id != 0
        || illustration.image_urls.square_medium.is_some()
        || illustration.image_urls.medium.is_some()
        || illustration.image_urls.large.is_some()
        || illustration.image_urls.original.is_some()
}

#[cfg(test)]
mod tests {
    use super::*;
    use pixeval_mako::{IllustContentType, ImageUrls, User};

    fn user() -> User {
        User {
            id: 2,
            name: "a".to_string(),
            account: "a".to_string(),
            ..User::default()
        }
    }

    fn sample_illust() -> Illustration {
        Illustration {
            id: 1,
            title: "gif".to_string(),
            illust_type: IllustContentType::Ugoira,
            image_urls: ImageUrls {
                medium: Some("https://example.com/m.jpg".into()),
                ..ImageUrls::default()
            },
            user: user(),
            create_date: "2020-01-01T00:00:00Z".into(),
            ..Illustration::default()
        }
    }

    #[test]
    fn hydrate_snake_case_ugoira() {
        let json = r#"{"id":1,"title":"gif","type":"ugoira","image_urls":{"medium":"https://example.com/m.jpg"},"user":{"id":2,"name":"a","account":"a"},"create_date":"2020-01-01T00:00:00Z"}"#;
        let ArtworkPayload::Work {
            entry: WorkEntry::Illust { illustration },
        } = hydrate(Some(ILLUSTRATION_SERIALIZE_KEY), json).unwrap()
        else {
            panic!("expected illustration");
        };
        assert_eq!(illustration.id, 1);
        assert_eq!(illustration.illust_type, IllustContentType::Ugoira);
    }

    #[test]
    fn roundtrip_preserves_illust_type() {
        let payload = ArtworkPayload::Work {
            entry: WorkEntry::Illust {
                illustration: sample_illust(),
            },
        };
        let (key, json) = serialize(&payload).unwrap();
        let ArtworkPayload::Work {
            entry: WorkEntry::Illust { illustration },
        } = hydrate(Some(&key), &json).unwrap()
        else {
            panic!("expected illustration");
        };
        assert_eq!(illustration.illust_type, IllustContentType::Ugoira);
        assert_eq!(work_key(&payload), "Mako.Model.Illustration:1");
    }

    #[test]
    fn roundtrips_all_current_payload_kinds() {
        let payloads = [
            ArtworkPayload::Work { entry: WorkEntry::Illust { illustration: sample_illust() } },
            ArtworkPayload::Work { entry: WorkEntry::NovelWork { novel: Novel { id: 4, title: "novel".into(), user: user(), ..Novel::default() } } },
            ArtworkPayload::Booru { post: pixeval_booru::BooruPost { id: "5".into(), ..Default::default() } },
            ArtworkPayload::SauceNao { item: SauceNaoItem { index_id: 6, ..Default::default() } },
        ];
        for payload in payloads {
            let (key, json) = serialize(&payload).unwrap();
            assert_eq!(hydrate(Some(&key), &json), Some(payload));
            assert!(hydrate(Some(&key), "broken").is_none());
        }
    }

    #[test]
    fn rejects_unknown_keys_and_old_shapes() {
        let payload = ArtworkPayload::Work { entry: WorkEntry::Illust { illustration: sample_illust() } };
        let (_, json) = serialize(&payload).unwrap();
        for key in [None, Some(""), Some("Illustration"), Some("Pixeval.Mako.Illustration"), Some("unknown")] {
            assert!(hydrate(key, &json).is_none());
        }
        assert!(hydrate(Some(ILLUSTRATION_SERIALIZE_KEY), r#"{"Id":1,"Title":"old"}"#).is_none());
        assert!(hydrate(Some(BOORU_SERIALIZE_KEY), r#"{"Id":{"Id":"1"},"FileResolution":{"Width":1}}"#).is_none());
    }

    #[test]
    fn hydrate_rejects_empty_illustration() {
        assert!(hydrate(Some(ILLUSTRATION_SERIALIZE_KEY), r#"{"title":"Test Artwork"}"#).is_none());
    }
}
