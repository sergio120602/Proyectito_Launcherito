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
import androidx.compose.material.icons.rounded.Favorite
import androidx.compose.material.icons.rounded.Search
import androidx.compose.material.icons.automirrored.rounded.TrendingUp
import androidx.compose.material.icons.rounded.LibraryMusic
import androidx.compose.material.icons.rounded.PlayCircle
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.text.input.ImeAction
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
import io.github.sergio120602.launcherito.data.OnlineSource

/** Menú principal: canciones del móvil, las guardadas, borrarlas o una lista de Spotify o YouTube Music. */
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

        // Barra de búsqueda de YouTube: Buscar abre el panel con los resultados.
        var query by remember { mutableStateOf("") }
        OutlinedTextField(
            value = query,
            onValueChange = { query = it },
            singleLine = true,
            shape = RoundedCornerShape(22.dp),
            leadingIcon = { Icon(Icons.Rounded.Search, contentDescription = null) },
            placeholder = { Text("Buscar canciones en YouTube…", maxLines = 1) },
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
            keyboardActions = KeyboardActions(onSearch = {
                vm.openYouTube(query)
                query = ""
            }),
            colors = OutlinedTextFieldDefaults.colors(unfocusedContainerColor = Pill, focusedContainerColor = Pill),
            modifier = Modifier.widthIn(min = 260.dp, max = 420.dp),
        )
        Spacer(Modifier.height(18.dp))

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
            text = "Tus playlists",
            icon = Icons.Rounded.Favorite,
            content = FavoritePink,
            onClick = { vm.showSectionFromStart(Section.Playlists) },
        )
        Spacer(Modifier.height(10.dp))
        StartButton(
            text = "Del momento",
            icon = Icons.AutoMirrored.Rounded.TrendingUp,
            content = SpotifyGreen,
            onClick = { vm.showSectionFromStart(Section.Trending) },
        )
        Spacer(Modifier.height(10.dp))
        StartButton(
            text = "Lista de Spotify",
            icon = null,
            source = OnlineSource.Spotify,
            onClick = { vm.openListDialog(OnlineSource.Spotify) },
        )
        Spacer(Modifier.height(10.dp))
        StartButton(
            text = "Lista de YouTube Music",
            icon = null,
            source = OnlineSource.YouTubeMusic,
            onClick = { vm.openListDialog(OnlineSource.YouTubeMusic) },
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
        Spacer(Modifier.height(16.dp))
        UpdateSection(vm)   // versión y botón de actualizar (Updates.kt)
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
    source: OnlineSource? = null,   // si es el botón de una lista, lleva la marca de su servicio
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
        when {
            source == OnlineSource.Spotify -> SpotifyBadge(size = 22.dp)
            source == OnlineSource.YouTubeMusic -> YouTubeMusicBadge(size = 22.dp)
            icon != null -> Icon(icon, contentDescription = null)
        }
        Spacer(Modifier.size(10.dp))
        Text(text, fontSize = 17.sp, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}
