pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}
plugins {
    id("org.gradle.toolchains.foojay-resolver-convention") version "1.0.0"
}
rootProject.name = "Pixeval"

include(":nativeBindings", ":sharedApp", ":androidApp", ":desktop")
project(":nativeBindings").projectDir = file("compose/nativeBindings")
project(":sharedApp").projectDir = file("compose/sharedApp")
project(":androidApp").projectDir = file("compose/androidApp")
project(":desktop").projectDir = file("compose/desktop")
