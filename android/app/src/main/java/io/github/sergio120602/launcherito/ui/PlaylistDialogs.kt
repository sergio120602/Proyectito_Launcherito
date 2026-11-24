package io.github.sergio120602.launcherito.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.FormatListBulleted
import androidx.compose.material.icons.rounded.Add
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material.icons.rounded.Favorite
import androidx.compose.material.icons.rounded.FavoriteBorder
import androidx.compose.material.icons.rounded.MusicNote
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.Search
import androidx.compose.material.icons.rounded.SmartDisplay
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CheckboxDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import coil3.compose.AsyncImage
import io.github.sergio120602.launcherito.data.OnlineSongs
import io.github.sergio120602.launcherito.data.OnlineSource
import io.github.sergio120602.launcherito.data.OnlineTrack
import io.github.sergio120602.launcherito.data.SongCover
import io.github.sergio120602.launcherito.data.UserPlaylists
import io.github.sergio120602.launcherito.data.YouTubeMusicLibrary

/** Los cuadros de Favoritos, playlists y YouTube (se dibujan encima de todo, con los demás). */
@Composable
fun PlaylistDialogs(vm: LauncheritoViewModel) {
    vm.songMenuKey?.let { SongMenuDialog(vm, it) }
    if (vm.pickListId != null) PickDialog(vm)
    vm.nameDialog?.let { NameDialog(vm, it) }
    vm.confirmDeleteList?.let { id ->
        val list = vm.userPlaylist(id)
        AlertDialog(
            onDismissRequest = { vm.confirmDeleteList = null },
            title = { Text("Borrar playlist") },
            text = { Text("¿Borrar la playlist «${list?.name}»?\nSus canciones siguen en «Tus canciones».") },
            confirmButton = {
                Button(onClick = { vm.deleteList(id) }, colors = ButtonDefaults.buttonColors(containerColor = DangerRed)) { Text("Borrar") }
            },
            dismissButton = { TextButton(onClick = { vm.confirmDeleteList = null }) { Text("Cancelar") } },
        )
    }
    if (vm.youTubeOpen) YouTubeDialog(vm)
}

/** Carátula pequeña de una canción (o una nota mientras no la hay). */
@Composable
fun SmallCover(model: Any?, size: androidx.compose.ui.unit.Dp = 44.dp) {
    Box(Modifier.size(size).clip(RoundedCornerShape(6.dp)).background(EmptyFill), contentAlignment = Alignment.Center) {
        Icon(Icons.Rounded.MusicNote, contentDescription = null, tint = PlaceholderColor, modifier = Modifier.size(size * 0.4f))
        if (model != null) AsyncImage(model, contentDescription = null, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
    }
}

/**
 * Menú de una canción (al mantenerla pulsada o con el botón de añadir de los controles): Favoritos,
 * cada playlist (marcada si ya la tiene; al pulsarla se pone o se quita) y «Nueva playlist…».
 */
@Composable
private fun SongMenuDialog(vm: LauncheritoViewModel, key: String) {
    val (title, artist) = vm.describeSong(key)
    val favorite = vm.isFavorite(key)
    val lists = vm.userPlaylists().filter { !it.isFavorites }
    Dialog(onDismissRequest = { vm.songMenuKey = null }) {
        Surface(shape = RoundedCornerShape(20.dp), color = Color(0xFF161616)) {
            Column(Modifier.padding(vertical = 14.dp).heightIn(max = 560.dp)) {
                Row(Modifier.padding(horizontal = 18.dp, vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                    SmallCover(SongCover(key))
                    Spacer(Modifier.width(12.dp))
                    Column(Modifier.weight(1f)) {
                        Text(title, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
                        if (artist.isNotEmpty()) Text(artist, color = Muted, fontSize = 13.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    }
                }
                HorizontalDivider(Modifier.padding(vertical = 8.dp), color = Color(0xFF2A2A2A))
                Column(Modifier.verticalScroll(rememberScrollState())) {
                    MenuRow(
                        if (favorite) Icons.Rounded.Favorite else Icons.Rounded.FavoriteBorder, FavoritePink,
                        if (favorite) "Quitar de Favoritos" else "Añadir a Favoritos",
                    ) {
                        vm.songMenuKey = null
                        vm.toggleFavorite(key)
                    }
                    if (lists.isNotEmpty()) {
                        Text(
                            "Tus playlists", color = Muted, fontSize = 12.sp,
                            modifier = Modifier.padding(start = 20.dp, top = 10.dp, bottom = 2.dp),
                        )
                    }
                    for (list in lists) {
                        val inList = key in list
                        MenuRow(
                            Icons.AutoMirrored.Rounded.FormatListBulleted, Muted, list.name,
                            trailing = if (inList) Icons.Rounded.Check else null,
                        ) { vm.setInList(list.id, key, add = !inList) }
                    }
                    MenuRow(Icons.Rounded.Add, Accent, "Nueva playlist…") { vm.openNameDialog(songKey = key) }
                }
                TextButton(onClick = { vm.songMenuKey = null }, modifier = Modifier.align(Alignment.End).padding(end = 8.dp)) {
                    Text("Cerrar")
                }
            }
        }
    }
}

@Composable
private fun MenuRow(icon: ImageVector, tint: Color, text: String, trailing: ImageVector? = null, onClick: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().clickable(onClick = onClick).padding(horizontal = 20.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(icon, contentDescription = null, tint = tint, modifier = Modifier.size(20.dp))
        Spacer(Modifier.width(14.dp))
        Text(text, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f))
        if (trailing != null) Icon(trailing, contentDescription = "Ya está", tint = Accent, modifier = Modifier.size(20.dp))
    }
}

/** Nombre de una playlist nueva (o el nuevo nombre de una que ya existe). */
@Composable
private fun NameDialog(vm: LauncheritoViewModel, dialog: LauncheritoViewModel.NameDialog) {
    val renaming = dialog.renamingId != null
    AlertDialog(
        onDismissRequest = vm::closeNameDialog,
        title = { Text(if (renaming) "Renombrar playlist" else "Nueva playlist") },
        text = {
            Column {
                OutlinedTextField(
                    value = vm.nameText,
                    onValueChange = { if (it.length <= UserPlaylists.MAX_NAME_LENGTH) vm.nameText = it },
                    singleLine = true,
                    placeholder = { Text("Nombre de la playlist") },
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
                    keyboardActions = KeyboardActions(onDone = { vm.saveNameDialog() }),
                    modifier = Modifier.fillMaxWidth(),
                )
                if (vm.nameError.isNotEmpty()) {
                    Spacer(Modifier.padding(4.dp))
                    Text(vm.nameError, color = DangerRed, fontSize = 13.sp)
                }
            }
        },
        confirmButton = { Button(onClick = vm::saveNameDialog) { Text(if (renaming) "Guardar" else "Crear") } },
        dismissButton = { TextButton(onClick = vm::closeNameDialog) { Text("Cancelar") } },
    )
}

/**
 * Añadir canciones a una playlist: las de «Tus canciones» con su carátula y una casilla, con un filtro
 * por título o artista. Se abre solo al crear una playlist.
 */
@Composable
private fun PickDialog(vm: LauncheritoViewModel) {
    val list = vm.userPlaylist(vm.pickListId) ?: return
    val visible = vm.visiblePickRows()
    val marked = vm.pickedCount
    val allVisible = visible.isNotEmpty() && visible.all { vm.pickChecked[it.key] == true }
    Dialog(onDismissRequest = vm::closePickDialog, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Surface(shape = RoundedCornerShape(20.dp), color = Color(0xFF121212), modifier = Modifier.fillMaxWidth(0.94f).fillMaxSize(0.9f)) {
            Column(Modifier.padding(vertical = 16.dp)) {
                Text(
                    "Añadir canciones a «${list.name}»", fontSize = 20.sp, fontWeight = FontWeight.SemiBold,
                    maxLines = 2, overflow = TextOverflow.Ellipsis, modifier = Modifier.padding(horizontal = 20.dp),
                )
                Text(
                    "Marca las de «Tus canciones» que quieras añadir. Se ponen al final de la playlist.",
                    color = Muted, fontSize = 13.sp, modifier = Modifier.padding(horizontal = 20.dp, vertical = 4.dp),
                )
                OutlinedTextField(
                    value = vm.pickFilter,
                    onValueChange = { vm.pickFilter = it },
                    singleLine = true,
                    leadingIcon = { Icon(Icons.Rounded.Search, contentDescription = null) },
                    placeholder = { Text("Filtrar por canción o artista") },
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 6.dp),
                )
                if (visible.isNotEmpty()) {
                    TextButton(onClick = vm::togglePickVisible, modifier = Modifier.padding(horizontal = 8.dp)) {
                        Text(
                            when {
                                allVisible -> "Quitar la selección"
                                vm.pickFilter.isNotBlank() -> "Seleccionar las que se ven"
                                else -> "Seleccionar todas"
                            }
                        )
                    }
                }
                if (vm.pickRows.isEmpty()) {
                    Text(
                        "Ya están en esta playlist todas tus canciones (o aún no has guardado ninguna).",
                        color = Muted, modifier = Modifier.padding(20.dp),
                    )
                }
                LazyColumn(Modifier.weight(1f)) {
                    items(visible, key = { it.key }) { song ->
                        val checked = vm.pickChecked[song.key] == true
                        Row(
                            Modifier.fillMaxWidth().clickable { vm.pickChecked[song.key] = !checked }.padding(horizontal = 8.dp, vertical = 4.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Checkbox(
                                checked = checked,
                                onCheckedChange = { vm.pickChecked[song.key] = it },
                                colors = CheckboxDefaults.colors(checkedColor = Accent),
                            )
                            // La carátula del álbum (del .mp3 o de Spotify / YouTube Music), que Coil carga al verse.
                            SmallCover(SongCover(song.key))
                            Spacer(Modifier.width(12.dp))
                            Text(
                                buildAnnotatedString {
                                    append(song.title)
                                    if (song.artist.isNotEmpty()) withStyle(SpanStyle(color = Muted)) { append("\n" + song.artist) }
                                },
                                maxLines = 2, overflow = TextOverflow.Ellipsis, fontSize = 14.sp, modifier = Modifier.weight(1f),
                            )
                            Spacer(Modifier.width(8.dp))
                            Text(
                                song.source?.let(OnlineSongs::name) ?: ".mp3", fontSize = 11.sp,
                                color = when (song.source) {
                                    OnlineSource.Spotify -> SpotifyGreen
                                    OnlineSource.YouTubeMusic -> YouTubeRed
                                    null -> Muted
                                },
                                modifier = Modifier.padding(end = 8.dp),
                            )
                        }
                    }
                }
                Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp), horizontalArrangement = Arrangement.End) {
                    TextButton(onClick = vm::closePickDialog) { Text("Cancelar") }
                    Spacer(Modifier.width(8.dp))
                    Button(onClick = vm::confirmPick, enabled = marked > 0) {
                        Text(if (marked == 0) "Añadir" else "Añadir ${LauncheritoViewModel.songs(marked)}")
                    }
                }
            }
        }
    }
}

/**
 * Buscar en YouTube: canciones o vídeos (o el enlace de un vídeo). «Añadir» la guarda en «Tus
 * canciones» y en el mosaico; el triángulo, además, la pone a sonar. Suenan con su vídeo.
 */
@Composable
private fun YouTubeDialog(vm: LauncheritoViewModel) {
    Dialog(onDismissRequest = vm::closeYouTube, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Surface(shape = RoundedCornerShape(20.dp), color = Color(0xFF121212), modifier = Modifier.fillMaxWidth(0.96f).fillMaxSize(0.9f)) {
            Column(Modifier.padding(vertical = 16.dp)) {
                Row(Modifier.padding(horizontal = 20.dp), verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.SmartDisplay, contentDescription = null, tint = YouTubeRed)
                    Spacer(Modifier.width(10.dp))
                    Text("Buscar en YouTube", fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
                }
                Text(
                    "Busca una canción o un artista, o pega el enlace de un vídeo. Las que añadas se quedan en «Tus canciones» " +
                        "y suenan con su vídeo de YouTube.",
                    color = Muted, fontSize = 13.sp, modifier = Modifier.padding(horizontal = 20.dp, vertical = 4.dp),
                )
                OutlinedTextField(
                    value = vm.youTubeQuery,
                    onValueChange = { vm.youTubeQuery = it },
                    singleLine = true,
                    leadingIcon = { Icon(Icons.Rounded.Search, contentDescription = null) },
                    placeholder = { Text("Canción, artista o enlace de YouTube") },
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                    keyboardActions = KeyboardActions(onSearch = { vm.searchYouTube() }),
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 6.dp),
                )
                Row(Modifier.padding(horizontal = 16.dp), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    TabChip("Canciones", !vm.youTubeVideos) { vm.setYouTubeTab(false) }
                    TabChip("Vídeos", vm.youTubeVideos) { vm.setYouTubeTab(true) }
                    Spacer(Modifier.weight(1f))
                    Button(
                        onClick = vm::searchYouTube, enabled = !vm.youTubeBusy,
                        colors = ButtonDefaults.buttonColors(containerColor = YouTubeRed, contentColor = Color.White),
                    ) { Text("Buscar") }
                }
                if (vm.youTubeBusy) LinearProgressIndicator(Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 6.dp), color = YouTubeRed)
                if (vm.youTubeStatus.isNotEmpty()) {
                    Text(
                        vm.youTubeStatus, fontSize = 13.sp, color = if (vm.youTubeError) DangerRed else Muted,
                        modifier = Modifier.padding(horizontal = 20.dp, vertical = 6.dp),
                    )
                }
                LazyColumn(Modifier.weight(1f)) {
                    items(vm.youTubeResults, key = { it.id }) { track -> YouTubeRow(vm, track) }
                }
                TextButton(onClick = vm::closeYouTube, modifier = Modifier.align(Alignment.End).padding(end = 8.dp)) { Text("Cerrar") }
            }
        }
    }
}

@Composable
private fun TabChip(text: String, active: Boolean, onClick: () -> Unit) {
    Text(
        text, fontSize = 14.sp, fontWeight = if (active) FontWeight.SemiBold else FontWeight.Normal,
        color = if (active) Color.White else Muted,
        modifier = Modifier.clip(RoundedCornerShape(16.dp)).background(if (active) Accent.copy(alpha = 0.35f) else Color.Transparent)
            .clickable(onClick = onClick).padding(horizontal = 14.dp, vertical = 8.dp),
    )
}

/** Un resultado: imagen, título, artista y duración, el triángulo para escucharla ya y «Añadir». */
@Composable
private fun YouTubeRow(vm: LauncheritoViewModel, track: OnlineTrack) {
    val mp3 = vm.ownedMp3(track)
    val saved = vm.isSaved(track.key)
    Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
        SmallCover(YouTubeMusicLibrary.thumbnailUrl(track), 52.dp)
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(track.title, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
            Text(
                track.artists + if (track.durationMs > 0) "  ·  ${formatTime(track.durationMs / 1000f)}" else "",
                color = Muted, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis,
            )
        }
        IconButton(onClick = { vm.addFromYouTube(track, play = true) }) {
            Icon(Icons.Rounded.PlayArrow, contentDescription = "Escuchar ahora (también se guarda)")
        }
        val enabled = mp3 == null && !saved
        Button(
            onClick = { vm.addFromYouTube(track, play = false) },
            enabled = enabled,
            colors = ButtonDefaults.buttonColors(
                containerColor = YouTubeRed, contentColor = Color.White,
                disabledContainerColor = Color(0xFF2A2A2A), disabledContentColor = Muted,
            ),
            contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 12.dp, vertical = 6.dp),
        ) {
            Text(
                when {
                    mp3 != null -> "Ya en .mp3"
                    saved -> "✓ Guardada"
                    else -> "Añadir"
                },
                fontSize = 13.sp, maxLines = 1,
            )
        }
    }
}
