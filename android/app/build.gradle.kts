plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.plugin.compose")
}

// La versión sale del archivo VERSION de la raíz del repositorio, la misma que la del .exe.
val appVersion = rootDir.resolve("../VERSION").readText().trim()
// 1.9.1 → 10901: cada parte con dos cifras, para que Android sepa qué versión es más nueva.
val appVersionCode = appVersion.split('.').map { it.toInt() }.let { (it + listOf(0, 0, 0)).take(3) }
    .fold(0) { code, part -> code * 100 + part }

android {
    namespace = "io.github.sergio120602.launcherito"
    compileSdk = 37

    defaultConfig {
        applicationId = "io.github.sergio120602.launcherito"
        minSdk = 26
        targetSdk = 37
        versionCode = appVersionCode
        versionName = appVersion
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            // Firmada con la clave de depuración para poder instalarla directamente; para publicarla
            // en Google Play habría que crear una clave propia.
            signingConfig = signingConfigs.getByName("debug")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        compose = true
    }
}

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2026.09.00")
    implementation(composeBom)
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended:1.7.8")
    implementation("androidx.compose.ui:ui-tooling-preview")
    debugImplementation("androidx.compose.ui:ui-tooling")

    implementation("androidx.core:core-ktx:1.19.1")
    implementation("androidx.activity:activity-compose:1.13.0")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.11.0")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.11.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.11.0")

    // Reproductor de los .mp3, con notificación y controles en la pantalla de bloqueo.
    implementation("androidx.media3:media3-exoplayer:1.11.1")
    implementation("androidx.media3:media3-session:1.11.1")

    // Portadas y fotos de Deezer, y las peticiones a Deezer, Spotify y YouTube.
    implementation("io.coil-kt.coil3:coil-compose:3.6.3")
    implementation("io.coil-kt.coil3:coil-network-okhttp:3.6.3")
    implementation("com.squareup.okhttp3:okhttp:5.5.0")
}
