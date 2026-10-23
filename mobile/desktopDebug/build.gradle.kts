plugins {
    kotlin("multiplatform")
    kotlin("plugin.compose")
    id("org.jetbrains.compose")
}
kotlin {
    jvmToolchain(21)
    jvm()
    sourceSets.jvmMain.dependencies {
        implementation(project(":sharedApp"))
        implementation(compose.desktop.currentOs)
        implementation(compose.material3)
        implementation("org.jetbrains.kotlinx:kotlinx-coroutines-swing:1.11.0")
    }
}
compose.desktop { application { mainClass = "io.github.pixeval.host.MainKt" } }
