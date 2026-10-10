package io.github.sergio120602.launcherito.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Pause
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.Shuffle
import androidx.compose.material.icons.rounded.SkipNext
import androidx.compose.material.icons.rounded.SkipPrevious
import androidx.compose.material3.FilledIconButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.IconButtonDefaults
import androidx.compose.material3.Slider
import androidx.compose.material3.SliderDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * La barra, los tiempos y los botones. Hay un solo juego de controles en cada vista: dentro de la
 * portada que suena (mosaico), debajo de la carátula (vista original) o abajo (artistas y géneros).
 */
@Composable
fun PlayerControls(vm: LauncheritoViewModel, modifier: Modifier = Modifier) {
    val shown = if (vm.isSeeking) vm.seekValue else vm.position
    Column(modifier) {
        Slider(
            value = shown.coerceIn(0f, maxOf(1f, vm.duration)),
            onValueChange = vm::seekDrag,
            onValueChangeFinished = vm::seekEnd,
            valueRange = 0f..maxOf(1f, vm.duration),
            colors = SliderDefaults.colors(
                thumbColor = Color.White,
                activeTrackColor = Accent,
                inactiveTrackColor = TrackColor,
            ),
            modifier = Modifier.fillMaxWidth().height(32.dp),
        )
        Row(Modifier.fillMaxWidth()) {
            Text(formatTime(shown), color = Muted, fontSize = 12.sp)
            Spacer(Modifier.weight(1f))
            Text(formatTime(vm.duration), color = Muted, fontSize = 12.sp)
        }
        Row(
            Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.Center,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = vm::toggleShuffle) {
                Icon(
                    Icons.Rounded.Shuffle,
                    contentDescription = if (vm.shuffle) "Aleatorio: activado" else "Aleatorio: desactivado (orden alfabético)",
                    tint = if (vm.shuffle) Accent else Muted,
                )
            }
            Spacer(Modifier.width(4.dp))
            IconButton(onClick = vm::previous) {
                Icon(Icons.Rounded.SkipPrevious, contentDescription = "Anterior", modifier = Modifier.size(30.dp))
            }
            FilledIconButton(
                onClick = vm::togglePlay,
                modifier = Modifier.size(56.dp),
                colors = IconButtonDefaults.filledIconButtonColors(containerColor = Accent, contentColor = Color.White),
            ) {
                Icon(
                    if (vm.isPlaying) Icons.Rounded.Pause else Icons.Rounded.PlayArrow,
                    contentDescription = if (vm.isPlaying) "Pausar" else "Reproducir",
                    modifier = Modifier.size(32.dp),
                )
            }
            IconButton(onClick = vm::next) {
                Icon(Icons.Rounded.SkipNext, contentDescription = "Siguiente", modifier = Modifier.size(30.dp))
            }
            Spacer(Modifier.width(4.dp))
            // Posición en la lista; ancho fijo para que los botones no se muevan al cambiar.
            Box(Modifier.width(56.dp), contentAlignment = Alignment.CenterStart) {
                Text(vm.positionText, color = Muted, fontSize = 12.sp, maxLines = 1)
            }
        }
    }
}

fun formatTime(seconds: Float): String {
    val total = seconds.toInt().coerceAtLeast(0)
    val h = total / 3600
    val m = total % 3600 / 60
    val s = total % 60
    return if (h > 0) "%d:%02d:%02d".format(h, m, s) else "%d:%02d".format(m, s)
}
