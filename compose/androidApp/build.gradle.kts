plugins {
    id("com.android.application")
    kotlin("plugin.compose")
    id("org.jetbrains.compose")
}
android {
    namespace = "io.github.pixeval.pixeval"
    compileSdk = 37
    defaultConfig {
        applicationId = "io.github.pixeval.pixeval"
        minSdk = 23
        targetSdk = 36
        versionCode = 1
        versionName = "1.0.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        ndk { abiFilters += listOf("arm64-v8a", "x86_64") }
    }
    compileOptions { sourceCompatibility = JavaVersion.VERSION_21; targetCompatibility = JavaVersion.VERSION_21 }
    buildFeatures { compose = true }
    val releaseKeystore = providers.environmentVariable("PIXEVAL_ANDROID_KEYSTORE").orNull
    if (releaseKeystore != null) {
        val signing = signingConfigs.create("releaseCredentials") {
            storeFile = file(releaseKeystore)
            storePassword = providers.environmentVariable("ANDROID_KEYSTORE_PASSWORD").get()
            keyAlias = "pixeval"
            keyPassword = providers.environmentVariable("ANDROID_KEY_PASSWORD").get()
        }
        buildTypes.named("release") { signingConfig = signing }
    }
}
dependencies {
    implementation(project(":sharedApp"))
    implementation(compose.foundation)
    implementation(compose.material3)
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.11.0")
    implementation("androidx.activity:activity-compose:1.12.1")
    implementation("androidx.core:core-ktx:1.17.0")
    androidTestImplementation("androidx.test:runner:1.7.0")
    androidTestImplementation("androidx.test.ext:junit:1.3.0")
}
