plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

// Push notifications need app/google-services.json from the Firebase console. That file is a private setting and is never
// committed, so builds without it (CI, a fresh checkout, demo mode) still work: push is simply switched off.
if (file("google-services.json").exists()) apply(plugin = "com.google.gms.google-services")

android {
    namespace = "com.rhythmandflow.app"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.rhythmandflow.app"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "1.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        // Run the whole app on built-in sample data (no server):  gradlew assembleDebug -PdemoMode=true.  Always off otherwise.
        buildConfigField("boolean", "DEMO_MODE", (project.findProperty("demoMode") == "true").toString())
        // The video player blocks screenshots and recording. Only for making a backup screen recording:  gradlew assembleDebug -PallowCapture=true
        buildConfigField("boolean", "ALLOW_SCREEN_CAPTURE", (project.findProperty("allowCapture") == "true").toString())
    }

    buildTypes {
        // Point the app at any API with:  gradlew assembleDebug -PapiBaseUrl=https://your-app.azurewebsites.net/
        // Without the flag, debug builds use 10.0.2.2, the emulator's alias for the PC running the API.
        debug {
            val base = (project.findProperty("apiBaseUrl") as String?) ?: "http://10.0.2.2:5080/"
            buildConfigField("String", "API_BASE_URL", "\"$base\"")
            // The "simulate payment" shortcut only exists for a local API (the hosted API does not offer it).
            buildConfigField("boolean", "SIMULATE_PAYMENT", base.contains("10.0.2.2").toString())
        }
        release {
            isMinifyEnabled = false
            val base = (project.findProperty("apiBaseUrl") as String?) ?: "https://api.example.com/"
            buildConfigField("String", "API_BASE_URL", "\"$base\"")
            buildConfigField("boolean", "SIMULATE_PAYMENT", "false")
        }
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
}

dependencies {
    implementation(platform("androidx.compose:compose-bom:2025.06.01"))
    implementation("androidx.core:core-ktx:1.16.0")
    implementation("androidx.activity:activity-compose:1.10.1")
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")
    implementation("androidx.navigation:navigation-compose:2.9.0")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.9.1")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.9.1")

    // Networking
    implementation("com.squareup.retrofit2:retrofit:2.11.0")
    implementation("com.squareup.retrofit2:converter-gson:2.11.0")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("com.squareup.okhttp3:logging-interceptor:4.12.0")

    // Secure token storage
    implementation("androidx.security:security-crypto:1.1.0-alpha06")

    // Background work (notification sync, class reminders)
    implementation("androidx.work:work-runtime-ktx:2.10.2")

    // Push notifications (Firebase Cloud Messaging)
    implementation(platform("com.google.firebase:firebase-bom:33.7.0"))
    implementation("com.google.firebase:firebase-messaging")

    // Video playback
    implementation("androidx.media3:media3-exoplayer:1.7.1")
    implementation("androidx.media3:media3-ui:1.7.1")

    debugImplementation("androidx.compose.ui:ui-tooling")

    testImplementation("junit:junit:4.13.2")
}

