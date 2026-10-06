// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum McpError {
    #[error("Server error: {message}")]
    Server { message: String },
    #[error("Binding error: {message}")]
    Bind { message: String },
    #[error("Protocol error: {message}")]
    Protocol { message: String },
    #[error("Tool execution failed ({tool}): {message}")]
    ToolExecution { tool: String, message: String },
    #[error("Write tools are disabled in settings")]
    WriteToolsDisabled,
    #[error("User is not logged in")]
    NotLoggedIn,
    #[error("IO error: {message}")]
    Io { message: String },
}
