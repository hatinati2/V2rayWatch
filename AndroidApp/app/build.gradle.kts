import java.security.MessageDigest
import java.net.URL

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

val xrayVersion = "v26.10.29"
val xrayAarUrl = "https://github.com/2dust/AndroidLibXrayLite/releases/download/$xrayVersion/libv2ray.aar"
val xrayAarSha256 = "64fc529302dce728bde859f45d87a64f7289a5fbcca8ef962aae3d94c6e3fa82"
val xrayAar = layout.projectDirectory.file("libs/libv2ray.aar").asFile

val androidSigningStoreFile = providers.environmentVariable("ANDROID_SIGNING_STORE_FILE").orNull
val androidSigningStorePassword = providers.environmentVariable("ANDROID_SIGNING_STORE_PASSWORD").orNull
val androidSigningKeyAlias = providers.environmentVariable("ANDROID_SIGNING_KEY_ALIAS").orNull
val androidSigningKeyPassword = providers.environmentVariable("ANDROID_SIGNING_KEY_PASSWORD").orNull
val hasAndroidReleaseSigning = listOf(
    androidSigningStoreFile,
    androidSigningStorePassword,
    androidSigningKeyAlias,
    androidSigningKeyPassword
).all { !it.isNullOrBlank() }


val downloadXrayCore by tasks.registering {
    group = "setup"
    description = "Downloads the pinned Android Xray Core AAR and verifies its SHA-256."
    outputs.file(xrayAar)
    outputs.upToDateWhen { false }
    doLast {
        if (xrayAar.exists()) {
            val digest = MessageDigest.getInstance("SHA-256")
                .digest(xrayAar.readBytes())
                .joinToString("") { "%02x".format(it) }
            if (digest == xrayAarSha256) {
                logger.lifecycle("Verified existing Xray AAR ($xrayVersion)")
                return@doLast
            }
            xrayAar.delete()
        }
        xrayAar.parentFile.mkdirs()
        val temporary = File(xrayAar.parentFile, "libv2ray.aar.download")
        logger.lifecycle("Downloading Android Xray Core $xrayVersion (~60 MB)...")
        URL(xrayAarUrl).openStream().use { input -> temporary.outputStream().use { output -> input.copyTo(output) } }
        val digest = MessageDigest.getInstance("SHA-256")
            .digest(temporary.readBytes())
            .joinToString("") { "%02x".format(it) }
        if (digest != xrayAarSha256) {
            temporary.delete()
            throw GradleException("Xray AAR checksum mismatch. Expected $xrayAarSha256, got $digest")
        }
        if (!temporary.renameTo(xrayAar)) {
            temporary.copyTo(xrayAar, overwrite = true)
            temporary.delete()
        }
        logger.lifecycle("Xray AAR downloaded and SHA-256 verified.")
    }
}

tasks.matching { it.name == "preBuild" }.configureEach {
    dependsOn(downloadXrayCore)
}

android {
    signingConfigs {
        create("release") {
            if (hasAndroidReleaseSigning) {
                storeFile = file(requireNotNull(androidSigningStoreFile))
                storePassword = requireNotNull(androidSigningStorePassword)
                keyAlias = requireNotNull(androidSigningKeyAlias)
                keyPassword = requireNotNull(androidSigningKeyPassword)
            }
        }
    }

    namespace = "com.linkwatch.app"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.linkwatch.app"
        minSdk = 26
        targetSdk = 35
        versionCode = 4
        versionName = "0.2.2"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            if (hasAndroidReleaseSigning) {
                signingConfig = signingConfigs.getByName("release")
            }
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    packaging {
        jniLibs {
            useLegacyPackaging = true
        }
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
        }
    }
}

val verifyAndroidReleaseSigning by tasks.registering {
    group = "verification"
    description = "Fails release packaging unless the persistent Android upload keystore is configured."

    doLast {
        val missing = listOf(
            "ANDROID_SIGNING_STORE_FILE" to androidSigningStoreFile,
            "ANDROID_SIGNING_STORE_PASSWORD" to androidSigningStorePassword,
            "ANDROID_SIGNING_KEY_ALIAS" to androidSigningKeyAlias,
            "ANDROID_SIGNING_KEY_PASSWORD" to androidSigningKeyPassword
        ).filter { it.second.isNullOrBlank() }.map { it.first }

        if (missing.isNotEmpty()) {
            throw GradleException(
                "Release signing is not configured. Set these environment variables: ${missing.joinToString()}. " +
                    "Never commit the keystore or passwords to source control."
            )
        }
        val keystore = file(requireNotNull(androidSigningStoreFile))
        if (!keystore.isFile) {
            throw GradleException("Android upload keystore file was not found at the configured path.")
        }
    }
}

tasks.configureEach {
    if ((name.startsWith("package") || name.startsWith("bundle")) && name.contains("Release")) {
        dependsOn(verifyAndroidReleaseSigning)
    }
}

dependencies {
    implementation(files("libs/libv2ray.aar"))

    implementation(platform("androidx.compose:compose-bom:2025.03.00"))
    implementation("androidx.activity:activity-compose:1.10.1")
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
    implementation("androidx.lifecycle:lifecycle-viewmodel-ktx:2.8.7")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.7")
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")

    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.10.1")
    implementation("com.google.code.gson:gson:2.12.1")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("com.journeyapps:zxing-android-embedded:4.3.0")

    debugImplementation("androidx.compose.ui:ui-tooling")
    testImplementation("junit:junit:4.13.2")
}
