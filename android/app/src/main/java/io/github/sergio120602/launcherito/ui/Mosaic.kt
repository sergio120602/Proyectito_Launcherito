package io.github.sergio120602.launcherito.ui

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.VectorConverter
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.awaitLongPressOrCancellation
import androidx.compose.foundation.gestures.drag
import androidx.compose.foundation.gestures.scrollBy
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.MusicNote
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.runtime.snapshots.SnapshotStateList
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.zIndex
import coil3.compose.AsyncImage
import io.github.sergio120602.launcherito.data.OnlineSource
import io.github.sergio120602.launcherito.data.SongCover
import io.github.sergio120602.launcherito.data.SongTags
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlin.math.abs
import kotlin.math.ceil
import kotlin.math.max
import kotlin.math.min
import kotlin.math.roundToInt

private val Gap = 8.dp
private val TileRadius = 14.dp
// Ancho que necesitan los controles dentro de la portada que suena (más con la letra grande).
private val ControlsMinWidth = 300.dp
private val ControlsMinWidthLarge = 340.dp
private val GripSize = 40.dp            // zona del tirador (algo mayor que el dibujo, para el dedo)

/** Columnas y tamaño de celda del mosaico (en píxeles), según el ancho de la pantalla. */
private class MosaicMetrics(val columns: Int, val cell: Float, val gap: Float, controlsMin: Float) {
    /** La que suena ocupa lo necesario para que quepan los controles (en un móvil, todo el ancho). */
    val activeMinSpan = ceil((controlsMin + gap) / (cell + gap)).toInt().coerceIn(2, columns)

    /** Cuántas celdas de lado ocupa una pieza que mide [size] (como mínimo 1, como mucho todas las columnas). */
    fun spanForSize(size: Float) = ((size + gap) / (cell + gap)).roundToInt().coerceIn(1, columns)

    /** Hueco que ocuparía una pieza de [span] celdas puesta donde empieza [slot]. */
    fun areaFrom(slot: Rect, span: Int): Rect {
        val step = cell + gap
        val column = minOf((slot.left / step).roundToInt(), columns - span).coerceAtLeast(0)
        val size = span * cell + (span - 1) * gap
        val top = (slot.top / step).roundToInt() * step
        return Rect(column * step, top, column * step + size, top + size)
    }

    fun slot(row: Int, column: Int, span: Int): Rect {
        val size = span * cell + (span - 1) * gap
        return Rect(Offset(column * (cell + gap), row * (cell + gap)), androidx.compose.ui.geometry.Size(size, size))
    }
}

/**
 * Reparte las piezas en una cuadrícula de celdas cuadradas: cada una ocupa span x span celdas y los
 * huecos que dejan las grandes se rellenan con las pequeñas siguientes (como "grid-auto-flow: dense"
 * en CSS). Devuelve la fila y la columna de cada una, y cuántas filas ocupa el mosaico.
 */
private fun packMosaic(spans: List<Int>, columns: Int): Pair<List<Pair<Int, Int>>, Int> {
    val occupied = mutableListOf<BooleanArray>()
    var firstFreeRow = 0
    var rows = 0
    val result = ArrayList<Pair<Int, Int>>(spans.size)
    for (span in spans) {
        // Las filas ya completas no se vuelven a revisar.
        while (firstFreeRow < occupied.size && occupied[firstFreeRow].all { it }) firstFreeRow++
        var spot: Pair<Int, Int>? = null
        var row = firstFreeRow
        while (spot == null) {
            while (occupied.size < row + span) occupied.add(BooleanArray(columns))
            for (column in 0..columns - span) {
                if (fits(occupied, row, column, span)) {
                    spot = row to column
                    break
                }
            }
            row++
        }
        val (r0, c0) = spot
        for (r in r0 until r0 + span) for (c in c0 until c0 + span) occupied[r][c] = true
        rows = max(rows, r0 + span)
        result.add(spot)
    }
    return result to rows
}

private fun fits(occupied: List<BooleanArray>, row: Int, column: Int, span: Int): Boolean {
    for (r in row until row + span) for (c in column until column + span) if (occupied[r][c]) return false
    return true
}

/**
 * Lo que necesita un mosaico: sus portadas y qué hacer con ellas. Hay dos: el del reproductor (todas
 * las canciones cargadas, con los controles dentro de la que suena) y el de una playlist (sus
 * canciones; moverlas cambia el orden de la playlist).
 *
 * @param activeKey la portada que lleva dentro los controles (null: ninguna).
 * @param bottomBar sale la barra de abajo cuando la portada que suena se pierde de vista.
 */
class MosaicModel(
    val tiles: SnapshotStateList<Tile>,
    val isEditing: () -> Boolean,
    val activeKey: () -> String?,
    val onSelect: (String) -> Unit,
    val onLongPress: (String) -> Unit,
    val onMove: (Tile, Tile) -> Unit,
    val onRemember: () -> Unit,
    val bottomBar: Boolean,
)

/**
 * Modo edición del mosaico: una portada se mantiene pulsada y se arrastra a otro sitio (las demás se
 * apartan), y con el tirador de su esquina se agranda o se encoge de celda en celda. Al agrandarla,
 * las grandes que quedan debajo se encogen para dejarle sitio; al encogerla, la siguiente crece y
 * ocupa el hueco.
 */
private class MosaicEditor(private val model: MosaicModel) {
    var metrics: MosaicMetrics? = null
    var slots: Map<Tile, Rect> = emptyMap()   // dónde está cada portada ahora mismo
    var gripPx = 0f

    var dragged by mutableStateOf<Tile?>(null)
        private set
    var dragPoint by mutableStateOf(Offset.Zero)   // dedo, en coordenadas del mosaico
    var grab = Offset.Zero                         // dónde se ha cogido, desde la esquina de la portada
        private set
    private var lastTarget: Tile? = null

    var resized by mutableStateOf<Tile?>(null)
        private set
    private var resizeOrigin = Rect.Zero
    private var startSpan = 1
    private var before: Map<Tile, Pair<Int, Rect>> = emptyMap()   // cómo estaba todo al coger el tirador

    fun tileAt(point: Offset, except: Tile? = null): Tile? =
        slots.entries.firstOrNull { it.key != except && it.value.contains(point) }?.key

    fun onGrip(tile: Tile, point: Offset): Boolean {
        val slot = slots[tile] ?: return false
        return point.x >= slot.right - gripPx && point.y >= slot.bottom - gripPx
    }

    fun effectiveSpan(tile: Tile): Int {
        val m = metrics ?: return tile.baseSpan
        val min = if (tile.key == model.activeKey()) m.activeMinSpan else 1
        return max(min, tile.baseSpan).coerceIn(1, m.columns)
    }

    // ───────────────────────────── Mover ─────────────────────────────

    fun beginDrag(tile: Tile, point: Offset) {
        val slot = slots[tile] ?: return
        dragged = tile
        grab = point - slot.topLeft
        dragPoint = point
    }

    fun drag(point: Offset) {
        val tile = dragged ?: return
        dragPoint = point
        val target = tileAt(point, except = tile)
        if (target != lastTarget) {
            lastTarget = target
            if (target != null) model.onMove(tile, target)
        }
    }

    // ───────────────────────────── Tamaño ─────────────────────────────

    fun beginResize(tile: Tile) {
        resizeOrigin = slots[tile] ?: return
        resized = tile
        startSpan = effectiveSpan(tile)
        before = slots.mapValues { (t, slot) -> t.baseSpan to slot }
    }

    fun resize(point: Offset) {
        val tile = resized ?: return
        val m = metrics ?: return
        // El tamaño lo marca el dedo respecto a la esquina de arriba a la izquierda, por la diagonal.
        val size = max(point.x - resizeOrigin.left, point.y - resizeOrigin.top)
        val min = if (tile.key == model.activeKey()) m.activeMinSpan else 1
        val span = max(min, m.spanForSize(size))
        if (span == effectiveSpan(tile)) return

        // Siempre desde como estaba al coger el tirador, así ir y volver no deja cambios sueltos.
        for ((other, was) in before) other.baseSpan = was.first
        tile.baseSpan = span
        if (span > startSpan) shrinkCovered(tile, span, m)
        else if (span < startSpan) growNext(tile, startSpan - span, m)
    }

    /** Las grandes que quedarían debajo de la portada agrandada pasan a ocupar una celda. */
    private fun shrinkCovered(tile: Tile, span: Int, m: MosaicMetrics) {
        val area = m.areaFrom(resizeOrigin, span).deflate(1f)   // las que solo tocan el borde no cuentan
        for ((other, was) in before) {
            if (other != tile && other.baseSpan > 1 && was.second.overlaps(area)) other.baseSpan = 1
        }
    }

    /** La siguiente portada (o la anterior, si es la última) crece lo que ha encogido esta. */
    private fun growNext(tile: Tile, freed: Int, m: MosaicMetrics) {
        val tiles = model.tiles.toList()
        val at = tiles.indexOf(tile)
        val neighbour = (tiles.drop(at + 1) + tiles.take(at).reversed()).firstOrNull { it.key != model.activeKey() }
        if (neighbour != null) neighbour.baseSpan = minOf(m.columns, neighbour.baseSpan + freed)
    }

    // ───────────────────────────── Común ─────────────────────────────

    /** Suelta lo que se lleve cogido: la portada vuela hasta su sitio y se guarda cómo ha quedado. */
    fun end() {
        if (dragged == null && resized == null) return
        dragged = null
        lastTarget = null
        resized = null
        before = emptyMap()
        model.onRemember()
    }
}

/**
 * Vista mosaico: las canciones como portadas de distintos tamaños. En el del reproductor la que suena
 * se ve en grande, con la barra y los controles dentro de la propia portada.
 */
@Composable
fun MosaicView(vm: LauncheritoViewModel, modifier: Modifier = Modifier, model: MosaicModel = vm.mainMosaic) {
    val density = LocalDensity.current
    val editor = remember(model) { MosaicEditor(model) }
    val editing = model.isEditing()
    val activeKey = model.activeKey()
    val scroll = rememberScrollState()
    val large = LocalLargeText.current

    BoxWithConstraints(modifier.fillMaxSize()) {
        val width = constraints.maxWidth.toFloat()
        val viewport = constraints.maxHeight.toFloat()
        val gap = with(density) { Gap.toPx() }
        // En un móvil las portadas son más pequeñas que en el PC (190 px): caben 3 por fila (2 con la
        // letra grande, para que se lean los títulos).
        val target = with(density) {
            (if (maxWidth < 600.dp) (if (large) 150.dp else 112.dp) else (if (large) 210.dp else 170.dp)).toPx()
        }
        val columns = max(2, ((width + gap) / (target + gap)).toInt())
        val cell = max(1f, (width - gap * (columns - 1)) / columns)
        val metrics = MosaicMetrics(columns, cell, gap, with(density) { (if (large) ControlsMinWidthLarge else ControlsMinWidth).toPx() })
        editor.metrics = metrics
        editor.gripPx = with(density) { GripSize.toPx() }

        val tiles = model.tiles.toList()
        val spans = tiles.map(editor::effectiveSpan)
        val (spots, rows) = packMosaic(spans, columns)
        val slots = tiles.indices.associate { i -> tiles[i] to metrics.slot(spots[i].first, spots[i].second, spans[i]) }
        editor.slots = slots
        val height = if (rows == 0) 0f else rows * cell + (rows - 1) * gap
        val activeIndex = tiles.indexOfFirst { it.key == activeKey }
        val activeSlot = tiles.getOrNull(activeIndex)?.let { slots[it] }

        // La barra de abajo sale cuando los controles de la portada que suena (en su franja de abajo)
        // se han perdido de vista al desplazarse, para pausar o cambiar de canción sin ir a buscarla.
        // Va encima del mosaico, sin cambiar su alto: al volver la portada, sus controles están justo
        // debajo de la barra y no hay saltos.
        val controlsLost = model.bottomBar && activeSlot != null && run {
            val band = min(activeSlot.height / 2, with(density) { 150.dp.toPx() })
            val line = activeSlot.bottom - band / 2
            line < scroll.value || line > scroll.value + viewport
        }
        var barHeight by remember { mutableIntStateOf(0) }
        val scope = rememberCoroutineScope()
        val goToSong: () -> Unit = {
            activeSlot?.let { slot -> scope.launch { scroll.animateScrollTo((slot.top - gap).toInt().coerceAtLeast(0)) } }
        }

        // Al cambiar de canción, la que suena se pone a la vista.
        LaunchedEffect(activeKey, columns) {
            val slot = tiles.getOrNull(activeIndex)?.let { slots[it] } ?: return@LaunchedEffect
            if (slot.top < scroll.value || slot.bottom > scroll.value + viewport) {
                scroll.animateScrollTo((slot.top - gap).toInt().coerceAtLeast(0))
            }
        }

        // Al llevar una portada cerca del borde de arriba o de abajo, el mosaico se desplaza solo.
        LaunchedEffect(editor.dragged) {
            val zone = with(density) { 56.dp.toPx() }
            val step = with(density) { 10.dp.toPx() }
            while (editor.dragged != null) {
                val y = editor.dragPoint.y - scroll.value
                val wanted = if (y < zone) -step else if (y > viewport - zone) step else 0f
                if (wanted != 0f) {
                    val moved = scroll.scrollBy(wanted)
                    if (moved != 0f) editor.drag(editor.dragPoint + Offset(0f, moved))
                }
                delay(16)
            }
        }

        Box(Modifier.fillMaxSize().verticalScroll(scroll, enabled = editor.resized == null)) {
            Box(
                Modifier
                    .fillMaxWidth()
                    // Sitio al final para no tapar la última (con la barra de abajo, también lo que ella tapa).
                    .height(with(density) { height.toDp() + if (controlsLost) barHeight.toDp() else 0.dp } + 96.dp)
                    .pointerInput(editing) {
                        if (!editing) return@pointerInput
                        awaitEachGesture {
                            val down = awaitFirstDown(requireUnconsumed = false)
                            val tile = editor.tileAt(down.position) ?: return@awaitEachGesture
                            if (editor.onGrip(tile, down.position)) {
                                down.consume()
                                editor.beginResize(tile)
                                drag(down.id) { change ->
                                    editor.resize(change.position)
                                    change.consume()
                                }
                                editor.end()
                                return@awaitEachGesture
                            }
                            // Manteniéndola pulsada se coge; si el dedo se mueve antes, se desplaza el mosaico.
                            val press = awaitLongPressOrCancellation(down.id) ?: return@awaitEachGesture
                            editor.beginDrag(tile, press.position)
                            drag(press.id) { change ->
                                editor.drag(change.position)
                                change.consume()
                            }
                            editor.end()
                        }
                    },
            ) {
                tiles.forEachIndexed { i, tile ->
                    key(tile.key) {
                        val slot = slots.getValue(tile)
                        // Solo se cargan las portadas visibles (y una pantalla por encima y por debajo).
                        val near = slot.bottom >= scroll.value - viewport && slot.top <= scroll.value + 2 * viewport
                        PlacedTile(
                            vm, model, editor, tile, slot,
                            active = i == activeIndex,
                            showCover = near,
                            // El mosaico se forma desde la canción que suena hacia fuera.
                            enterDelay = if (activeIndex < 0) i * 25 else abs(i - activeIndex) * 30,
                        )
                    }
                }
            }
        }

        if (model.bottomBar) AnimatedVisibility(
            visible = controlsLost,
            enter = slideInVertically { it } + fadeIn(),
            exit = slideOutVertically { it } + fadeOut(),
            modifier = Modifier.align(Alignment.BottomCenter),
        ) {
            BottomBar(vm, Modifier.onSizeChanged { barHeight = it.height }, onGoToSong = goToSong)
        }
    }
}

@Composable
private fun PlacedTile(
    vm: LauncheritoViewModel,
    model: MosaicModel,
    editor: MosaicEditor,
    tile: Tile,
    slot: Rect,
    active: Boolean,
    showCover: Boolean,
    enterDelay: Int,
) {
    val density = LocalDensity.current
    val dragging = editor.dragged == tile
    val position = remember { Animatable(slot.topLeft, Offset.VectorConverter) }
    val size = remember { Animatable(slot.width) }
    val appear = remember { Animatable(0f) }

    LaunchedEffect(Unit) {
        delay(enterDelay.coerceAtMost(700).toLong())
        appear.animateTo(1f, tween(380, easing = FastOutSlowInEasing))
    }
    LaunchedEffect(slot.topLeft, dragging) {
        if (dragging) {
            // La portada sigue al dedo; al soltarla, vuela hasta su sitio.
            snapshotFlow { editor.dragPoint - editor.grab }.collect { position.snapTo(it) }
        } else {
            position.animateTo(slot.topLeft, tween(420, easing = FastOutSlowInEasing))
        }
    }
    LaunchedEffect(slot.width) {
        size.animateTo(slot.width, tween(420, easing = FastOutSlowInEasing))
    }

    Box(
        Modifier
            .offset { IntOffset(position.value.x.roundToInt(), position.value.y.roundToInt()) }
            .size(with(density) { size.value.toDp() })
            .zIndex(if (dragging) 10f else if (active) 1f else 0f)
            .graphicsLayer {
                val grow = if (dragging) 1.06f else 1f
                scaleX = (0.85f + 0.15f * appear.value) * grow
                scaleY = scaleX
                alpha = appear.value
            },
    ) {
        MosaicTileContent(vm, model, tile, active, showCover)
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun MosaicTileContent(vm: LauncheritoViewModel, model: MosaicModel, tile: Tile, active: Boolean, showCover: Boolean) {
    val shape = RoundedCornerShape(TileRadius)
    val editing = model.isEditing()
    val title = vm.meta[tile.key]?.title ?: SongTags.fallbackTitle(tile.key)
    Box(
        Modifier
            .fillMaxSize()
            .clip(shape)
            .background(EmptyFill)
            .then(if (active) Modifier.border(2.dp, Accent, shape) else Modifier)
            // En modo edición pulsar no hace nada: se mantiene pulsada para moverla. Fuera de él,
            // mantenerla pulsada abre el menú de Favoritos y playlists.
            .then(
                if (!active && !editing) Modifier.combinedClickable(
                    onClick = { model.onSelect(tile.key) },
                    onLongClick = { model.onLongPress(tile.key) },
                ) else Modifier
            ),
    ) {
        Icon(
            Icons.Rounded.MusicNote, contentDescription = null, tint = PlaceholderColor,
            modifier = Modifier.align(Alignment.Center).size(44.dp),
        )
        if (showCover) {
            AsyncImage(
                model = SongCover(tile.key),
                contentDescription = title,
                contentScale = ContentScale.Crop,
                modifier = Modifier.fillMaxSize(),
            )
        }

        // Marca de procedencia: morada con una nota si es tu .mp3, verde con las ondas si es de Spotify,
        // roja con un aro y el triángulo si es de YouTube Music.
        Box(Modifier.align(Alignment.TopStart).padding(8.dp)) {
            when (tile.source) {
                OnlineSource.Spotify -> SpotifyBadge(22.dp)
                OnlineSource.YouTubeMusic -> YouTubeMusicBadge(22.dp)
                null -> OwnBadge(22.dp)
            }
        }
        // Las de Spotify suenan con un vídeo de YouTube: se avisa con la marca roja de la derecha.
        // Las de YouTube Music ya lo dicen con su propia marca.
        if (tile.source == OnlineSource.Spotify) {
            YouTubeBadge(Modifier.align(Alignment.TopEnd).padding(8.dp))
        }

        val shade = Brush.verticalGradient(listOf(Color.Transparent, Color(0xE6000000)))
        if (active) {
            Column(
                Modifier.align(Alignment.BottomCenter).fillMaxWidth().background(shade)
                    .padding(start = 14.dp, end = 14.dp, top = 40.dp, bottom = 6.dp),
            ) {
                Text(vm.title, fontSize = 18.sp, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text(vm.artist, fontSize = 13.sp, color = Muted, maxLines = 1, overflow = TextOverflow.Ellipsis)
                PlayerControls(vm)
            }
        } else {
            // En la pantalla táctil no hay "pasar el ratón": el título se ve siempre.
            Text(
                title,
                fontSize = 12.sp, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis,
                modifier = Modifier.align(Alignment.BottomStart).fillMaxWidth().background(shade)
                    .padding(start = 8.dp, end = if (editing) 34.dp else 8.dp, top = 18.dp, bottom = 6.dp),
            )
        }
        if (editing) ResizeGrip(Modifier.align(Alignment.BottomEnd).padding(4.dp))
    }
}

/** Dos rayas en diagonal sobre un cuadrado oscuro: arrastrándolo cambia el tamaño. */
@Composable
private fun ResizeGrip(modifier: Modifier) {
    Canvas(
        modifier.size(26.dp).clip(RoundedCornerShape(7.dp)).background(Color(0xB0000000)),
    ) {
        val k = size.width / 24f
        val stroke = 2.dp.toPx()
        drawLine(Color.White, Offset(17 * k, 7 * k), Offset(7 * k, 17 * k), stroke, StrokeCap.Round)
        drawLine(Color.White, Offset(17 * k, 12 * k), Offset(12 * k, 17 * k), stroke, StrokeCap.Round)
    }
}

/** Tu música: un .mp3 tuyo, que suena entero. */
@Composable
fun OwnBadge(size: Dp) {
    Box(Modifier.size(size).clip(CircleShape).background(Accent), contentAlignment = Alignment.Center) {
        Icon(Icons.Rounded.MusicNote, contentDescription = "Tu .mp3", tint = Color.White, modifier = Modifier.size(size * 0.6f))
    }
}

/** De Spotify: el círculo verde con las tres ondas negras del logo. */
@Composable
fun SpotifyBadge(size: Dp) {
    Canvas(Modifier.size(size).clip(CircleShape).background(SpotifyGreen)) {
        val k = this.size.width / 24f
        val path = Path().apply {
            moveTo(6.5f * k, 9.6f * k); quadraticTo(12f * k, 7.6f * k, 17.5f * k, 10.6f * k)
            moveTo(7.3f * k, 12.7f * k); quadraticTo(12f * k, 11.1f * k, 16.6f * k, 13.5f * k)
            moveTo(8.1f * k, 15.6f * k); quadraticTo(12f * k, 14.4f * k, 15.6f * k, 16.3f * k)
        }
        drawPath(path, Color.Black, style = Stroke(width = 1.8f * k, cap = StrokeCap.Round))
    }
}

/** De YouTube Music: el círculo rojo con un aro blanco y el triángulo de reproducir. */
@Composable
fun YouTubeMusicBadge(size: Dp) {
    Canvas(Modifier.size(size).clip(CircleShape).background(YouTubeRed)) {
        val k = this.size.width / 24f
        drawCircle(Color.White, radius = 5.5f * k, center = Offset(12f * k, 12f * k), style = Stroke(width = 1.4f * k))
        val play = Path().apply {
            moveTo(10.3f * k, 9.4f * k)
            lineTo(14.8f * k, 12f * k)
            lineTo(10.3f * k, 14.6f * k)
            close()
        }
        drawPath(play, Color.White)
    }
}

/** Marca roja con el triángulo de reproducir: la canción suena con su vídeo de YouTube. */
@Composable
fun YouTubeBadge(modifier: Modifier = Modifier) {
    Box(
        modifier.size(width = 30.dp, height = 21.dp).clip(RoundedCornerShape(6.dp)).background(YouTubeRed),
        contentAlignment = Alignment.Center,
    ) {
        Icon(Icons.Rounded.PlayArrow, contentDescription = "Vídeo de YouTube", tint = Color.White, modifier = Modifier.size(15.dp))
    }
}
