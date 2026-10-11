// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum PluginError {
    #[error("Native dynamic library load failed: {message}")]
    LibraryLoadFailed { message: String },
    #[error("Missing entry point: {entry_point}")]
    MissingEntryPoint { entry_point: String },
    #[error("Entry point invocation failed: {message}")]
    InvocationFailed { message: String },
    #[error("Outdated SDK version (expected {current_version}, got {plugin_version})")]
    OutdatedSdk {
        current_version: String,
        plugin_version: String,
    },
    #[error("Plugin initialization failed: {message}")]
    InitializationFailed { message: String },
    #[error("Extension load failed: {message}")]
    ExtensionLoadFailed { message: String },
    #[error("Plugin not found: {id}")]
    NotFound { id: String },
    #[error("Invalid plugin archive: {message}")]
    InvalidArchive { message: String },
    #[error("Plugin installation failed: {message}")]
    InstallFailed { message: String },
    #[error("Security violation: {message}")]
    Security { message: String },
    #[error("IO error: {message}")]
    Io { message: String },
}
