use crate::values::FilterValueKind;

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct FilterSyntaxPattern {
    pub prefix: String,
    pub aliases: Vec<String>,
    pub suffix: String,
    pub metadata: Option<String>,
    pub example_value: Option<String>,
    pub description: Option<String>,
}

impl FilterSyntaxPattern {
    pub fn new(
        prefix: impl Into<String>,
        aliases: Vec<String>,
        suffix: impl Into<String>,
        metadata: Option<String>,
        example_value: Option<String>,
        description: Option<String>,
    ) -> Self {
        Self {
            prefix: prefix.into(),
            aliases,
            suffix: suffix.into(),
            metadata,
            example_value,
            description,
        }
    }

    pub fn default_pattern(example_value: Option<String>, description: Option<String>) -> Self {
        Self {
            prefix: String::new(),
            aliases: vec![String::new()],
            suffix: String::new(),
            metadata: None,
            example_value,
            description,
        }
    }

    pub fn prefix_only(
        prefix: impl Into<String>,
        example_value: Option<String>,
        description: Option<String>,
    ) -> Self {
        Self {
            prefix: prefix.into(),
            aliases: vec![String::new()],
            suffix: String::new(),
            metadata: None,
            example_value,
            description,
        }
    }

    pub fn keyword(
        keyword: impl Into<String>,
        suffix: impl Into<String>,
        example_value: Option<String>,
        description: Option<String>,
    ) -> Self {
        Self {
            prefix: String::new(),
            aliases: vec![keyword.into()],
            suffix: suffix.into(),
            metadata: None,
            example_value,
            description,
        }
    }

    pub fn expand(
        &self,
        syntax_key: &str,
        value_kind: FilterValueKind,
        default_example: Option<&str>,
    ) -> Vec<FilterSyntaxMatch> {
        let example = self.example_value.as_deref().or(default_example);
        if self.aliases.is_empty() {
            vec![FilterSyntaxMatch::new(
                syntax_key,
                value_kind,
                &self.prefix,
                "",
                &self.suffix,
                self.metadata.clone(),
                example,
                self.description.as_deref(),
            )]
        } else {
            self.aliases
                .iter()
                .map(|alias| {
                    FilterSyntaxMatch::new(
                        syntax_key,
                        value_kind,
                        &self.prefix,
                        alias,
                        &self.suffix,
                        self.metadata.clone(),
                        example,
                        self.description.as_deref(),
                    )
                })
                .collect()
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, uniffi::Record)]
pub struct FilterSyntaxDefinition {
    pub key: String,
    pub value_kind: FilterValueKind,
    pub example_value: Option<String>,
    pub patterns: Vec<FilterSyntaxPattern>,
}

impl FilterSyntaxDefinition {
    pub fn new(
        key: impl Into<String>,
        value_kind: FilterValueKind,
        example_value: Option<String>,
        patterns: Vec<FilterSyntaxPattern>,
    ) -> Self {
        Self {
            key: key.into(),
            value_kind,
            example_value,
            patterns,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FilterSyntaxMatch {
    pub syntax_key: String,
    pub value_kind: FilterValueKind,
    pub prefix: String,
    pub alias: String,
    pub suffix: String,
    pub header_text: String,
    pub metadata: Option<String>,
    pub example_value: Option<String>,
    pub description: Option<String>,
    pub completion_text: String,
    pub completion_insert_text: String,
    pub diagnostic_text: String,
}

impl FilterSyntaxMatch {
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        syntax_key: &str,
        value_kind: FilterValueKind,
        prefix: &str,
        alias: &str,
        suffix: &str,
        metadata: Option<String>,
        example_value: Option<&str>,
        description: Option<&str>,
    ) -> Self {
        let header_text = format!("{prefix}{alias}{suffix}");
        let completion_insert_text = header_text.clone();
        let diagnostic_text = if !header_text.is_empty() {
            header_text.clone()
        } else {
            syntax_key.to_string()
        };
        let completion_text = if value_kind == FilterValueKind::None {
            header_text.clone()
        } else {
            format!("{header_text}{}", example_value.unwrap_or(""))
        };

        Self {
            syntax_key: syntax_key.to_string(),
            value_kind,
            prefix: prefix.to_string(),
            alias: alias.to_string(),
            suffix: suffix.to_string(),
            header_text,
            metadata,
            example_value: example_value.map(String::from),
            description: description.map(String::from),
            completion_text,
            completion_insert_text,
            diagnostic_text,
        }
    }
}
