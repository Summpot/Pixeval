uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod engine;
pub mod metapath;

pub use engine::*;
pub use metapath::*;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_valid_macro_analysis() {
        let result = analyze("@{id}");
        assert!(result.is_success);
        assert_eq!(result.highlights.len(), 4);
    }

    #[test]
    fn test_formatted_transducer_analysis() {
        let result = analyze("@{ext:u}");
        assert!(result.is_success);
        assert_eq!(result.highlights.len(), 6);
    }

    #[test]
    fn test_missing_brace_diagnostic() {
        let result = analyze("@{id");
        assert!(!result.is_success);
        assert_eq!(
            result.diagnostics[0].kind,
            MacroDiagnosticKind::MissingRightBrace
        );
    }

    #[test]
    fn test_unknown_macro_diagnostic() {
        let result = analyze("@{missing}");
        assert!(!result.is_success);
        assert_eq!(
            result.diagnostics[0].kind,
            MacroDiagnosticKind::UnknownMacroName
        );
    }

    #[test]
    fn test_predicate_without_branches() {
        let result = analyze("@{is_pic_set}");
        assert!(!result.is_success);
        assert_eq!(
            result.diagnostics[0].kind,
            MacroDiagnosticKind::ConditionalBranchesMissing
        );
    }

    #[test]
    fn test_macro_reduction() {
        let mut context = MacroContext::default();
        context.artwork_id = "12345678".to_string();
        context.create_date = "2020-10-12T12:00:00Z".to_string();

        let path = reduce("@{id}", &context).unwrap();
        assert_eq!(path, "12345678");

        let path = reduce("@{publish_time:yyyy-MM-dd}", &context).unwrap();
        assert_eq!(path, "2020-10-12");

        let path = reduce("@{ext}", &context).unwrap();
        assert_eq!(path, "<ext>");
    }

    #[test]
    fn test_conditional_reduction() {
        let mut context = MacroContext::default();
        context.artwork_id = "100".to_string();
        context.image_type = "ImageSet".to_string();

        let path = reduce("@{is_pic_set?set:single}", &context).unwrap();
        assert_eq!(path, "set");

        context.image_type = "SingleImage".to_string();
        let path = reduce("@{is_pic_set?set:single}", &context).unwrap();
        assert_eq!(path, "single");
    }

    #[tokio::test]
    async fn test_download_manager_queue_and_cancel() {
        let manager = DownloadManager::new(2, None, None);
        let key = DownloadTaskKey::new_ordinary("test_path.jpg");
        manager.enqueue_task(
            key.clone(),
            "https://example.com/fake.jpg".to_string(),
            "test_path.jpg".to_string(),
            true,
        );

        assert!(manager.cancel_task(key.clone()));
        assert_eq!(manager.get_task_state(&key), Some(DownloadState::Cancelled));
    }

    #[tokio::test]
    async fn test_download_manager_network_options() {
        let mut static_ips = std::collections::HashMap::new();
        static_ips.insert(
            "i.pximg.net".to_string(),
            vec!["210.140.139.134".to_string()],
        );
        let options = DownloadNetworkOptions {
            proxy_url: Some("http://127.0.0.1:7890".to_string()),
            static_domain_ips: static_ips,
        };
        let manager = DownloadManager::new(2, None, Some(options));
        assert_eq!(manager.list_tasks().len(), 0);

        // Test dynamic network options update
        manager.update_network_options(None);
    }
}
