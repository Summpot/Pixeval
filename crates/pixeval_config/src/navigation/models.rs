// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, uniffi::Enum)]
pub enum NavigationDiagnosticKind {
    YamlSyntaxError,
    BothHeaderAndFooterEmpty,
    EmptyFolder,
    FieldNotAllowed,
    I18nResourceKeyEmpty,
    ItemMustHaveEitherPageOrFolder,
    MaxDepthExceededFolder,
    MaxDepthExceeded,
    MenuMustContainSettingsPage,
    UnknownField,
    UnknownI18nResourceKey,
    UnknownIcon,
    UnknownPage,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct NavigationDiagnostic {
    pub kind: NavigationDiagnosticKind,
    pub message: String,
    pub start: i32,
    pub length: i32,
    pub line: i32,
    pub column: i32,
    pub arguments: Vec<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, serde::Serialize, serde::Deserialize, uniffi::Record)]
#[serde(rename_all = "camelCase")]
pub struct NavigationYamlSettings {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub new_tab: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub header: Option<Vec<NavigationYamlItem>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub footer: Option<Vec<NavigationYamlItem>>,
}

#[derive(Debug, Clone, PartialEq, Eq, serde::Serialize, serde::Deserialize, uniffi::Record)]
#[serde(rename_all = "camelCase")]
pub struct NavigationYamlItem {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub page: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub folder: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub title: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub icon: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub children: Option<Vec<NavigationYamlItem>>,
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct NavigationParseResult {
    pub settings: Option<NavigationYamlSettings>,
    pub is_valid: bool,
    pub diagnostics: Vec<NavigationDiagnostic>,
}
