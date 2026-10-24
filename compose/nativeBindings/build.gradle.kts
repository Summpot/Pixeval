import ch.ubique.uniffi.plugin.tasks.BuildBindingsTask
import ch.ubique.uniffi.plugin.tasks.CargoBuildTask
import ch.ubique.uniffi.plugin.tasks.InstallBindgenTask
import ch.ubique.uniffi.plugin.tasks.MergeLibrariesTask

plugins {
    kotlin("multiplatform")
    id("com.android.kotlin.multiplatform.library")
    id("ch.ubique.uniffi.plugin")
}
val optimizedNative = providers.gradleProperty("nativeRelease")
    .orElse(providers.gradleProperty("releaseBuild"))
    .map(String::toBoolean)
    .getOrElse(gradle.startParameter.taskNames.any { it.contains("release", ignoreCase = true) }
        || System.getenv("CONFIGURATION").equals("Release", ignoreCase = true))
kotlin {
    jvmToolchain(21)
    jvm()
    android {
        namespace = "io.github.pixeval.bindings"
        compileSdk = 37
        minSdk = 23
        withHostTestBuilder {}.configure {}
    }
    if (System.getProperty("os.name").startsWith("Mac")) {
        iosArm64()
        iosSimulatorArm64()
    }
    sourceSets.commonTest.dependencies { implementation(kotlin("test")) }
    sourceSets.getByName("jvmTest").resources.srcDir(rootProject.layout.projectDirectory.dir("src/Pixeval.Tests/Fixtures/ProgressiveImages"))
}
cargo {
    packageDirectory.set(rootProject.layout.projectDirectory.dir("crates/pixeval_native"))
    targetDirectory.set(rootProject.layout.projectDirectory.dir("compose/build/cargo"))
    ndkVersion.set("28.2.13676358")
    androidDebugAbis.addAll("arm64-v8a", "x86_64")
}
uniffi {
    bindgenFromPath(rootProject.layout.projectDirectory.dir("compose/.tools/ubique/bindgen"))
    generateBindingsForExternalCrates.set(true)
    generateFromLibrary()
}
val externalBindgen = rootProject.file("../Pixeval_target/uniffi_bindgen/bin/uniffi-bindgen-kotlin-multiplatform.exe")
tasks.named<InstallBindgenTask>("installBindgen") {
    dependsOn(rootProject.tasks.named("prepareBindgen"))
    if (externalBindgen.exists()) {
        actions.clear()
        doLast {
            outputs.files.files.forEach {
                it.parentFile?.mkdirs()
                it.writeText("ready")
            }
        }
    }
}
tasks.named<BuildBindingsTask>("buildBindings") {
    val externalBindgen = rootProject.file("../Pixeval_target/uniffi_bindgen/bin/uniffi-bindgen-kotlin-multiplatform.exe")
    if (externalBindgen.exists()) {
        bindgen.set(externalBindgen)
    }
    doLast {
        // Upstream emits both a constructor property named `message` and an
        // additional Throwable.message getter. Preserve the field's wire name
        // and make it the override; never change Rust/C# error definitions.
        val collision = Regex("""(?s)(class\s+\w+\(\s*(?:(?!\n\s*class).)*?\bval `message`: kotlin.String(?:(?!\n\s*class).)*?\)\s*:\s*\w+\(\)\s*\{)\s*override val message\s*\n\s*get\(\) = "[^\n]*"""")
        fileTree(layout.buildDirectory.dir("uniffi/bindings")) { include("**/*.kt") }.forEach { file ->
            val original = file.readText()
            val fixed = collision.replace(original) { it.groupValues[1].replace("val `message`", "override val `message`") }
                .replace("@Throws(kotlin.String::class", "@Throws(UniffiStringException::class")
                .replace("kotlin.StringErrorHandler", "UniffiStringExceptionErrorHandler")
                .replace("fun `close`(", "fun `closeNative`(")
            if (original != fixed) file.writeText(fixed)
        }
    }
}
// The plugin's release JVM matrix includes MinGW. Pixeval uses the host's
// existing MSVC toolchain; package only the current host in this debug frontend.
tasks.withType<CargoBuildTask>().configureEach {
    additionalEnvironment.put("CARGO_PROFILE_DEV_DEBUG", "0")
    // Ubique 1.3.0 assumes NDK launchers are .exe files on Windows. NDK ships
    // target-specific .cmd launchers; keep the workaround local to this build.
}
afterEvaluate {
    tasks.withType<CargoBuildTask>().configureEach {
        // Ubique chooses one native variant before AGP creates app variants.
        // Override the task's profile (and its derived output providers), not
        // just its ABI list. Keep bindgen's plain host library independent.
        if (optimizedNative && rustTarget.isPresent) release.set(true)
        if (rustTarget.orNull?.rustTriple?.endsWith("android") == true) {
            val environment = additionalEnvironment.get().mapValues { (key, value) ->
                if (key.startsWith("CC_") || key.startsWith("CXX_") || key.endsWith("_LINKER")) {
                    val api23 = value.replace("android21-clang", "android23-clang")
                    if (System.getProperty("os.name").startsWith("Windows")) api23.removeSuffix(".exe") + ".cmd" else api23
                } else value
            }
            additionalEnvironment.set(environment)
            val tempDirectory = layout.buildDirectory.dir("tmp/native/$name").get().asFile
            val temp = tempDirectory.absolutePath
            listOf("TEMP", "TMP", "TMPDIR").forEach { additionalEnvironment.put(it, temp) }
            doFirst { tempDirectory.mkdirs() }
        }
    }
}
tasks.withType<MergeLibrariesTask>().configureEach {
    if (name == "mergeUniffiAndroidJniLibs") {
        libraries.set(libraries.get().filter { it.directoryName.get() in setOf("arm64-v8a", "x86_64") })
    }
}
