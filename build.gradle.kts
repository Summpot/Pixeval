plugins {
    kotlin("multiplatform") version "2.4.21" apply false
    kotlin("plugin.compose") version "2.4.21" apply false
    id("org.jetbrains.compose") version "1.12.2" apply false
    id("com.android.application") version "9.3.1" apply false
    id("com.android.kotlin.multiplatform.library") version "9.3.1" apply false
    id("ch.ubique.uniffi.plugin") version "1.3.0" apply false
}

// The released generator is locked to 0.32.0. Build the same source against
// Pixeval's 0.32.2 rather than downgrading the shared Rust ABI for one frontend.
val prepareBindgen by tasks.registering {
    val revision = "20c1190053cb29c6c5e8bb07d366323baa2ed96b"
    val source = layout.projectDirectory.dir("compose/.tools/ubique")
    inputs.property("revision", revision)
    inputs.property("uniffi", "0.32.2")
    inputs.property("stringErrorPatch", 2)
    outputs.file(source.file(".pixeval-ready"))
    doLast {
        fun run(vararg args: String) {
            val process = ProcessBuilder(*args).directory(rootDir).inheritIO().start()
            check(process.waitFor() == 0) { "Failed: ${args.joinToString(" ")}" }
        }
        if (!source.dir(".git").asFile.exists()) {
            source.asFile.parentFile.mkdirs()
            run("git", "clone", "--no-checkout", "https://github.com/UbiqueInnovation/uniffi-kotlin-multiplatform-bindings.git", source.asFile.absolutePath)
        }
        run("git", "-C", source.asFile.absolutePath, "checkout", "--detach", revision)
        run("cargo", "update", "--manifest-path", source.file("Cargo.toml").asFile.absolutePath,
            "--package", "uniffi", "--precise", "0.32.2")
        // `cargo update` resolves a lockfile but need not unpack crate sources.
        // A fresh CI machine must have bindgen available before copying it.
        run("cargo", "fetch", "--locked", "--manifest-path", source.file("Cargo.toml").asFile.absolutePath)
        val cargoHome = System.getenv("CARGO_HOME")?.let(::File) ?: File(System.getProperty("user.home"), ".cargo")
        val upstream = File(cargoHome, "registry/src").listFiles().orEmpty()
            .map { File(it, "uniffi_bindgen-0.32.2") }.firstOrNull { it.isDirectory }
            ?: error("UniFFI 0.32.2 sources were not downloaded")
        val patched = source.dir("pixeval-uniffi-bindgen").asFile
        if (!patched.exists()) check(upstream.copyRecursively(patched))
        fun patch(file: File, before: String, after: String) {
            val contents = file.readText()
            if (!contents.contains(after)) {
                check(contents.contains(before)) { "Upstream patch no longer applies: $file" }
                file.writeText(contents.replace(before, after))
            }
        }
        // Rust permits String as LowerError, but the bindgen interface only
        // recognizes named enum/object errors. It needn't register a named error
        // for String; the Kotlin template supplies its own exception converter.
        patch(File(patched, "src/interface/mod.rs"),
            "None => None,\n        Some(Type::Enum",
            "None | Some(Type::String) => None,\n        Some(Type::Enum")
        val manifest = source.file("Cargo.toml").asFile
        if (!manifest.readText().contains("[patch.crates-io]")) manifest.appendText(
            "\n[patch.crates-io]\nuniffi_bindgen = { path = \"pixeval-uniffi-bindgen\" }\n")
        val code = source.file("bindgen/src/gen_kotlin_multiplatform/mod.rs").asFile
        code.writeText(code.readText().replace("if label == \"String\" {", "if label == \"String\" || label == \"kotlin.String\" {"))
        val anchor = "    // Workaround problem with impl AsCodeType for &Variant (see variant.rs)."
        patch(code, anchor, """
    #[askama::filter_fn]
    pub(super) fn error_type_name(as_ct: &impl AsCodeType, _: &dyn askama::Values, ci: &ComponentInterface) -> Result<String, askama::Error> {
        let label = as_ct.as_codetype().type_label(ci);
        Ok(if label == "String" || label == "kotlin.String" { "UniffiStringException".into() } else { label })
    }

$anchor
""".trimIndent())
        val templates = source.dir("bindgen/src/templates").asFile
        listOf("macros.kt", "generic/common/Interface.kt").forEach { path ->
            val file = File(templates, path)
            file.writeText(file.readText()
                .replace("throwable|type_name(ci)", "throwable|error_type_name(ci)")
                .replace("e|type_name(ci) }}ErrorHandler", "e|error_type_name(ci) }}ErrorHandler"))
        }
        val types = File(templates, "generic/common/Types.kt")
        if (!types.readText().contains("class UniffiStringException")) types.appendText(
            "\nclass UniffiStringException(message: String) : Exception(message)\n")
        listOf("android+jvm", "native").forEach { platform ->
            val wrapper = File(templates, "generic/$platform/wrapper.kt")
            if (!wrapper.readText().contains("object UniffiStringExceptionErrorHandler")) wrapper.appendText("""

object UniffiStringExceptionErrorHandler : UniffiRustCallStatusErrorHandler<UniffiStringException> {
    override fun lift(errorBuf: RustBufferByValue): UniffiStringException =
        UniffiStringException(FfiConverterString.liftFromRustBuffer(errorBuf))
}
""")
        }
        run("cargo", "update", "--manifest-path", manifest.absolutePath, "--package", "uniffi_bindgen")
        source.file(".pixeval-ready").asFile.writeText("$revision\nuniffi=0.32.2\n")
    }
}
