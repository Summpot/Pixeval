use fluent_syntax::{ast, parser};
use std::{collections::BTreeMap, fmt::Write, path::Path};

fn quote(value: &str) -> String {
    format!(
        "\"{}\"",
        value
            .chars()
            .map(|ch| match ch {
                '\\' => "\\\\".into(),
                '"' => "\\\"".into(),
                '$' => "\\$".into(),
                '\n' => "\\n".into(),
                '\r' => "\\r".into(),
                '\t' => "\\t".into(),
                ch => ch.to_string(),
            })
            .collect::<String>()
    )
}

fn pattern(value: ast::Pattern<&str>) -> Result<String, String> {
    let mut result = String::new();
    for element in value.elements {
        match element {
            ast::PatternElement::TextElement { value } => {
                write!(result, "append({});", quote(value)).unwrap()
            }
            ast::PatternElement::Placeable {
                expression: ast::Expression::Inline(inline),
            } => match inline {
                ast::InlineExpression::VariableReference { id } => {
                    write!(
                        result,
                        "append(args[{}] ?: error({}));",
                        quote(id.name),
                        quote(&format!("Missing message argument {}", id.name))
                    )
                    .unwrap();
                }
                ast::InlineExpression::StringLiteral { value } => {
                    let value = fluent_syntax::unicode::unescape_unicode_to_string(value);
                    write!(result, "append({});", quote(&value)).unwrap();
                }
                _ => {
                    return Err(
                        "Unsupported Fluent expression; extend the compiler explicitly".into(),
                    );
                }
            },
            _ => return Err("Unsupported Fluent selector; extend the compiler explicitly".into()),
        }
    }
    Ok(result)
}

fn compile(root: &Path, output: &Path) -> Result<(), String> {
    let mut locales: BTreeMap<String, BTreeMap<String, String>> = BTreeMap::new();
    fn visit(
        dir: &Path,
        root: &Path,
        locales: &mut BTreeMap<String, BTreeMap<String, String>>,
    ) -> Result<(), String> {
        for item in std::fs::read_dir(dir).map_err(|e| e.to_string())? {
            let path = item.map_err(|e| e.to_string())?.path();
            if path.is_dir() {
                visit(&path, root, locales)?;
                continue;
            }
            if path.extension().is_none_or(|ext| ext != "ftl") {
                continue;
            }
            let locale = path.file_stem().unwrap().to_str().unwrap().to_string();
            let prefix = path
                .parent()
                .unwrap()
                .strip_prefix(root)
                .unwrap()
                .components()
                .map(|c| c.as_os_str().to_str().unwrap())
                .collect::<Vec<_>>()
                .join(".");
            let source = std::fs::read_to_string(&path).map_err(|e| e.to_string())?;
            let resource = parser::parse(source.as_str())
                .map_err(|(_, errors)| format!("{}: {errors:?}", path.display()))?;
            for entry in resource.body {
                if let ast::Entry::Message(message) = entry {
                    if let Some(value) = message.value {
                        let key = format!("{prefix}.{}", message.id.name);
                        let body =
                            pattern(value).map_err(|e| format!("{} {key}: {e}", path.display()))?;
                        if locales
                            .entry(locale.clone())
                            .or_default()
                            .insert(key.clone(), body)
                            .is_some()
                        {
                            return Err(format!("Duplicate message {locale}/{key}"));
                        }
                    }
                }
            }
        }
        Ok(())
    }
    visit(root, root, &mut locales)?;
    let mut kotlin = String::from(
        "// Generated from Pixeval Fluent resources. Do not edit.\npackage io.github.pixeval.mobile.localization\n\nobject Messages {\n",
    );
    writeln!(
        kotlin,
        "val locales = listOf({})",
        locales
            .keys()
            .map(|v| quote(v))
            .collect::<Vec<_>>()
            .join(",")
    )
    .unwrap();
    kotlin.push_str("fun text(key: String, locale: String, args: Map<String, Any> = emptyMap()): String {\nval selected = when {\n");
    for locale in locales.keys() {
        writeln!(
            kotlin,
            "locale.equals({}, true) -> {}",
            quote(locale),
            quote(locale)
        )
        .unwrap();
    }
    for locale in locales.keys() {
        let language = locale.split('-').next().unwrap();
        if language != "zh" {
            writeln!(
                kotlin,
                "locale.substringBefore('-').equals({}, true) -> {}",
                quote(language),
                quote(locale)
            )
            .unwrap();
        }
    }
    kotlin.push_str("else -> \"zh-Hans\"\n}\nreturn resolve(selected, key, args) ?: resolve(\"zh-Hans\", key, args) ?: key\n}\nprivate fun resolve(locale: String, key: String, args: Map<String, Any>): String? = when(locale) {\n");
    for locale in locales.keys() {
        writeln!(
            kotlin,
            "{} -> {}(key, args)",
            quote(locale),
            locale.replace('-', "_")
        )
        .unwrap();
    }
    kotlin.push_str("else -> null\n}\n");
    for (locale, messages) in locales {
        writeln!(
            kotlin,
            "private fun {}(key: String, args: Map<String, Any>): String? = when(key) {{",
            locale.replace('-', "_")
        )
        .unwrap();
        for (key, body) in messages {
            writeln!(kotlin, "{} -> buildString {{ {body} }}", quote(&key)).unwrap();
        }
        kotlin.push_str("else -> null\n}\n");
    }
    kotlin.push_str("}\n");
    std::fs::create_dir_all(output.parent().unwrap()).map_err(|e| e.to_string())?;
    if std::fs::read_to_string(output).ok().as_ref() != Some(&kotlin) {
        std::fs::write(output, kotlin).map_err(|e| e.to_string())?;
    }
    Ok(())
}

fn main() {
    let args: Vec<_> = std::env::args().collect();
    if args.len() != 3 {
        eprintln!("Usage: pixeval_mobile_localization <messages directory> <output.kt>");
        std::process::exit(2);
    }
    if let Err(error) = compile(Path::new(&args[1]), Path::new(&args[2])) {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn kotlin_literals_do_not_interpolate() {
        assert_eq!(quote("$a\n\""), "\"\\$a\\n\\\"\"");
    }
    #[test]
    fn compiles_variables_and_escaped_literals() {
        let source = "Title = Hello { $name }{ \"\\u000A\" }\n";
        let resource = parser::parse(source).unwrap();
        let ast::Entry::Message(message) = resource.body.into_iter().next().unwrap() else {
            panic!()
        };
        let result = pattern(message.value.unwrap()).unwrap();
        assert!(result.contains("args[\"name\"]"));
        assert!(result.contains("append(\"\\n\")"));
    }
}
