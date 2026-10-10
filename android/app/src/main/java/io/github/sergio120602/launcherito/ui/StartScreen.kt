package io.github.sergio120602.launcherito.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.DeleteOutline
import androidx.compose.material.icons.rounded.LibraryMusic
import androidx.compose.material.icons.rounded.PlayCircle
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.sergio120602.launcherito.R

/** Menú principal: canciones del móvil, las guardadas, borrarlas o una lista de Spotify. */
@Composable
fun StartScreen(vm: LauncheritoViewModel, onLoadFiles: () -> Unit) {
    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(horizontal = 24.dp, vertical = 32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Image(
            painterResource(R.drawable.logo),
            contentDescription = "Launcherito",
            modifier = Modifier.size(200.dp).clip(RoundedCornerShape(36.dp)),
        )
        Spacer(Modifier.height(28.dp))

        // Si hay algo cargado, se puede volver al reproductor.
        if (vm.hasSongs) {
            StartButton(
                text = if (vm.isPlaying) "Volver al reproductor  ·  ${vm.title}" else "Volver al reproductor",
                icon = Icons.Rounded.PlayCircle,
                background = SpotifyGreen.copy(alpha = 0.18f),
                content = SpotifyGreen,
                onClick = vm::showPlayer,
            )
            Spacer(Modifier.height(18.dp))
        }

        Button(
            onClick = onLoadFiles,
            shape = RoundedCornerShape(18.dp),
            colors = ButtonDefaults.buttonColors(containerColor = Accent, contentColor = Color.White),
            contentPadding = PaddingValues(horizontal = 40.dp, vertical = 18.dp),
            modifier = Modifier.widthIn(min = 260.dp),
        ) {
            Text("Cargar archivos", fontSize = 22.sp, fontWeight = FontWeight.SemiBold)
        }
        Spacer(Modifier.height(14.dp))
        StartButton(
            text = if (vm.libraryCount > 0) "Tus canciones  · ${vm.libraryCount}" else "Tus canciones",
            icon = Icons.Rounded.LibraryMusic,
            enabled = vm.libraryCount > 0,
            onClick = vm::loadLibrary,
        )
        Spacer(Modifier.height(10.dp))
        StartButton(
            text = "Borrar canciones",
            icon = Icons.Rounded.DeleteOutline,
            enabled = vm.libraryCount > 0,
            onClick = vm::openDeleteDialog,
        )
        Spacer(Modifier.height(10.dp))
        StartButton(
            text = "Lista de Spotify",
            icon = null,
            spotify = true,
            onClick = { vm.openSpotifyDialog() },
        )
        Spacer(Modifier.height(22.dp))
        TextSizeChooser(vm)
        if (vm.libraryCount == 0) {
            Spacer(Modifier.height(16.dp))
            Text(
                "Las canciones que cargues se guardan solas en «Tus canciones».",
                color = Muted, fontSize = 13.sp, textAlign = TextAlign.Center,
            )
        }
    }
}

/** «Letra: Pequeña · Grande». La elegida va en morado. */
@Composable
private fun TextSizeChooser(vm: LauncheritoViewModel) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text("Letra:", color = Muted, fontSize = 14.sp)
        Spacer(Modifier.width(8.dp))
        listOf(false to "Pequeña", true to "Grande").forEach { (large, label) ->
            val selected = vm.largeText == large
            Text(
                label,
                fontSize = 14.sp,
                fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal,
                color = if (selected) Color.White else Muted,
                maxLines = 1,
                softWrap = false,
                modifier = Modifier.padding(horizontal = 3.dp).clip(RoundedCornerShape(14.dp))
                    .background(if (selected) Accent.copy(alpha = 0.35f) else Pill)
                    .clickable { vm.chooseLargeText(large) }
                    .padding(horizontal = 14.dp, vertical = 7.dp),
            )
        }
    }
}

@Composable
private fun StartButton(
    text: String,
    icon: ImageVector?,
    onClick: () -> Unit,
    enabled: Boolean = true,
    spotify: Boolean = false,
    background: Color = Pill,
    content: Color = Color.White,
) {
    Button(
        onClick = onClick,
        enabled = enabled,
        shape = RoundedCornerShape(18.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = background,
            contentColor = content,
            disabledContainerColor = background.copy(alpha = 0.4f),
            disabledContentColor = content.copy(alpha = 0.4f),
        ),
        contentPadding = PaddingValues(horizontal = 28.dp, vertical = 14.dp),
        modifier = Modifier.widthIn(min = 260.dp, max = 420.dp),
    ) {
        if (spotify) SpotifyBadge(size = 22.dp)
        else if (icon != null) Icon(icon, contentDescription = null)
        Spacer(Modifier.size(10.dp))
        Text(text, fontSize = 17.sp, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}
