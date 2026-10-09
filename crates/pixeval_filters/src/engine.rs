use std::sync::Arc;
use parking_lot::RwLock;

use crate::builtin::{
    build_artwork_full_completions, build_artwork_intrinsics, build_artwork_syntaxes,
    build_artwork_value_hints,
};
use crate::completions::{
    AuthorCandidate, FilterCompletionDefinition, FilterLocalization, FilterStorageProvider,
    FilterValueCompletionCallback, FilterValueCompletionContext,
};
use crate::eval::{filter_artworks, ArtworkMetadata, ArtworkTag};
use crate::language::{FilterAnalysisResult, FilterLanguage};

struct EngineCandidateProvider<'a> {
    storage: Option<&'a dyn FilterStorageProvider>,
    session_tags: &'a [ArtworkTag],
    session_authors: &'a [AuthorCandidate],
}

impl<'a> FilterValueCompletionCallback for EngineCandidateProvider<'a> {
    fn get_completions(
        &self,
        context: &FilterValueCompletionContext,
    ) -> Vec<FilterCompletionDefinition> {
        let frag_str = context.fragment_span.get_text(&context.source);
        let fragment = frag_str.trim().to_lowercase();
        let mut results = Vec::new();
        let mut seen = std::collections::HashSet::new();

        if context.match_syntax_key == "Tag" {
            // 1. Session tags from current view
            for tag in self.session_tags {
                let name_lower = tag.name.to_lowercase();
                let trans_lower = tag.translated_name.as_deref().unwrap_or("").to_lowercase();
                if (fragment.is_empty() || name_lower.contains(&fragment) || trans_lower.contains(&fragment))
                    && seen.insert(tag.name.clone())
                {
                    results.push(FilterCompletionDefinition::new(
                        format!("tag:{}", tag.name),
                        &tag.name,
                        &tag.name,
                        tag.translated_name.clone(),
                    ));
                }
            }

            // 2. Storage search history
            if let Some(storage) = self.storage {
                let records = storage.query_search_history_tags(frag_str.to_string(), 30);
                for r in records {
                    if seen.insert(r.name.clone()) {
                        results.push(FilterCompletionDefinition::new(
                            format!("tag:{}", r.name),
                            &r.name,
                            &r.name,
                            r.translated_name,
                        ));
                    }
                }
            }
        } else if context.match_syntax_key == "Author" {
            // 1. Session authors from current view
            for author in self.session_authors {
                let name_lower = author.name.to_lowercase();
                let account_lower = author.account.as_deref().unwrap_or("").to_lowercase();
                if (fragment.is_empty() || name_lower.contains(&fragment) || account_lower.contains(&fragment))
                    && seen.insert(author.name.clone())
                {
                    results.push(FilterCompletionDefinition::new(
                        format!("author:{}", author.name),
                        &author.name,
                        &author.name,
                        author.account.clone(),
                    ));
                }
            }

            // 2. Storage subscription authors
            if let Some(storage) = self.storage {
                let subs = storage.query_subscription_authors(frag_str.to_string(), 30);
                for s in subs {
                    if seen.insert(s.name.clone()) {
                        results.push(FilterCompletionDefinition::new(
                            format!("author:{}", s.name),
                            &s.name,
                            &s.name,
                            s.account,
                        ));
                    }
                }
            }
        }

        // Rank results: exact starts_with > contains
        results.sort_by(|a, b| {
            let a_starts = a.display_text.to_lowercase().starts_with(&fragment);
            let b_starts = b.display_text.to_lowercase().starts_with(&fragment);
            match (a_starts, b_starts) {
                (true, false) => std::cmp::Ordering::Less,
                (false, true) => std::cmp::Ordering::Greater,
                _ => a.display_text.to_lowercase().cmp(&b.display_text.to_lowercase()),
            }
        });

        results
    }
}

#[derive(uniffi::Object)]
pub struct FilterCompletionEngine {
    inner: FilterLanguage,
    storage: Option<Box<dyn FilterStorageProvider>>,
    session_tags: RwLock<Vec<ArtworkTag>>,
    session_authors: RwLock<Vec<AuthorCandidate>>,
}

#[uniffi::export]
impl FilterCompletionEngine {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Self::with_provider(None, None)
    }

    #[uniffi::constructor]
    pub fn with_provider(
        storage: Option<Box<dyn FilterStorageProvider>>,
        localization: Option<FilterLocalization>,
    ) -> Arc<Self> {
        let syntaxes = build_artwork_syntaxes(localization.as_ref());
        let intrinsics = build_artwork_intrinsics(localization.as_ref());
        let fulls = build_artwork_full_completions(localization.as_ref());
        let hints = build_artwork_value_hints();
        let inner = FilterLanguage::new(syntaxes, Some(intrinsics), Some(fulls), Some(hints));

        Arc::new(Self {
            inner,
            storage,
            session_tags: RwLock::new(Vec::new()),
            session_authors: RwLock::new(Vec::new()),
        })
    }

    pub fn set_session_artworks(&self, artworks: Vec<ArtworkMetadata>) {
        let mut tags = Vec::new();
        let mut authors = Vec::new();
        let mut seen_tags = std::collections::HashSet::new();
        let mut seen_authors = std::collections::HashSet::new();

        for art in artworks {
            if !art.author_name.is_empty() && seen_authors.insert(art.author_name.clone()) {
                let account = if art.author_account.is_empty() {
                    None
                } else {
                    Some(art.author_account)
                };
                authors.push(AuthorCandidate::new(art.author_name, account));
            }

            for t in art.tags {
                if !t.name.is_empty() && seen_tags.insert(t.name.clone()) {
                    tags.push(t);
                }
            }
        }

        *self.session_tags.write() = tags;
        *self.session_authors.write() = authors;
    }

    pub fn set_session_candidates(&self, tags: Vec<ArtworkTag>, authors: Vec<AuthorCandidate>) {
        *self.session_tags.write() = tags;
        *self.session_authors.write() = authors;
    }

    pub fn clear_session_candidates(&self) {
        self.session_tags.write().clear();
        self.session_authors.write().clear();
    }

    pub fn analyze(&self, text: String, caret_position: i32) -> FilterAnalysisResult {
        let tags_guard = self.session_tags.read();
        let authors_guard = self.session_authors.read();
        let provider = EngineCandidateProvider {
            storage: self.storage.as_deref(),
            session_tags: &tags_guard,
            session_authors: &authors_guard,
        };

        let result = self.inner.analyze(&text, caret_position, Some(&provider));

        let ast_root = result
            .query
            .as_ref()
            .map(|q| crate::ast::FilterAstNode::from(&crate::ast::FilterNode::Group(q.root.clone())));
        let query_handle = result.query.map(Arc::new);
        let has_query = query_handle.is_some();

        FilterAnalysisResult {
            has_query,
            is_success: result.is_success,
            diagnostics: result.diagnostics,
            completions: result.completions,
            query_handle,
            ast_root,
        }
    }

    pub fn filter_artworks(&self, query_text: String, artworks: Vec<ArtworkMetadata>) -> Vec<bool> {
        let analysis = self.analyze(query_text, -1);
        if let Some(query) = analysis.query_handle {
            filter_artworks(&query, &artworks)
        } else {
            vec![true; artworks.len()]
        }
    }
}
