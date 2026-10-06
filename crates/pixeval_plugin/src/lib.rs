uniffi::setup_scaffolding!();

// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

pub mod engine;
pub mod error;
pub mod models;

pub use engine::*;
pub use error::*;
pub use models::*;

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;

    #[test]
    fn test_plugin_metadata_serde() {
        let meta = PluginMetadata {
            id: "test-plugin".to_string(),
            name: "Test Plugin".to_string(),
            author: "Tester".to_string(),
            version: "1.0.0".to_string(),
            description: "A test plugin".to_string(),
            sdk_version: "5.0.0".to_string(),
            library_path: "".to_string(),
            extensions: vec![PluginExtensionDescriptor {
                kind: "image_transformer".to_string(),
                label: "Grayscale".to_string(),
                description: "Converts images to grayscale".to_string(),
                identifier: "test.grayscale".to_string(),
            }],
            is_active: true,
        };

        let json = serde_json::to_string(&meta).unwrap();
        let deserialized: PluginMetadata = serde_json::from_str(&json).unwrap();
        assert_eq!(meta, deserialized);
    }

    #[test]
    fn test_plugin_engine_lifecycle_and_registration() {
        let engine = PluginHostEngine::new("5.0.0".to_string());
        assert_eq!(engine.current_sdk_version(), "5.0.0");

        let meta = PluginMetadata {
            id: "plugin-1".to_string(),
            name: "My Plugin".to_string(),
            author: "Author".to_string(),
            version: "1.0.0".to_string(),
            description: "Test".to_string(),
            sdk_version: "5.0.0".to_string(),
            library_path: "/path/to/lib".to_string(),
            extensions: vec![],
            is_active: true,
        };

        engine.register_metadata(meta.clone());
        assert_eq!(engine.get_loaded_plugins().len(), 1);
        assert_eq!(engine.get_plugin("plugin-1".to_string()).unwrap().name, "My Plugin");

        assert!(engine.set_plugin_active("plugin-1".to_string(), false).unwrap());
        assert!(!engine.get_plugin("plugin-1".to_string()).unwrap().is_active);

        assert!(engine.unload_plugin("plugin-1".to_string()).unwrap());
        assert_eq!(engine.get_loaded_plugins().len(), 0);
        assert!(engine.unload_plugin("plugin-1".to_string()).is_err());
    }

    #[test]
    fn test_plugin_enumeration() {
        let dir = tempfile::tempdir().unwrap();
        let sub = dir.path().join("sub");
        fs::create_dir(&sub).unwrap();

        let ext = if cfg!(target_os = "windows") {
            "dll"
        } else if cfg!(target_os = "macos") {
            "dylib"
        } else {
            "so"
        };

        fs::write(dir.path().join(format!("plugin1.{ext}")), b"dummy").unwrap();
        fs::write(sub.join(format!("plugin2.{ext}")), b"dummy").unwrap();
        fs::write(dir.path().join("ignore.txt"), b"dummy").unwrap();

        let engine = PluginHostEngine::new("5.0.0".to_string());
        let discovered = engine.enumerate_plugins(dir.path().to_string_lossy().to_string());
        assert_eq!(discovered.len(), 2);
    }
}
