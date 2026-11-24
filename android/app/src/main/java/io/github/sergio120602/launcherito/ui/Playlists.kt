package io.github.sergio120602.launcherito.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.ArrowBack
import androidx.compose.material.icons.automirrored.rounded.FormatListBulleted
import androidx.compose.material.icons.automirrored.rounded.OpenInNew
import androidx.compose.material.icons.rounded.Add
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material.icons.rounded.Close
import androidx.compose.material.icons.rounded.DeleteOutline
import androidx.compose.material.icons.rounded.DriveFileRenameOutline
import androidx.compose.material.icons.rounded.Edit
import androidx.compose.material.icons.rounded.Favorite
import androidx.compose.material.icons.rounded.GridView
import androidx.compose.material.icons.rounded.MusicNote
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.Refresh
import androidx.compose.material.icons.rounded.Save
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil3.compose.AsyncImage
import io.github.sergio120602.launcherito.data.TrendingPlaylists
import io.github.sergio120602.launcherito.data.UserPlaylist

private val FavoritesFill = Brush.linearGradient(listOf(Color(0xFF4A1638), Color(0xFF1A0E18)))
private val ListFill = Brush.linearGradient(listOf(Color(0xFF2E1F5C), Color(0xFF15131F)))
private val NewListFill = Brush.linearGradient(listOf(Color(0xFF121212), Color(0xFF121212)))

/** Botón con forma de píldora (Reproducir, Añadir canciones, Editar…). */
@Composable
private fun ActionButton(icon: ImageVector, text: String, background: Color = Pill, iconTint: Color = Color.White, onClick: () -> Unit) {
    Row(
        Modifier.clip(RoundedCornerShape(16.dp)).background(background).clickable(onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(icon, contentDescription = null, tint = iconTint, modifier = Modifier.size(17.dp))
        Spacer(Modifier.width(6.dp))
        Text(text, fontSize = 13.sp, maxLines = 1, softWrap = false)
    }
}

/** Cuadro con un icono sobre un degradado: la «portada» de Favoritos y de las playlists. */
@Composable
private fun ListArt(list: UserPlaylist?, modifier: Modifier, iconSize: Dp = 44.dp) {
    val favorites = list?.isFavorites == true
    Box(
        modifier.clip(RoundedCornerShape(14.dp)).background(
            when {
                list == null -> NewListFill
                favorites -> FavoritesFill
                else -> ListFill
            }
        ),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            when {
                list == null -> Icons.Rounded.Add
                favorites -> Icons.Rounded.Favorite
                else -> Icons.AutoMirrored.Rounded.FormatListBulleted
            },
            contentDescription = null,
            tint = when {
                list == null -> Muted
                favorites -> FavoritePink
                else -> AccentHover
            },
            modifier = Modifier.size(iconSize),
        )
    }
}

/** Cabecera de un detalle: volver, la portada, el nombre y lo de debajo. */
@Composable
private fun DetailHeader(title: String, subtitle: String, onBack: () -> Unit, art: @Composable () -> Unit) {
    Row(Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Rounded.ArrowBack, contentDescription = "Volver") }
        art()
        Spacer(Modifier.width(14.dp))
        Column(Modifier.weight(1f)) {
            Text(title, fontSize = 22.sp, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
            Text(subtitle, color = Muted, fontSize = 13.sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
        }
    }
}

// ─────────────────────────────── Playlists ───────────────────────────────

/** Pestaña Playlists: Favoritos, tus playlists y «Nueva playlist», o el detalle de una. */
@Composable
fun PlaylistsView(vm: LauncheritoViewModel) {
    val open = vm.userPlaylist(vm.openCategory)
    if (open != null) {
        PlaylistDetail(vm, open)
        return
    }
    val lists = vm.userPlaylists()
    LazyVerticalGrid(
        columns = GridCells.Adaptive(150.dp),
        contentPadding = PaddingValues(16.dp),
        horizontalArrangement = Arrangement.spacedBy(14.dp),
        verticalArrangement = Arrangement.spacedBy(18.dp),
        modifier = Modifier.fillMaxSize(),
    ) {
        items(lists, key = { it.id }) { list ->
            ListCard(
                art = { ListArt(list, it) },
                title = list.name,
                detail = LauncheritoViewModel.songs(list.songs.size) + if (vm.sessionListId == list.id) "  ·  sonando" else "",
            ) { vm.openCategory(list.id) }
        }
        item(key = "nueva") {
            ListCard(art = { ListArt(null, it) }, title = "Nueva playlist", detail = "Ponle nombre y añade canciones") {
                vm.openNameDialog()
            }
        }
    }
}

@Composable
private fun ListCard(art: @Composable (Modifier) -> Unit, title: String, detail: String, onClick: () -> Unit) {
    Column(Modifier.clip(RoundedCornerShape(14.dp)).clickable(onClick = onClick)) {
        art(Modifier.fillMaxWidth().aspectRatio(1f))
        Text(
            title, fontSize = 15.sp, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis,
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        )
        Text(detail, fontSize = 12.sp, color = Muted, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}

/**
 * Una playlist: cabecera, botones y sus canciones, en mosaico (las portadas se mueven y agrandan con
 * «Editar») o en lista (la X quita la canción). Al pulsar una suena la playlist desde ella.
 */
@Composable
private fun PlaylistDetail(vm: LauncheritoViewModel, list: UserPlaylist) {
    val available = vm.availableKeys(list.songs.toList())
    val mosaic = !list.listView && available.isNotEmpty()
    Column(Modifier.fillMaxSize()) {
        DetailHeader(
            list.name,
            (if (list.isFavorites) "Favoritos · " else "Playlist · ") + LauncheritoViewModel.songs(list.songs.size) +
                if (vm.sessionListId == list.id) "  ·  sonando ahora" else "",
            onBack = { vm.openCategory(null) },
        ) { ListArt(list, Modifier.size(64.dp), iconSize = 30.dp) }

        Row(
            Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(horizontal = 16.dp, vertical = 6.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            if (vm.playlistEditing) {
                ActionButton(Icons.Rounded.Check, "Listo", StopEditGreen) { vm.editPlaylist(false) }
            } else {
                if (available.isNotEmpty()) ActionButton(Icons.Rounded.PlayArrow, "Reproducir", Accent) { vm.playUserList(list.id, goToSongs = true) }
                ActionButton(Icons.Rounded.Add, "Añadir canciones") { vm.openPickDialog(list.id) }
                if (mosaic) ActionButton(Icons.Rounded.Edit, "Editar") { vm.editPlaylist(true) }
                if (list.listView) ActionButton(Icons.Rounded.GridView, "Vista mosaico") { vm.setListView(list.id, false) }
                else ActionButton(Icons.AutoMirrored.Rounded.FormatListBulleted, "Vista lista") { vm.setListView(list.id, true) }
                if (!list.isFavorites) {
                    ActionButton(Icons.Rounded.DriveFileRenameOutline, "Renombrar") { vm.openNameDialog(renamingId = list.id) }
                    ActionButton(Icons.Rounded.DeleteOutline, "Borrar", iconTint = DangerRed) { vm.confirmDeleteList = list.id }
                }
            }
        }
        if (vm.playlistEditing) {
            Text(
                "Mantén pulsada una portada para moverla (cambia el orden de la playlist); arrastra su esquina para cambiar el tamaño",
                color = Muted, fontSize = 12.sp, modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 2.dp),
            )
        }

        when {
            list.songs.isEmpty() -> Text(
                if (list.isFavorites) "Aún no tienes favoritos. Mientras suena una canción, pulsa el corazón de los controles, " +
                    "o mantén pulsada una portada y elige «Añadir a Favoritos»."
                else "Esta playlist está vacía. Añade canciones con «Añadir canciones», manteniendo pulsada una portada o con el " +
                    "botón de añadir de los controles.",
                color = Muted, fontSize = 14.sp, modifier = Modifier.padding(20.dp),
            )
            mosaic -> MosaicView(vm, Modifier.padding(horizontal = 10.dp), vm.playlistMosaic)
            else -> LazyColumn(Modifier.fillMaxSize()) {
                itemsIndexed(list.songs.toList(), key = { _, key -> key }) { _, key ->
                    val (title, artist) = vm.describeSong(key)
                    val canPlay = key in available
                    Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.alpha(if (canPlay) 1f else 0.45f)) {
                        Box(Modifier.weight(1f)) {
                            SongRow(vm, key, title, artist, onClick = { if (canPlay) vm.playFromList(list.id, key) })
                        }
                        IconButton(onClick = {
                            if (list.isFavorites) vm.toggleFavorite(key) else vm.setInList(list.id, key, add = false)
                        }) {
                            Icon(
                                Icons.Rounded.Close, tint = Muted, modifier = Modifier.size(18.dp),
                                contentDescription = if (list.isFavorites) "Quitar de Favoritos" else "Quitar de la playlist",
                            )
                        }
                    }
                }
            }
        }
    }
}

// ─────────────────────────────── Del momento ───────────────────────────────

/** Pestaña Del momento: las playlists de Spotify con su portada, o las canciones de una por puesto. */
@Composable
fun TrendingView(vm: LauncheritoViewModel) {
    val open = vm.openCategory?.let { vm.trending[it] }?.takeIf { it.tracks.isNotEmpty() }
    if (open != null) {
        TrendingDetail(vm, open.id)
        return
    }
    val lists = vm.trending.lists.filter { it.tracks.isNotEmpty() }
    Column(Modifier.fillMaxSize()) {
        // De dónde salen, cuándo se renovaron y «Actualizar».
        Row(Modifier.fillMaxWidth().padding(start = 16.dp, end = 8.dp), verticalAlignment = Alignment.CenterVertically) {
            SpotifyBadge(18.dp)
            Spacer(Modifier.width(8.dp))
            val schedule = "se renuevan cada día a las ${TrendingPlaylists.REFRESH_HOUR}:00"
            Text(
                when {
                    vm.trendingRefreshing -> "Leyendo las playlists del momento en Spotify…"
                    vm.trendingWhen() != null -> "De Spotify · actualizadas ${vm.trendingWhen()} · $schedule" +
                        if (vm.trendingError != null && vm.trending.isStale()) " · no se han podido renovar hoy" else ""
                    else -> "De Spotify · $schedule"
                },
                color = Muted, fontSize = 12.sp, maxLines = 2, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f),
            )
            IconButton(onClick = { vm.refreshTrending() }, enabled = !vm.trendingRefreshing) {
                Icon(Icons.Rounded.Refresh, contentDescription = "Actualizar ahora", tint = if (vm.trendingRefreshing) PlaceholderColor else Color.White)
            }
        }
        if (lists.isEmpty()) {
            Box(Modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.Center) {
                Text(
                    if (vm.trendingRefreshing) "Leyendo las playlists del momento en Spotify…"
                    else "Aún no se han podido leer las playlists del momento. Comprueba la conexión y pulsa actualizar.",
                    color = Muted, textAlign = TextAlign.Center,
                )
            }
            return
        }
        LazyVerticalGrid(
            columns = GridCells.Adaptive(150.dp),
            contentPadding = PaddingValues(16.dp),
            horizontalArrangement = Arrangement.spacedBy(14.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp),
            modifier = Modifier.fillMaxSize(),
        ) {
            items(lists, key = { it.id }) { list ->
                ListCard(
                    art = { CoverArt(list.coverUrl, it) },
                    title = list.name,
                    detail = LauncheritoViewModel.songs(list.tracks.size) + " · Spotify" +
                        if (vm.sessionTrendingId == list.id) "  ·  sonando" else "",
                ) { vm.openCategory(list.id) }
            }
        }
    }
}

@Composable
private fun CoverArt(url: String?, modifier: Modifier) {
    Box(modifier.clip(RoundedCornerShape(14.dp)).background(EmptyFill), contentAlignment = Alignment.Center) {
        Icon(Icons.Rounded.MusicNote, contentDescription = null, tint = SpotifyGreen, modifier = Modifier.size(44.dp))
        if (url != null) AsyncImage(url, contentDescription = null, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
    }
}

/** Canciones de una playlist del momento, por puesto. Al pulsar una suena la playlist desde ella. */
@Composable
private fun TrendingDetail(vm: LauncheritoViewModel, id: String) {
    val list = vm.trending[id] ?: return
    val uri = LocalUriHandler.current
    Column(Modifier.fillMaxSize()) {
        DetailHeader(
            list.name,
            "Spotify · " + LauncheritoViewModel.songs(list.tracks.size) +
                (vm.trendingWhen()?.let { " · actualizada $it" } ?: "") +
                if (vm.sessionTrendingId == id) "  ·  sonando ahora" else "",
            onBack = { vm.openCategory(null) },
        ) { CoverArt(list.coverUrl, Modifier.size(64.dp)) }
        Row(
            Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(horizontal = 16.dp, vertical = 6.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            ActionButton(Icons.Rounded.PlayArrow, "Reproducir", Accent) { vm.playTrending(id, goToSongs = true) }
            ActionButton(Icons.Rounded.Save, "Guardar en mis playlists") { vm.saveTrending(id) }
            ActionButton(Icons.AutoMirrored.Rounded.OpenInNew, "Ver en Spotify", iconTint = SpotifyGreen) {
                uri.openUri("https://open.spotify.com/playlist/$id")
            }
        }
        LazyColumn(Modifier.fillMaxSize()) {
            itemsIndexed(list.tracks, key = { i, song -> "$i:${song.key}" }) { i, song ->
                val key = vm.trendingPlayKey(song)
                SongRow(vm, key, "${i + 1}.  ${song.title}", song.artist, onClick = { vm.playFromTrending(id, key) })
            }
        }
    }
}
