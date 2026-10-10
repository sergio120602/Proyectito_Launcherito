package io.github.sergio120602.launcherito.ui

import android.view.ViewGroup
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.VolumeUp
import androidx.compose.material.icons.rounded.Add
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material.icons.rounded.Close
import androidx.compose.material.icons.rounded.CropSquare
import androidx.compose.material.icons.rounded.DeleteOutline
import androidx.compose.material.icons.rounded.Edit
import androidx.compose.material.icons.rounded.FormatSize
import androidx.compose.material.icons.rounded.GridView
import androidx.compose.material.icons.rounded.Home
import androidx.compose.material.icons.rounded.LibraryMusic
import androidx.compose.material.icons.rounded.MoreVert
import androidx.compose.material.icons.rounded.MusicNote
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.Search
import androidx.compose.material.icons.rounded.SmartDisplay
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import coil3.compose.AsyncImage
import io.github.sergio120602.launcherito.data.SongCover
import io.github.sergio120602.launcherito.data.SpotifyLibrary
import io.github.sergio120602.launcherito.player.YouTubeVideo

@Composable
fun PlayerScreen(vm: LauncheritoViewModel, onLoadFiles: () -> Unit) {
    // Atrás va deshaciendo: el buscador, la edición, el artista abierto, la pestaña y, por último, al menú.
    BackHandler {
        when {
            vm.searchQuery.isNotEmpty() -> vm.searchQuery = ""
            vm.isEditing -> vm.editMosaic(false)
            vm.openCategory != null -> vm.openCategory(null)
            vm.section != Section.Songs -> vm.showSection(Section.Songs)
            else -> vm.goHome()
        }
    }

    Column(Modifier.fillMaxSize()) {
        TopBar(vm, onLoadFiles)
        SectionBar(vm)
        StatusLine(vm)
        Box(Modifier.weight(1f).fillMaxWidth()) {
            when {
                vm.section != Section.Songs -> CategoryView(vm)
                vm.mosaicView -> MosaicView(vm, Modifier.padding(horizontal = 10.dp))
                else -> SingleView(vm)
            }
            SearchResults(vm)
        }
        // En artistas y géneros los controles van abajo.
        if (vm.section != Section.Songs) BottomBar(vm)
    }
}

@Composable
private fun TopBar(vm: LauncheritoViewModel, onLoadFiles: () -> Unit) {
    var menuOpen by remember { mutableStateOf(false) }
    Row(
        Modifier.fillMaxWidth().padding(start = 4.dp, end = 4.dp, top = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconButton(onClick = vm::goHome) { Icon(Icons.Rounded.Home, contentDescription = "Inicio") }
        SearchField(vm, Modifier.weight(1f))
        Box {
            IconButton(onClick = { menuOpen = true }) { Icon(Icons.Rounded.MoreVert, contentDescription = "Más") }
            DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                MenuItem("Cargar archivos", Icons.Rounded.Add) { menuOpen = false; onLoadFiles() }
                DropdownMenuItem(
                    text = { Text("Lista de Spotify") },
                    leadingIcon = { SpotifyBadge(22.dp) },
                    onClick = { menuOpen = false; vm.openSpotifyDialog() },
                )
                if (vm.unloadedCount > 0) {
                    MenuItem("Tus canciones: cargar ${if (vm.unloadedCount == 1) "la que falta" else "las ${vm.unloadedCount} que faltan"}", Icons.Rounded.LibraryMusic) {
                        menuOpen = false
                        vm.loadLibrary()
                    }
                }
                if (vm.libraryCount > 0) {
                    MenuItem("Borrar canciones", Icons.Rounded.DeleteOutline) { menuOpen = false; vm.openDeleteDialog() }
                }
                HorizontalDivider()
                // Letra pequeña o grande: se marca si está puesta la grande.
                DropdownMenuItem(
                    text = { Text("Letra grande") },
                    leadingIcon = { Icon(Icons.Rounded.FormatSize, contentDescription = null) },
                    trailingIcon = { if (vm.largeText) Icon(Icons.Rounded.Check, contentDescription = "Activada", tint = Accent) },
                    onClick = { menuOpen = false; vm.chooseLargeText(!vm.largeText) },
                )
            }
        }
    }
}

@Composable
private fun MenuItem(text: String, icon: ImageVector, onClick: () -> Unit) {
    DropdownMenuItem(text = { Text(text) }, leadingIcon = { Icon(icon, contentDescription = null) }, onClick = onClick)
}

/** Buscador con forma de píldora: lupa, texto de ayuda mientras está vacío y X para borrarlo. */
@Composable
private fun SearchField(vm: LauncheritoViewModel, modifier: Modifier) {
    val focus = LocalFocusManager.current
    BasicTextField(
        value = vm.searchQuery,
        onValueChange = { vm.searchQuery = it },
        singleLine = true,
        textStyle = TextStyle(color = Color.White, fontSize = 15.sp),
        cursorBrush = SolidColor(Color.White),
        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
        // Buscar elige la primera.
        keyboardActions = KeyboardActions(onSearch = {
            vm.searchResults().firstOrNull()?.let { vm.pickSearchResult(it.key) }
            focus.clearFocus()
        }),
        modifier = modifier,
        decorationBox = { inner ->
            Row(
                Modifier.clip(RoundedCornerShape(22.dp)).background(Pill).padding(start = 12.dp, end = 4.dp).heightIn(min = 42.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(Icons.Rounded.Search, contentDescription = null, tint = Muted, modifier = Modifier.size(20.dp))
                Box(Modifier.weight(1f).padding(horizontal = 8.dp)) {
                    if (vm.searchQuery.isEmpty()) {
                        Text("Buscar", color = Muted, fontSize = 15.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    }
                    inner()
                }
                if (vm.searchQuery.isNotEmpty()) {
                    IconButton(onClick = { vm.searchQuery = "" }, modifier = Modifier.size(36.dp)) {
                        Icon(Icons.Rounded.Close, contentDescription = "Borrar la búsqueda", modifier = Modifier.size(18.dp))
                    }
                }
            }
        },
    )
}

/** Pestañas Canciones / Artistas / Géneros. */
@Composable
private fun SectionBar(vm: LauncheritoViewModel) {
    Row(
        Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(horizontal = 12.dp, vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(if (LocalLargeText.current) 2.dp else 6.dp),
    ) {
        Tab("Canciones", vm.section == Section.Songs) { vm.showSection(Section.Songs) }
        Tab("Artistas", vm.section == Section.Artists) { vm.showSection(Section.Artists) }
        Tab("Géneros", vm.section == Section.Genres) { vm.showSection(Section.Genres) }
    }
}

@Composable
private fun Tab(text: String, active: Boolean, onClick: () -> Unit) {
    Text(
        text,
        fontSize = 14.sp,
        fontWeight = if (active) FontWeight.SemiBold else FontWeight.Normal,
        color = if (active) Color.White else Muted,
        maxLines = 1,
        softWrap = false,
        modifier = Modifier.clip(RoundedCornerShape(16.dp))
            .background(if (active) Accent.copy(alpha = 0.25f) else Color.Transparent)
            .clickable(onClick = onClick)
            .padding(horizontal = if (LocalLargeText.current) 9.dp else 12.dp, vertical = 7.dp),
    )
}

@Composable
private fun ActionPill(icon: ImageVector, text: String, background: Color, onClick: () -> Unit) {
    Row(
        Modifier.clip(RoundedCornerShape(16.dp)).background(background).clickable(onClick = onClick)
            .padding(horizontal = 10.dp, vertical = 7.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(icon, contentDescription = null, modifier = Modifier.size(16.dp))
        Spacer(Modifier.width(6.dp))
        Text(text, fontSize = 13.sp, maxLines = 1, softWrap = false)
    }
}

/**
 * Avisos a la izquierda y, en Canciones, los botones de la vista a la derecha: Editar (solo en el
 * mosaico) y el cambio entre la vista original y la vista mosaico.
 */
@Composable
private fun StatusLine(vm: LauncheritoViewModel) {
    // Con la letra grande no caben el aviso y los botones en una fila: el aviso va arriba, solo.
    val large = LocalLargeText.current
    if (large) StatusText(vm, Modifier.fillMaxWidth().padding(start = 16.dp, end = 16.dp, bottom = 6.dp))
    Row(
        Modifier.fillMaxWidth().padding(start = 16.dp, end = 12.dp, bottom = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp, if (large) Alignment.End else Alignment.Start),
    ) {
        if (!large) StatusText(vm, Modifier.weight(1f))
        if (vm.mosaicVisible) {
            if (vm.isEditing) {
                // Dejar edición, en verde; Editar se queda en morado mientras dura.
                ActionPill(Icons.Rounded.Check, "Listo", StopEditGreen) { vm.editMosaic(false) }
            } else {
                ActionPill(Icons.Rounded.Edit, "Editar", Pill) { vm.editMosaic(true) }
            }
        }
        if (vm.section == Section.Songs) {
            // El botón muestra la vista a la que se cambia al pulsarlo.
            if (vm.mosaicView) ActionPill(Icons.Rounded.CropSquare, "Vista original", Pill, vm::toggleMosaicView)
            else ActionPill(Icons.Rounded.GridView, "Vista mosaico", Pill, vm::toggleMosaicView)
        }
    }
    // La ayuda del modo edición va en su propia línea: al lado de los botones no cabe.
    if (vm.isEditing) {
        Text(
            "Mantén pulsada una portada para moverla; arrastra su esquina para cambiar el tamaño",
            color = Muted, fontSize = 12.sp,
            modifier = Modifier.fillMaxWidth().padding(start = 16.dp, end = 16.dp, bottom = 6.dp),
        )
    }
}

/** Una línea de avisos: Spotify y vídeo, la clasificación con Deezer, la edición o cuántas canciones hay. */
@Composable
private fun StatusText(vm: LauncheritoViewModel, modifier: Modifier) {
    val text = when {
        vm.statusText.isNotEmpty() -> vm.statusText
        vm.catalogStatus.isNotEmpty() -> vm.catalogStatus
        else -> LauncheritoViewModel.songs(vm.tiles.size)
    }
    Text(
        text, color = Muted, fontSize = 12.sp, maxLines = if (vm.section == Section.Songs) 2 else 1,
        overflow = TextOverflow.Ellipsis, modifier = modifier,
    )
}

/** Vista original: la carátula grande (o el vídeo de YouTube de la canción), el título y los controles. */
@Composable
private fun SingleView(vm: LauncheritoViewModel) {
    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(horizontal = 20.dp, vertical = 8.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        val video = vm.songVideo
        // Marco 16:9 para el vídeo y cuadrado para la carátula.
        Box(
            Modifier.widthIn(max = if (vm.videoShown) 560.dp else 400.dp).fillMaxWidth()
                .aspectRatio(if (vm.videoShown) 16f / 9f else 1f)
                .clip(RoundedCornerShape(16.dp)).background(EmptyFill),
            contentAlignment = Alignment.Center,
        ) {
            // El WebView no se puede hacer transparente: mientras no se ve el vídeo, la carátula se
            // dibuja encima y lo tapa (el vídeo sigue sonando debajo).
            if (video != null && vm.videoHost == VideoHost.Single) {
                key(video) {
                    VideoSurface(video, visible = vm.videoShown, modifier = Modifier.fillMaxSize())
                }
            }
            if (!vm.videoShown) {
                Box(Modifier.fillMaxSize().background(EmptyFill), contentAlignment = Alignment.Center) {
                    Icon(Icons.Rounded.MusicNote, contentDescription = null, tint = PlaceholderColor, modifier = Modifier.size(96.dp))
                    vm.currentKey?.let { key ->
                        AsyncImage(SongCover(key), contentDescription = vm.title, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
                    }
                }
            }
            vm.videoStatus?.let { status ->
                Text(
                    status, fontSize = 13.sp,
                    modifier = Modifier.align(Alignment.BottomCenter).padding(12.dp)
                        .clip(RoundedCornerShape(12.dp)).background(Color(0xCC000000)).padding(horizontal = 12.dp, vertical = 6.dp),
                )
            }
        }
        Spacer(Modifier.size(18.dp))
        Text(vm.title, fontSize = 24.sp, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
        Text(vm.artist, fontSize = 15.sp, color = Muted, maxLines = 1, overflow = TextOverflow.Ellipsis)
        Spacer(Modifier.size(10.dp))
        PlayerControls(vm, Modifier.widthIn(max = 480.dp).fillMaxWidth())
    }
}

/**
 * Coloca la vista del vídeo. Es siempre la misma vista (no se recarga): si estaba en otro sitio, se
 * saca de allí antes de ponerla aquí. Invisible ([visible] false) sigue sonando, pero no tapa nada.
 */
@Composable
fun VideoSurface(video: YouTubeVideo, visible: Boolean, modifier: Modifier) {
    AndroidView(
        factory = {
            (video.view.parent as? ViewGroup)?.removeView(video.view)
            video.view
        },
        // Además de taparlo, se pone transparente: así no se ve ni un instante al cambiar de sitio.
        update = { it.alpha = if (visible) 1f else 0f },
        modifier = modifier,
    )
}

/** Barra de abajo en artistas y géneros: lo que suena y los controles. */
@Composable
private fun BottomBar(vm: LauncheritoViewModel) {
    Surface(color = Color(0xFF111111), shape = RoundedCornerShape(topStart = 18.dp, topEnd = 18.dp)) {
        Column(Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp)) {
            Text(vm.title, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
            Text(vm.artist, color = Muted, fontSize = 13.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
            PlayerControls(vm)
        }
    }
}

/** Resultados del buscador, encima de lo que se esté viendo. */
@Composable
private fun SearchResults(vm: LauncheritoViewModel) {
    if (vm.searchQuery.isBlank()) return
    val focus = LocalFocusManager.current
    val results = vm.searchResults()
    Surface(
        color = Color(0xFF161616),
        shape = RoundedCornerShape(16.dp),
        shadowElevation = 8.dp,
        modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp).heightIn(max = 420.dp),
    ) {
        if (results.isEmpty()) {
            Text(
                "Ninguna canción se llama «${vm.searchQuery.trim()}»", color = Muted,
                maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.padding(16.dp),
            )
        } else {
            LazyColumn {
                items(results, key = { it.key }) { result ->
                    SongRow(vm, result.key, result.title, result.artist, result.highlight) {
                        focus.clearFocus()
                        vm.pickSearchResult(result.key)
                    }
                    HorizontalDivider(color = Color(0xFF222222))
                }
            }
        }
    }
}

/**
 * Fila de una canción (detalle de artista o género, y resultados del buscador): icono, título y un
 * dato a la derecha. La que suena sale resaltada. Si se indica, una parte del título va en morado (lo
 * que coincide con la búsqueda).
 */
@Composable
fun SongRow(
    vm: LauncheritoViewModel,
    key: String,
    title: String,
    detail: String,
    highlight: IntRange? = null,
    onClick: () -> Unit = { vm.selectSong(key) },
) {
    val playing = key == vm.currentKey
    val youTube = SpotifyLibrary.isSpotify(key)
    Row(
        Modifier.fillMaxWidth().clickable(onClick = onClick).padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            when {
                playing -> Icons.AutoMirrored.Rounded.VolumeUp
                youTube -> Icons.Rounded.SmartDisplay
                else -> Icons.Rounded.PlayArrow
            },
            contentDescription = null,
            tint = if (playing) Accent else Muted,
            modifier = Modifier.size(20.dp),
        )
        Spacer(Modifier.width(12.dp))
        Text(
            buildAnnotatedString {
                if (highlight != null && highlight.last < title.length) {
                    append(title.substring(0, highlight.first))
                    withStyle(SpanStyle(color = Accent, fontWeight = FontWeight.SemiBold)) { append(title.substring(highlight)) }
                    append(title.substring(highlight.last + 1))
                } else {
                    append(title)
                }
            },
            color = if (playing) Accent else Color.White,
            maxLines = 1, overflow = TextOverflow.Ellipsis,
            modifier = Modifier.weight(1f),
        )
        if (detail.isNotEmpty()) {
            Spacer(Modifier.width(12.dp))
            Text(detail, color = Muted, fontSize = 13.sp, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.widthIn(max = 140.dp))
        }
    }
}
