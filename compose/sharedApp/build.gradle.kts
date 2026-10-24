plugins {
    kotlin("multiplatform")
    kotlin("plugin.compose")
    id("org.jetbrains.compose")
    id("com.android.kotlin.multiplatform.library")
}
val generateLocalization = tasks.register<Exec>("generateLocalization") {
    val messages = rootProject.layout.projectDirectory.dir("src/Pixeval/messages")
    val compiler = rootProject.layout.projectDirectory.dir("compose/tooling/localization")
    val output = layout.buildDirectory.file("generated/localization/Messages.kt")
    inputs.dir(messages)
    inputs.dir(compiler.dir("src"))
    inputs.file(compiler.file("Cargo.toml"))
    outputs.file(output)
    workingDir(rootProject.projectDir)
    commandLine("cargo", "run", "--manifest-path", compiler.file("Cargo.toml").asFile.absolutePath,
        "--", messages.asFile.absolutePath, output.get().asFile.absolutePath)
}
kotlin {
    jvmToolchain(21)
    jvm()
    android { namespace = "io.github.pixeval.shared"; compileSdk = 37; minSdk = 23 }
    if (System.getProperty("os.name").startsWith("Mac")) {
        listOf(iosArm64(), iosSimulatorArm64()).forEach {
            it.binaries.framework { baseName = "PixevalMobile"; isStatic = true }
        }
    }
    sourceSets {
        commonMain { kotlin.srcDir(generateLocalization.map { layout.buildDirectory.dir("generated/localization") }) }
        commonMain.dependencies {
            api(project(":nativeBindings"))
            implementation(compose.runtime)
            implementation(compose.foundation)
            implementation(compose.material3)
            implementation(compose.components.resources)
            implementation("org.jetbrains.kotlinx:kotlinx-coroutines-core:1.11.0")
        }
        commonTest.dependencies {
            implementation(kotlin("test"))
            implementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:1.11.0")
        }
        jvmMain.dependencies { implementation(compose.desktop.currentOs) }
    }
}
