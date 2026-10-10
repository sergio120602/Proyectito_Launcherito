package io.github.sergio120602.launcherito.ui

import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Density

// Los mismos colores que la versión de Windows.
val Accent = Color(0xFF8B5CF6)
val AccentHover = Color(0xFFA78BFA)
val Muted = Color(0xFF9A9A9A)
val TrackColor = Color(0xFF2A2A2A)
val Pill = Color(0xFF1C1C1C)
val EmptyFill = Color(0xFF151515)
val PlaceholderColor = Color(0xFF3A3A3A)
val DangerRed = Color(0xFFEF4444)
val SpotifyGreen = Color(0xFF1DB954)
val YouTubeRed = Color(0xFFFF0033)
val StopEditGreen = Color(0xFF16A34A)

private val colors = darkColorScheme(
    primary = Accent,
    onPrimary = Color.White,
    secondary = AccentHover,
    background = Color.Black,
    onBackground = Color.White,
    surface = Color(0xFF121212),
    onSurface = Color.White,
    surfaceVariant = Pill,
    onSurfaceVariant = Muted,
    surfaceContainerHigh = Color(0xFF1A1A1A),
    surfaceContainerHighest = Pill,
    error = DangerRed,
)

// Letra pequeña: como mucho el tamaño normal, aunque el móvil la tenga más grande (diseño compacto).
private const val SMALL_MAX_SCALE = 1f

// Letra grande: la del móvil, pero al menos 1,3× (para que se note aunque el móvil la tenga normal) y
// como mucho 1,5× (más allá no caben los controles en el ancho de un móvil).
private const val LARGE_MIN_SCALE = 1.3f
private const val LARGE_MAX_SCALE = 1.5f

/** true con la letra grande: la interfaz pone los botones en su propia fila, menos portadas por fila… */
val LocalLargeText = staticCompositionLocalOf { false }

@Composable
fun LauncheritoTheme(largeText: Boolean, content: @Composable () -> Unit) {
    val density = LocalDensity.current
    val fontScale = if (largeText) density.fontScale.coerceIn(LARGE_MIN_SCALE, LARGE_MAX_SCALE)
    else density.fontScale.coerceAtMost(SMALL_MAX_SCALE)
    MaterialTheme(colorScheme = colors) {
        CompositionLocalProvider(
            LocalDensity provides Density(density.density, fontScale),
            LocalLargeText provides largeText,
            // Sin Surface ni Scaffold el color de iconos y textos sería negro: aquí es blanco en toda la app.
            LocalContentColor provides Color.White,
            content = content,
        )
    }
}
