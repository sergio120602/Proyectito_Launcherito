package io.github.sergio120602.launcherito.ui

import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.graphics.Color

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

@Composable
fun LauncheritoTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = colors) {
        // Sin Surface ni Scaffold el color de iconos y textos sería negro: aquí es blanco en toda la app.
        CompositionLocalProvider(LocalContentColor provides Color.White, content = content)
    }
}
