// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashSet;
use marked_yaml::{Marker, Node, Span};

use crate::navigation::models::{
    NavigationDiagnostic, NavigationDiagnosticKind, NavigationParseResult,
    NavigationYamlItem, NavigationYamlSettings,
};
use crate::text::Utf16Mapping;

const MAX_DEPTH: usize = 3;
const SETTINGS_PAGE_KEY: &str = "Settings";

pub struct NavigationParser<'a> {
    text: &'a str,
    utf16_map: Utf16Mapping,
    known_pages: HashSet<String>,
    known_icons: HashSet<String>,
    known_i18n_keys: Option<HashSet<String>>,
    diagnostics: Vec<NavigationDiagnostic>,
}

impl<'a> NavigationParser<'a> {
    pub fn new(
        text: &'a str,
        known_pages: Vec<String>,
        known_icons: Vec<String>,
        known_i18n_keys: Option<Vec<String>>,
    ) -> Self {
        Self {
            text,
            utf16_map: Utf16Mapping::new(text),
            known_pages: known_pages.into_iter().map(|s| s.to_lowercase()).collect(),
            known_icons: known_icons.into_iter().map(|s| s.to_lowercase()).collect(),
            known_i18n_keys: known_i18n_keys.map(|keys| keys.into_iter().collect()),
            diagnostics: Vec::new(),
        }
    }

    pub fn parse(mut self) -> NavigationParseResult {
        if self.text.trim().is_empty() {
            let diag = self.create_diagnostic_at_start(
                NavigationDiagnosticKind::BothHeaderAndFooterEmpty,
                Vec::new(),
            );
            return NavigationParseResult {
                settings: None,
                is_valid: false,
                diagnostics: vec![diag],
            };
        }

        let root_node = match marked_yaml::parse_yaml(0, self.text) {
            Ok(node) => node,
            Err(err) => {
                let diag = self.create_load_error_diagnostic(&err);
                return NavigationParseResult {
                    settings: None,
                    is_valid: false,
                    diagnostics: vec![diag],
                };
            }
        };

        let root_map = match root_node.as_mapping() {
            Some(map) => map,
            None => {
                let diag = self.create_diagnostic(
                    NavigationDiagnosticKind::YamlSyntaxError,
                    root_node.span(),
                    vec!["Top level of navigation YAML must be a mapping".to_string()],
                );
                return NavigationParseResult {
                    settings: None,
                    is_valid: false,
                    diagnostics: vec![diag],
                };
            }
        };

        let mut new_tab: Option<String> = None;
        let mut header_items: Option<Vec<NavigationYamlItem>> = None;
        let mut footer_items: Option<Vec<NavigationYamlItem>> = None;

        // Root key validation
        for (k_scalar, v_node) in root_map.iter() {
            let key = k_scalar.as_str();
            let key_lower = key.to_lowercase();
            match key_lower.as_str() {
                "newtab" => {
                    if let Some(val_scalar) = v_node.as_scalar() {
                        let raw_val = val_scalar.as_str().trim();
                        if !raw_val.is_empty() {
                            new_tab = Some(raw_val.to_string());
                            if !self.known_pages.contains(&raw_val.to_lowercase()) {
                                self.add_diagnostic(
                                    NavigationDiagnosticKind::UnknownPage,
                                    val_scalar.span(),
                                    vec![raw_val.to_string()],
                                );
                            }
                        }
                    }
                }
                "header" => {
                    header_items = self.parse_item_sequence(v_node, "header", 1);
                }
                "footer" => {
                    footer_items = self.parse_item_sequence(v_node, "footer", 1);
                }
                _ => {
                    self.add_diagnostic(
                        NavigationDiagnosticKind::UnknownField,
                        k_scalar.span(),
                        vec![key.to_string()],
                    );
                }
            }
        }

        let header_count = header_items.as_ref().map(|v| v.len()).unwrap_or(0);
        let footer_count = footer_items.as_ref().map(|v| v.len()).unwrap_or(0);

        if header_count == 0 && footer_count == 0 {
            let diag = self.create_diagnostic_at_start(
                NavigationDiagnosticKind::BothHeaderAndFooterEmpty,
                Vec::new(),
            );
            self.diagnostics.push(diag);
        }

        let contains_settings = self.items_contain_page(header_items.as_deref(), SETTINGS_PAGE_KEY)
            || self.items_contain_page(footer_items.as_deref(), SETTINGS_PAGE_KEY);

        if !contains_settings {
            let diag = self.create_diagnostic_at_start(
                NavigationDiagnosticKind::MenuMustContainSettingsPage,
                Vec::new(),
            );
            self.diagnostics.push(diag);
        }

        let is_valid = self.diagnostics.is_empty();
        let settings = if is_valid {
            Some(NavigationYamlSettings {
                new_tab,
                header: header_items,
                footer: footer_items,
            })
        } else {
            None
        };

        NavigationParseResult {
            settings,
            is_valid,
            diagnostics: self.diagnostics,
        }
    }

    fn parse_item_sequence(
        &mut self,
        node: &Node,
        path: &str,
        depth: usize,
    ) -> Option<Vec<NavigationYamlItem>> {
        let seq = node.as_sequence()?;
        let mut items = Vec::new();
        for (i, item_node) in seq.iter().enumerate() {
            let item_path = format!("{path}[{i}]");
            if let Some(item) = self.parse_item(item_node, &item_path, depth) {
                items.push(item);
            }
        }
        Some(items)
    }

    fn parse_item(&mut self, node: &Node, path: &str, depth: usize) -> Option<NavigationYamlItem> {
        if depth > MAX_DEPTH {
            self.add_diagnostic(
                NavigationDiagnosticKind::MaxDepthExceeded,
                node.span(),
                vec![path.to_string(), MAX_DEPTH.to_string()],
            );
            return None;
        }

        let map = match node.as_mapping() {
            Some(m) => m,
            None => {
                self.add_diagnostic(
                    NavigationDiagnosticKind::ItemMustHaveEitherPageOrFolder,
                    node.span(),
                    vec![path.to_string()],
                );
                return None;
            }
        };

        let mut page: Option<String> = None;
        let mut folder: Option<String> = None;
        let mut title: Option<String> = None;
        let mut icon: Option<String> = None;
        let mut children: Option<Vec<NavigationYamlItem>> = None;

        let mut raw_keys = Vec::new();

        for (k_scalar, v_node) in map.iter() {
            let key = k_scalar.as_str();
            raw_keys.push((key, k_scalar.span(), v_node));
            let key_lower = key.to_lowercase();
            match key_lower.as_str() {
                "page" => {
                    if let Some(s) = v_node.as_scalar() {
                        page = Some(s.as_str().trim().to_string());
                    }
                }
                "folder" => {
                    if let Some(s) = v_node.as_scalar() {
                        folder = Some(s.as_str().trim().to_string());
                    }
                }
                "title" => {
                    if let Some(s) = v_node.as_scalar() {
                        title = Some(s.as_str().trim().to_string());
                    }
                }
                "icon" => {
                    if let Some(s) = v_node.as_scalar() {
                        icon = Some(s.as_str().trim().to_string());
                    }
                }
                "children" => {
                    // Handled conditionally below based on whether it's a folder
                }
                _ => {}
            }
        }

        let has_page = page.as_ref().map(|s| !s.is_empty()).unwrap_or(false);
        let has_folder = folder.as_ref().map(|s| !s.is_empty()).unwrap_or(false);

        if has_page == has_folder {
            // Disallowed field diagnostics against general item keys
            for (key, span, _) in &raw_keys {
                let kl = key.to_lowercase();
                if !matches!(kl.as_str(), "page" | "folder" | "title" | "icon" | "children") {
                    self.add_diagnostic(
                        NavigationDiagnosticKind::UnknownField,
                        span,
                        vec![key.to_string()],
                    );
                }
            }
            self.add_diagnostic(
                NavigationDiagnosticKind::ItemMustHaveEitherPageOrFolder,
                node.span(),
                vec![path.to_string()],
            );
            return None;
        }

        if has_page {
            // Disallowed field diagnostics for page item
            for (key, span, _) in &raw_keys {
                let kl = key.to_lowercase();
                if matches!(kl.as_str(), "folder" | "children") {
                    self.add_diagnostic(
                        NavigationDiagnosticKind::FieldNotAllowed,
                        span,
                        vec![path.to_string(), key.to_string()],
                    );
                } else if !matches!(kl.as_str(), "page" | "title" | "icon") {
                    self.add_diagnostic(
                        NavigationDiagnosticKind::UnknownField,
                        span,
                        vec![key.to_string()],
                    );
                }
            }

            let page_val = page.as_ref().unwrap();
            let page_node = raw_keys.iter().find(|(k, _, _)| k.eq_ignore_ascii_case("page"));
            if !self.known_pages.contains(&page_val.to_lowercase()) {
                let span = page_node.map(|(_, _, v)| v.span()).unwrap_or(node.span());
                self.add_diagnostic(
                    NavigationDiagnosticKind::UnknownPage,
                    span,
                    vec![page_val.clone()],
                );
            }

            if let Some(ref icon_val) = icon {
                if !icon_val.is_empty() && !self.known_icons.contains(&icon_val.to_lowercase()) {
                    let span = raw_keys
                        .iter()
                        .find(|(k, _, _)| k.eq_ignore_ascii_case("icon"))
                        .map(|(_, _, v)| v.span())
                        .unwrap_or(node.span());
                    self.add_diagnostic(
                        NavigationDiagnosticKind::UnknownIcon,
                        span,
                        vec![icon_val.clone()],
                    );
                }
            }

            if let Some(ref title_val) = title {
                let span = raw_keys
                    .iter()
                    .find(|(k, _, _)| k.eq_ignore_ascii_case("title"))
                    .map(|(_, _, v)| v.span())
                    .unwrap_or(node.span());
                self.validate_i18n_text(title_val, "title", span);
            }

            Some(NavigationYamlItem {
                page,
                folder: None,
                title,
                icon,
                children: None,
            })
        } else {
            // Folder item
            if depth >= MAX_DEPTH {
                self.add_diagnostic(
                    NavigationDiagnosticKind::MaxDepthExceededFolder,
                    node.span(),
                    vec![path.to_string(), MAX_DEPTH.to_string()],
                );
                return None;
            }

            for (key, span, _) in &raw_keys {
                let kl = key.to_lowercase();
                if matches!(kl.as_str(), "page" | "title") {
                    self.add_diagnostic(
                        NavigationDiagnosticKind::FieldNotAllowed,
                        span,
                        vec![path.to_string(), key.to_string()],
                    );
                } else if !matches!(kl.as_str(), "folder" | "icon" | "children") {
                    self.add_diagnostic(
                        NavigationDiagnosticKind::UnknownField,
                        span,
                        vec![key.to_string()],
                    );
                }
            }

            let folder_val = folder.as_ref().unwrap();
            let folder_node = raw_keys.iter().find(|(k, _, _)| k.eq_ignore_ascii_case("folder"));
            let folder_span = folder_node.map(|(_, _, v)| v.span()).unwrap_or(node.span());
            self.validate_i18n_text(folder_val, "folder", folder_span);

            if let Some(ref icon_val) = icon {
                if !icon_val.is_empty() && !self.known_icons.contains(&icon_val.to_lowercase()) {
                    let span = raw_keys
                        .iter()
                        .find(|(k, _, _)| k.eq_ignore_ascii_case("icon"))
                        .map(|(_, _, v)| v.span())
                        .unwrap_or(node.span());
                    self.add_diagnostic(
                        NavigationDiagnosticKind::UnknownIcon,
                        span,
                        vec![icon_val.clone()],
                    );
                }
            }

            let children_node = raw_keys
                .iter()
                .find(|(k, _, _)| k.eq_ignore_ascii_case("children"))
                .map(|(_, _, v)| *v);

            if let Some(c_node) = children_node {
                let parsed_children = self.parse_item_sequence(c_node, &format!("{path}.children"), depth + 1);
                let empty = parsed_children.as_ref().map(|v| v.is_empty()).unwrap_or(true);
                if empty {
                    self.add_diagnostic(
                        NavigationDiagnosticKind::EmptyFolder,
                        folder_span,
                        vec![folder_val.clone()],
                    );
                }
                children = parsed_children;
            } else {
                self.add_diagnostic(
                    NavigationDiagnosticKind::EmptyFolder,
                    folder_span,
                    vec![folder_val.clone()],
                );
            }

            Some(NavigationYamlItem {
                page: None,
                folder,
                title: None,
                icon,
                children,
            })
        }
    }

    fn validate_i18n_text(&mut self, text: &str, field_name: &str, span: &Span) {
        let trimmed = text.trim();
        if !trimmed.starts_with('$') {
            return;
        }

        if trimmed.starts_with("$$") {
            return;
        }

        let resource_key = trimmed[1..].trim();
        if resource_key.is_empty() {
            self.add_diagnostic(
                NavigationDiagnosticKind::I18nResourceKeyEmpty,
                span,
                vec![field_name.to_string()],
            );
            return;
        }

        if let Some(ref known_keys) = self.known_i18n_keys {
            if !known_keys.contains(resource_key) {
                self.add_diagnostic(
                    NavigationDiagnosticKind::UnknownI18nResourceKey,
                    span,
                    vec![resource_key.to_string()],
                );
            }
        }
    }

    fn items_contain_page(&self, items: Option<&[NavigationYamlItem]>, page_key: &str) -> bool {
        let items = match items {
            Some(i) => i,
            None => return false,
        };

        for item in items {
            if let Some(ref p) = item.page {
                if p.eq_ignore_ascii_case(page_key) {
                    return true;
                }
            }
            if let Some(ref ch) = item.children {
                if self.items_contain_page(Some(ch), page_key) {
                    return true;
                }
            }
        }

        false
    }

    fn add_diagnostic(
        &mut self,
        kind: NavigationDiagnosticKind,
        span: &Span,
        arguments: Vec<String>,
    ) {
        let diag = self.create_diagnostic(kind, span, arguments);
        self.diagnostics.push(diag);
    }

    fn create_diagnostic(
        &self,
        kind: NavigationDiagnosticKind,
        span: &Span,
        arguments: Vec<String>,
    ) -> NavigationDiagnostic {
        let (start, length, line, column) = self.span_to_utf16_coords(span);
        NavigationDiagnostic {
            kind,
            message: String::new(),
            start,
            length,
            line,
            column,
            arguments,
        }
    }

    fn create_diagnostic_at_start(
        &self,
        kind: NavigationDiagnosticKind,
        arguments: Vec<String>,
    ) -> NavigationDiagnostic {
        let length = if self.text.is_empty() { 1 } else { 1 };
        NavigationDiagnostic {
            kind,
            message: String::new(),
            start: 0,
            length,
            line: 1,
            column: 1,
            arguments,
        }
    }

    fn create_load_error_diagnostic(&self, err: &marked_yaml::LoadError) -> NavigationDiagnostic {
        let marker = match err {
            marked_yaml::LoadError::TopLevelMustBeMapping(m) => Some(*m),
            marked_yaml::LoadError::TopLevelMustBeSequence(m) => Some(*m),
            marked_yaml::LoadError::UnexpectedAnchor(m) => Some(*m),
            marked_yaml::LoadError::MappingKeyMustBeScalar(m) => Some(*m),
            marked_yaml::LoadError::UnexpectedTag(m) => Some(*m),
            marked_yaml::LoadError::ScanError(m, _) => Some(*m),
            marked_yaml::LoadError::DuplicateKey(d) => d.key.span().start().copied(),
        };

        let (start, length, line, column) = if let Some(m) = marker {
            self.marker_to_utf16_coords(&m)
        } else {
            (0, 1, 1, 1)
        };

        NavigationDiagnostic {
            kind: NavigationDiagnosticKind::YamlSyntaxError,
            message: err.to_string(),
            start,
            length,
            line,
            column,
            arguments: vec![err.to_string()],
        }
    }

    fn span_to_utf16_coords(&self, span: &Span) -> (i32, i32, i32, i32) {
        let start_marker = span.start().copied();
        let end_marker = span.end().copied();

        let start_byte = start_marker.map(|m| m.character()).unwrap_or(0);
        let end_byte = end_marker
            .map(|m| m.character())
            .unwrap_or(start_byte.saturating_add(1));

        let utf16_start = self.utf16_map.byte_to_utf16(start_byte) as i32;
        let utf16_end = self.utf16_map.byte_to_utf16(end_byte) as i32;

        let length = if utf16_end > utf16_start {
            utf16_end - utf16_start
        } else {
            1
        };

        let line = start_marker.map(|m| m.line() as i32).unwrap_or(1);
        let column = start_marker.map(|m| m.column() as i32).unwrap_or(1);

        (utf16_start, length, line, column)
    }

    fn marker_to_utf16_coords(&self, marker: &Marker) -> (i32, i32, i32, i32) {
        let byte_pos = marker.character();
        let utf16_start = self.utf16_map.byte_to_utf16(byte_pos) as i32;
        (utf16_start, 1, marker.line() as i32, marker.column() as i32)
    }
}
