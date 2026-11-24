package io.github.sergio120602.launcherito.ui

import android.app.Application
import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import androidx.media3.common.MediaItem
import androidx.media3.common.MediaMetadata
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import io.github.sergio120602.launcherito.data.AppUpdate
import io.github.sergio120602.launcherito.data.AppUpdater
import io.github.sergio120602.launcherito.data.CatalogUnavailableException
import io.github.sergio120602.launcherito.data.UpdateException
import io.github.sergio120602.launcherito.data.GenreInfo
import io.github.sergio120602.launcherito.data.ListImportException
import io.github.sergio120602.launcherito.data.MosaicArrangement
import io.github.sergio120602.launcherito.data.MusicCatalog
import io.github.sergio120602.launcherito.data.OnlineSongs
import io.github.sergio120602.launcherito.data.OnlineSource
import io.github.sergio120602.launcherito.data.Playlist
import io.github.sergio120602.launcherito.data.SavedSong
import io.github.sergio120602.launcherito.data.SongLibrary
import io.github.sergio120602.launcherito.data.SongTags
import io.github.sergio120602.launcherito.data.OnlineTrack
import io.github.sergio120602.launcherito.data.TrendingList
import io.github.sergio120602.launcherito.data.TrendingPlaylists
import io.github.sergio120602.launcherito.data.TrendingWorker
import io.github.sergio120602.launcherito.data.UserPlaylist
import io.github.sergio120602.launcherito.data.UserPlaylists
import io.github.sergio120602.launcherito.data.YouTubeMusicLibrary
import io.github.sergio120602.launcherito.data.YouTubeLinks
import io.github.sergio120602.launcherito.player.PlayerHolder
import io.github.sergio120602.launcherito.player.YouTubeVideo
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.text.Collator
import java.io.File
import java.text.Normalizer
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import kotlin.math.abs
import kotlin.math.max

enum class Section { Songs, Artists, Genres, Playlists, Trending }

/** Dónde va el vídeo de la canción: a la vista en la vista original, u oculto (solo se oye). */
enum class VideoHost { Single, Hidden }

const val UNCLASSIFIED = "Sin clasificar"

data class SongMeta(
    val title: String,
    val artist: String,
    val genre: GenreInfo? = null,
    val classified: Boolean = false,
) {
    val genreName: String get() = genre?.name ?: UNCLASSIFIED
}

/** Una portada del mosaico. [baseSpan]: celdas de lado elegidas por el usuario o automáticas. */
class Tile(val key: String) {
    var baseSpan by mutableIntStateOf(1)
    val source: OnlineSource? = OnlineSongs.sourceOf(key)   // null: un .mp3
}

/** Tarjeta de un artista o de un género. [pictureArtist]: artista cuya foto se usa si no hay otra. */
data class Category(val name: String, val count: Int, val picture: String?, val pictureArtist: String?)

data class SearchResult(val key: String, val title: String, val artist: String, val highlight: IntRange?)

data class DeleteRow(val key: String, val title: String, val artist: String, val source: OnlineSource?, val missing: Boolean)

/**
 * Toda la lógica de la pantalla (lo que en Windows hace MainWindow.xaml.cs). Los campos con
 * mutableStateOf son observados por Compose: al cambiarlos, la pantalla se redibuja sola.
 */
class LauncheritoViewModel(app: Application) : AndroidViewModel(app) {
    private val context get() = getApplication<Application>()
    private val player = PlayerHolder.get(app)
    private val playlist = Playlist(SongTags::fallbackTitle)   // el aleatorio empieza activado
    private val library = SongLibrary(app)
    private val catalog = MusicCatalog(app)
    private val arrangement = MosaicArrangement(app)
    private val userLists = UserPlaylists(app)
    private val collator = Collator.getInstance().apply { strength = Collator.SECONDARY }
    private val settings = app.getSharedPreferences("ajustes", Context.MODE_PRIVATE)

    /**
     * Letra grande o pequeña. Se recuerda; la primera vez se elige según la letra que tenga el móvil
     * (grande si la tiene aumentada).
     */
    var largeText by mutableStateOf(
        settings.getBoolean(LARGE_TEXT_KEY, app.resources.configuration.fontScale > 1.15f)
    )
        private set

    fun chooseLargeText(large: Boolean) {
        largeText = large
        settings.edit().putBoolean(LARGE_TEXT_KEY, large).apply()
    }

    // ─────────────────────────────── Estado de la pantalla ───────────────────────────────

    /** false: menú principal. true: reproductor. */
    var playerVisible by mutableStateOf(false)
        private set
    var section by mutableStateOf(Section.Songs)
        private set
    var mosaicView by mutableStateOf(false)
        private set
    var isEditing by mutableStateOf(false)
        private set

    /** Las portadas en el orden del mosaico. */
    val tiles = mutableStateListOf<Tile>()
    private val tileByKey = HashMap<String, Tile>()

    /** El mosaico del reproductor: la que suena lleva los controles; mantener pulsada una abre su menú. */
    val mainMosaic = MosaicModel(
        tiles = tiles,
        isEditing = { isEditing },
        activeKey = { currentKey },
        onSelect = ::selectSong,
        onLongPress = { songMenuKey = it },
        onMove = ::moveTileBefore,
        onRemember = ::rememberArrangement,
        bottomBar = true,
    )

    var currentKey by mutableStateOf<String?>(null)
        private set
    var isPlaying by mutableStateOf(false)
        private set
    var title by mutableStateOf("")
        private set
    var artist by mutableStateOf("")
        private set
    var shuffle by mutableStateOf(playlist.shuffle)
        private set
    var positionText by mutableStateOf("")
        private set
    var position by mutableFloatStateOf(0f)     // segundos
        private set
    var duration by mutableFloatStateOf(0f)
        private set
    var isSeeking by mutableStateOf(false)
        private set
    var seekValue by mutableFloatStateOf(0f)
        private set

    var statusText by mutableStateOf("")        // avisos de las listas y del vídeo
        private set
    var statusSource by mutableStateOf<OnlineSource?>(null)   // de qué servicio es el aviso (para su color)
        private set
    private var statusClear: Job? = null
    var catalogStatus by mutableStateOf("")     // progreso de la clasificación con Deezer
        private set

    var libraryCount by mutableIntStateOf(library.count)
        private set
    var unloadedCount by mutableIntStateOf(0)
        private set

    /** Mensaje que se muestra en un cuadro de diálogo (null: ninguno). */
    var message by mutableStateOf<String?>(null)
    var messageTitle by mutableStateOf("Launcherito")
        private set

    // Artistas y géneros
    val meta = mutableStateMapOf<String, SongMeta>()
    val artistPictures = mutableStateMapOf<String, String?>()
    private val picturesRequested = HashSet<String>()
    var openCategory by mutableStateOf<String?>(null)
        private set
    private val pending = ArrayDeque<String>()   // canciones por clasificar con Deezer
    private var classifying = false

    private val localSongs = HashMap<String, String>()   // clave artista|título → .mp3 cargado

    // Lista de Spotify o YouTube Music: el cuadro está abierto si listDialogSource no es null (el
    // servicio con cuyo botón se ha abierto; el enlace puede ser de cualquiera de los dos).
    var listDialogSource by mutableStateOf<OnlineSource?>(null)
        private set
    var listLink by mutableStateOf("")
    var listDialogStatus by mutableStateOf("")
        private set
    var listBusy by mutableStateOf(false)
        private set

    // Borrar canciones
    var deleteRows by mutableStateOf<List<DeleteRow>?>(null)
        private set
    val deleteChecked = mutableStateMapOf<String, Boolean>()
    var confirmDelete by mutableStateOf(false)

    // Buscador
    var searchQuery by mutableStateOf("")

    // Vídeo de YouTube de la canción que suena, manejado con los controles de la app: a la vista en la
    // vista original y, para las de Spotify y YouTube Music, oculto en las demás. Mientras audioFromVideo es true el
    // sonido sale del vídeo y el .mp3 está cerrado.
    var songVideo by mutableStateOf<YouTubeVideo?>(null)
        private set
    var videoHost by mutableStateOf<VideoHost?>(null)
        private set
    var videoShown by mutableStateOf(false)      // en la vista original se ve el vídeo (no la carátula)
        private set
    var videoStatus by mutableStateOf<String?>(null)
        private set
    private var videoFailedFor: String? = null   // .mp3 sin vídeo: suena el archivo y no se reintenta
    private var skippedInARow = 0                // canciones de Spotify o YouTube Music sin vídeo saltadas seguidas
    private var songVideoPlaying = false
    private var songVideoStarted = false         // ya se ha cargado la página (luego solo se cambian los vídeos)
    private var songVideoVersion = 0             // invalida búsquedas de vídeo de una canción que ya no suena
    private var audioFromVideo = false
    private var videoTime = 0.0
    private var videoDuration = 0.0

    private var metadataVersion = 0              // invalida lecturas de etiquetas de una canción que ya no suena

    private val playerListener = object : Player.Listener {
        override fun onPlaybackStateChanged(state: Int) {
            if (audioFromVideo) return
            when (state) {
                Player.STATE_READY -> {
                    skippedInARow = 0
                    duration = max(1f, player.duration / 1000f)
                }
                Player.STATE_ENDED -> songEnded()
            }
        }

        // La notificación, los auriculares o una llamada también pausan y reanudan.
        override fun onPlayWhenReadyChanged(playWhenReady: Boolean, reason: Int) {
            if (!audioFromVideo && player.mediaItemCount > 0) isPlaying = playWhenReady
        }

        override fun onPlayerError(error: PlaybackException) = mediaFailed(error)
    }

    init {
        player.addListener(playerListener)
        PlayerHolder.onNext = ::next
        PlayerHolder.onPrevious = ::previous
        updateLibraryButtons()
        // Barra de progreso del .mp3 (la del vídeo la mueve el propio vídeo).
        viewModelScope.launch {
            while (true) {
                if (isPlaying && !audioFromVideo && !isSeeking) position = player.currentPosition / 1000f
                delay(250)
            }
        }
    }

    // ─────────────────────────────── Cargar canciones ───────────────────────────────

    /**
     * Añade los .mp3 elegidos en el selector o abiertos con «Abrir con». [persist]: se guarda el
     * permiso para leerlos también al volver a abrir la app (solo con el selector de archivos).
     */
    fun addFiles(uris: List<Uri>, persist: Boolean = true) {
        val valid = mutableListOf<String>()
        var rejected = 0
        for (uri in uris) {
            val name = SongTags.queryName(context, uri)
            val type = context.contentResolver.getType(uri)
            if (type != "audio/mpeg" && type != "audio/mp3") {
                rejected++
                continue
            }
            if (persist) {
                try {
                    context.contentResolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION)
                } catch (_: SecurityException) {
                    // Sin permiso duradero: suena ahora, pero puede no estar al volver a abrir la app.
                }
            }
            val key = uri.toString()
            if (name != null) SongTags.rememberName(key, name)
            valid.add(key)
        }
        if (rejected > 0) showMessage("Solo se admiten archivos .mp3. Se han ignorado $rejected archivo(s).")
        if (valid.isEmpty()) return

        // Se guardan en «Tus canciones»; el título y el artista se apuntan al leer sus etiquetas.
        library.add(valid.map { SavedSong(it, SongTags.fallbackTitle(it)) })
        addToPlaylist(valid)
    }

    /**
     * Añade canciones (.mp3, de Spotify o de YouTube Music) a la lista y al mosaico. Devuelve cuántas son
     * nuevas. Si la lista estaba vacía empieza a sonar [startWith] (o la primera).
     */
    private fun addToPlaylist(songs: List<String>, startWith: String? = null): Int {
        // Al añadir otras canciones mientras suena una playlist, deja de ser solo esa playlist.
        if (!loadingSession && (sessionListId != null || sessionTrendingId != null)) leaveSession()
        val before = playlist.count
        val wasEmpty = before == 0
        val added = playlist.add(songs)
        syncMosaic()
        classify(songs)

        // Al pasar de una canción a varias se cambia sola a la vista mosaico.
        if (before <= 1 && playlist.count > 1 && !mosaicView) showMosaic(true)

        // Si ya estaba sonando algo, las nuevas canciones se añaden a la lista sin interrumpirla.
        val first = if (startWith != null) playlist.jumpTo(startWith) else playlist.current
        if (wasEmpty && first != null) playSong(first) else updatePosition()
        showPlayer()   // también si se han añadido desde el menú principal
        return added
    }

    // ─────────────────────── Listas de Spotify y YouTube Music ───────────────────────

    /**
     * Abre el cuadro del enlace con el título y el color de [source]. Si se pasa un enlace
     * (compartido desde la app de Spotify o de YouTube Music), ya aparece pegado y manda el suyo.
     */
    fun openListDialog(source: OnlineSource, link: String? = null) {
        val shared = link?.let { OnlineSongs.parseLink(it) }
        if (shared != null) listLink = link.trim()
        listDialogStatus = ""
        listDialogSource = shared?.source ?: source
    }

    /**
     * Lo compartido desde otra app: se abre el cuadro del enlace, el de su servicio si es el enlace
     * de una lista (ya pegado) o, si no se reconoce, el de Spotify vacío.
     */
    fun openSharedText(text: String) {
        openListDialog(OnlineSongs.parseLink(text)?.source ?: OnlineSource.Spotify, text)
    }

    fun closeListDialog() {
        if (!listBusy) listDialogSource = null   // mientras se lee la lista no se cierra
    }

    /**
     * Lee la lista de Spotify o YouTube Music y añade sus canciones al mosaico. Las que ya están en
     * un .mp3 cargado no se repiten (suena el .mp3 entero); las demás suenan con su vídeo de YouTube.
     */
    fun addList() {
        if (listBusy) return
        val link = OnlineSongs.parseLink(listLink)
        if (link == null) {
            listDialogStatus = "Ese enlace no es de una lista ni de un álbum de Spotify o YouTube Music."
            return
        }
        viewModelScope.launch {
            listBusy = true
            listDialogStatus = "Leyendo la lista en ${OnlineSongs.name(link.source)}…"
            val list = try {
                OnlineSongs.load(link)
            } catch (e: ListImportException) {
                listDialogStatus = e.message ?: ""
                return@launch
            } finally {
                listBusy = false
            }

            val keys = mutableListOf<String>()
            val saved = mutableListOf<SavedSong>()
            var owned = 0
            for (track in list.tracks) {
                if (OnlineSongs.matchKey(track.artists, track.title) in localSongs) owned++
                else {
                    keys.add(track.key)
                    saved.add(SavedSong.fromTrack(track))
                }
            }
            library.add(saved)
            val added = addToPlaylist(keys)

            val summary = mutableListOf("«${list.name}»: ${songs(added)} añadida${if (added == 1) "" else "s"}")
            if (owned > 0) summary.add("$owned ya la${if (owned == 1) "" else "s"} tenías en .mp3")
            if (keys.size > added) summary.add("${keys.size - added} ya estaba${if (keys.size - added == 1) "" else "n"}")
            val text = if (list.tracks.isEmpty()) "«${list.name}» no tiene canciones." else summary.joinToString("  ·  ")

            // Si no se ha añadido nada el cuadro sigue abierto con la explicación; si no, se cierra.
            if (added == 0) {
                listDialogStatus = text
                return@launch
            }
            listDialogSource = null
            listLink = ""
            showStatus(text, list.source)
        }
    }

    /**
     * Quita las canciones de Spotify o YouTube Music que ya están en un .mp3 cargado: suena el
     * archivo, que está entero. La que está sonando se deja hasta que se cambie de canción.
     */
    private fun removeOnlineDuplicates() {
        val current = playlist.current
        val duplicates = playlist.songList.filter { key ->
            val track = OnlineSongs.tryGet(key)
            key != current && track != null && OnlineSongs.matchKey(track.artists, track.title) in localSongs
        }
        if (playlist.remove(duplicates) == 0) return
        // En Favoritos y en las playlists pasa a estar el .mp3, en el mismo sitio.
        for (key in duplicates) {
            val track = OnlineSongs.tryGet(key) ?: continue
            localSongs[OnlineSongs.matchKey(track.artists, track.title)]?.let { userLists.replace(key, it) }
        }
        listsChanged()
        duplicates.forEach { meta.remove(it) }
        library.remove(duplicates)   // tampoco hace falta guardarla: ya está guardado el .mp3
        syncMosaic()
        updatePosition()
    }

    /** Aviso durante unos segundos; [source] decide su color (rojo YouTube Music, verde Spotify, gris el resto). */
    private fun showStatus(text: String, source: OnlineSource? = null) {
        statusText = text
        statusSource = source
        statusClear?.cancel()
        statusClear = viewModelScope.launch {
            delay(12_000)
            statusText = ""
        }
    }

    private fun showMessage(text: String, title: String = "Launcherito") {
        messageTitle = title
        message = text
    }

    // ─────────────────────── Tus canciones y Borrar canciones ───────────────────────

    private fun updateLibraryButtons() {
        libraryCount = library.count
        unloadedCount = unloadedSongs().size
    }

    /**
     * Comprueba si la app puede leer cada .mp3: tiene el permiso guardado o ya está cargado. Pide al
     * sistema la lista de permisos una sola vez, no una por canción.
     */
    private fun availability(): (String) -> Boolean {
        val permitted = context.contentResolver.persistedUriPermissions
            .filter { it.isReadPermission }.mapTo(HashSet()) { it.uri.toString() }
        return { key -> OnlineSongs.isOnline(key) || key in tileByKey || key in permitted }
    }

    /** Guardadas que no están en el mosaico y se pueden cargar. */
    private fun unloadedSongs(): List<SavedSong> {
        val available = availability()
        return library.all.filter { it.key !in tileByKey && available(it.key) }
    }

    /** Carga todas las canciones guardadas que aún no están en el mosaico. */
    fun loadLibrary() {
        val songs = unloadedSongs().map { song ->
            if (song.isOnline) OnlineSongs.register(song.toTrack()) else SongTags.rememberName(song.key, song.title)
            song.key
        }
        if (songs.isNotEmpty()) addToPlaylist(songs)
        showPlayer()   // aunque ya estuvieran todas cargadas

        val available = availability()
        val missing = library.all.count { !available(it.key) }
        if (missing > 0) {
            showMessage(
                (if (missing == 1) "1 canción guardada ya no se puede abrir" else "$missing canciones guardadas ya no se pueden abrir") +
                    " (¿se ha movido o borrado el archivo?). Puedes quitarlas con «Borrar canciones»."
            )
        }
    }

    fun openDeleteDialog() {
        viewModelScope.launch {
            val songs = library.all
            // Comprobar si cada .mp3 sigue ahí obliga a abrirlo: fuera del hilo principal.
            val rows = withContext(Dispatchers.IO) {
                songs.map { song ->
                    DeleteRow(song.key, song.title, song.artist, song.source, !song.isOnline && !canOpen(song.key))
                }
            }
            deleteChecked.clear()
            deleteRows = rows
        }
    }

    private fun canOpen(key: String): Boolean = try {
        context.contentResolver.openFileDescriptor(Uri.parse(key), "r")?.close()
        true
    } catch (_: Exception) {
        false
    }

    fun closeDeleteDialog() {
        deleteRows = null
        deleteChecked.clear()
        confirmDelete = false
    }

    fun toggleSelectAll() {
        val rows = deleteRows ?: return
        val select = rows.any { deleteChecked[it.key] != true }
        rows.forEach { deleteChecked[it.key] = select }
    }

    val markedForDelete: List<String> get() = deleteChecked.filterValues { it }.keys.toList()

    val deleteQuestion: String
        get() {
            val count = markedForDelete.size
            return (if (count == library.count) "¿Borrar todas tus canciones guardadas?" else "¿Borrar ${songs(count)} de «Tus canciones»?") +
                "\nLos archivos .mp3 del móvil no se borran."
        }

    fun deleteMarked() {
        val keys = markedForDelete
        if (keys.isEmpty()) return
        library.remove(keys)
        userLists.forget(keys)   // tampoco siguen en Favoritos ni en las playlists
        listsChanged()
        // Ya no hace falta poder leerlos.
        for (key in keys.filter { !OnlineSongs.isOnline(it) }) {
            try {
                context.contentResolver.releasePersistableUriPermission(Uri.parse(key), Intent.FLAG_GRANT_READ_URI_PERMISSION)
            } catch (_: SecurityException) {
            }
        }
        closeDeleteDialog()
        removeFromSession(keys)
        updateLibraryButtons()
    }

    /**
     * Quita del mosaico y de la lista las canciones borradas. Si sonaba una de ellas, pasa a la
     * siguiente que quede; si no queda ninguna, se vuelve al menú principal.
     */
    private fun removeFromSession(keys: List<String>) {
        val gone = keys.toHashSet()
        val currentGone = playlist.current?.let { it in gone } == true
        val wasPlaying = isPlaying
        if (currentGone) stop()

        gone.forEach { meta.remove(it) }
        localSongs.entries.removeAll { it.value in gone }
        playlist.remove(gone)              // todas menos la que suena…
        if (currentGone) playlist.removeCurrent()   // …que se quita aparte
        syncMosaic()
        updatePosition()

        if (playlist.count == 0) {
            disposeSongVideo()
            setSongTexts("", "")
            currentKey = null
            playerVisible = false
        } else if (currentGone) {
            playlist.current?.let { next ->
                playSong(next)
                if (!wasPlaying) pause()
            }
        }
    }

    // ─────────────────────────────── Reproducción ───────────────────────────────

    /** Pulsar una canción: suena (las de Spotify y YouTube Music, con su vídeo de YouTube). */
    fun selectSong(key: String) {
        playlist.jumpTo(key)?.let(::playSong)
    }

    private fun playSong(key: String) {
        stop()
        currentKey = key
        showMetadata(key)
        updatePosition()
        position = 0f
        duration = 0f
        playerVisible = true

        // Suena el vídeo de YouTube en la vista original y siempre en las de Spotify y YouTube Music; si no, el .mp3.
        videoFailedFor = null
        val host = wantedVideoHost(key)
        if (host != null) startSongVideo(key, 0.0, host) else openMp3(key, 0.0)
        play()
    }

    private fun openMp3(key: String, startSeconds: Double) {
        val info = meta[key]
        val item = MediaItem.Builder()
            .setUri(key)
            .setMediaId(key)
            .setMediaMetadata(
                MediaMetadata.Builder()
                    .setTitle(info?.title ?: SongTags.fallbackTitle(key))
                    .setArtist(info?.artist)
                    .build()
            )
            .build()
        player.setMediaItem(item, (startSeconds * 1000).toLong())
        player.prepare()
    }

    fun previous() {
        playlist.previous()?.let(::playSong)
    }

    fun next() {
        playlist.next()?.let(::playSong)
    }

    fun toggleShuffle() {
        playlist.shuffle = !playlist.shuffle
        shuffle = playlist.shuffle
        updatePosition()
    }

    private fun updatePosition() {
        positionText = if (playlist.count == 0) "" else "${playlist.position + 1} / ${playlist.count}"
    }

    fun togglePlay() = if (isPlaying) pause() else play()

    private fun play() {
        if (audioFromVideo) songVideo?.play() else player.play()
        isPlaying = true
    }

    private fun pause() {
        if (audioFromVideo) songVideo?.pause() else player.pause()
        isPlaying = false
    }

    private fun stop() {
        player.stop()
        player.clearMediaItems()
        // El vídeo se queda (se reutiliza para la siguiente canción), pero callado.
        songVideoVersion++
        audioFromVideo = false
        songVideo?.pause()
        isPlaying = false
    }

    private fun mediaFailed(error: PlaybackException) {
        // Libera el archivo; si no, el reproductor se quedaría con el error puesto.
        stop()
        showMessage("No se pudo reproducir «$title» y se ha quitado de la lista.\n${error.localizedMessage ?: ""}")

        playlist.current?.let { failed ->
            meta.remove(failed)
            localSongs.entries.removeAll { it.value == failed }
        }
        val next = playlist.removeCurrent()
        syncMosaic()
        if (next != null) playSong(next)
        else {
            currentKey = null
            playerVisible = false
        }
    }

    /** Ha terminado la canción (el .mp3 o su vídeo). */
    private fun songEnded() {
        // Con varias canciones pasa a la siguiente (al acabar la lista vuelve a empezar).
        if (playlist.count > 1) {
            playlist.next()?.let {
                playSong(it)
                return
            }
        }
        pause()
        if (audioFromVideo) {
            songVideo?.seek(0.0)
            videoTime = 0.0
        } else {
            player.seekTo(0)
        }
        position = 0f
    }

    /** El dedo mueve la barra: el reproductor solo salta al soltarla. */
    fun seekDrag(seconds: Float) {
        isSeeking = true
        seekValue = seconds
    }

    fun seekEnd() {
        if (!isSeeking) return
        isSeeking = false
        if (audioFromVideo) {
            songVideo?.seek(seekValue.toDouble())
            videoTime = seekValue.toDouble()
        } else {
            player.seekTo((seekValue * 1000).toLong())
        }
        position = seekValue
    }

    /**
     * Pone al momento el título y el artista que ya se conocen y luego lee las etiquetas fuera del
     * hilo principal, para que la pantalla no se congele al cambiar de canción.
     */
    private fun showMetadata(key: String) {
        val version = ++metadataVersion
        val known = meta[key]
        if (known != null) setSongTexts(known.title, known.artist) else setSongTexts(SongTags.fallbackTitle(key), "")
        viewModelScope.launch {
            val info = withContext(Dispatchers.IO) { SongTags.read(context, key) }
            if (version != metadataVersion) return@launch   // mientras se leía ya se ha pasado a otra canción
            setSongTexts(info.title, info.artist)
        }
    }

    private fun setSongTexts(title: String, artist: String) {
        this.title = title
        this.artist = artist
    }

    // ─────────────────────── Vídeo de YouTube de la canción que suena ───────────────────────

    private enum class VideoProblem { Offline, NotEmbeddable, NoWebView }

    /**
     * Dónde va el vídeo de la canción: a la vista en la vista original; oculto si es de Spotify o
     * YouTube Music; en ningún sitio si es un .mp3 fuera de la vista original (suena el archivo).
     */
    private fun wantedVideoHost(key: String): VideoHost? = when {
        singleViewVisible -> VideoHost.Single
        OnlineSongs.isOnline(key) -> VideoHost.Hidden
        else -> null
    }

    /**
     * Pone el vídeo de YouTube de la canción en [host], empezando en el segundo [start]. El sonido
     * sale del vídeo y lo manejan los controles de la app. Mientras se busca se ve la carátula.
     */
    private fun startSongVideo(key: String, start: Double, host: VideoHost) {
        // El navegador se reutiliza de una canción a otra (y se cambia de sitio si hace falta).
        moveSongVideo(host)
        val version = ++songVideoVersion
        audioFromVideo = true
        songVideoPlaying = false
        videoTime = start
        videoDuration = 0.0
        showSongVideo(false)
        videoStatus = if (host == VideoHost.Single) "Buscando el vídeo en YouTube…" else null

        var video = songVideo
        if (video == null) {
            video = try {
                createSongVideo()
            } catch (_: Exception) {
                videoFailed(VideoProblem.NoWebView)   // el móvil no tiene WebView
                return
            }
            songVideo = video
            videoHost = host
            songVideoStarted = false
        }
        if (songVideoStarted) video.hold()   // el vídeo anterior no vuelve a sonar mientras se busca el nuevo
        viewModelScope.launch { loadSongVideo(key, start, version) }
    }

    private fun createSongVideo(): YouTubeVideo {
        val video = YouTubeVideo(context)
        video.onPlaying = {
            if (songVideo === video && audioFromVideo) {
                skippedInARow = 0
                songVideoPlaying = true
                if (videoHost == VideoHost.Single) showSongVideo(true)
            }
        }
        video.onEnded = {
            if (songVideo === video && audioFromVideo) songEnded()
        }
        video.onProgress = { time, total ->
            if (songVideo === video && audioFromVideo) {
                videoTime = time
                if (!isSeeking) position = time.toFloat()
                if (total > 0 && abs(total - videoDuration) > 0.5) {
                    videoDuration = total
                    duration = max(1f, total.toFloat())
                }
            }
        }
        video.onUnavailable = {
            if (songVideo === video && audioFromVideo) videoFailed(VideoProblem.NotEmbeddable)
        }
        return video
    }

    private suspend fun loadSongVideo(key: String, start: Double, version: Int) {
        val track = OnlineSongs.tryGet(key)
        val known = meta[key]
        val (title, artist) = when {
            track != null -> track.title to track.mainArtist
            known != null -> known.title to known.artist
            else -> withContext(Dispatchers.IO) { SongTags.read(context, key) }.let { it.title to it.artist }
        }
        val ids = YouTubeLinks.findVideos(key, artist, title)
        val video = songVideo
        if (version != songVideoVersion || video == null) return   // mientras se buscaba se ha cambiado de canción o de vista
        if (ids.isEmpty()) {
            videoFailed(VideoProblem.Offline)
            return
        }
        if (songVideoStarted) {
            video.load(ids, start)
        } else {
            songVideoStarted = true
            video.start(ids, start)
        }
    }

    /**
     * La canción no tiene vídeo que se pueda ver. Si es un .mp3, suena el archivo. Si es de Spotify o
     * YouTube Music no hay nada más que poner: sin conexión se para; si es solo esa, se pasa a la siguiente.
     */
    private fun videoFailed(problem: VideoProblem) {
        val key = playlist.current ?: return
        val source = OnlineSongs.sourceOf(key)
        if (source == null) {
            videoFailedFor = key   // no se vuelve a intentar al cambiar de vista
            switchToMp3(
                when (problem) {
                    VideoProblem.Offline -> "Sin conexión con YouTube: suena el .mp3"
                    VideoProblem.NoWebView -> "Este móvil no puede mostrar vídeos: suena el .mp3"
                    VideoProblem.NotEmbeddable -> "Esta canción no tiene vídeo que se pueda ver aquí: suena el .mp3"
                }
            )
            if (problem == VideoProblem.NoWebView) disposeSongVideo()
            return
        }

        videoStatus = null
        val title = SongTags.fallbackTitle(key)
        if (problem != VideoProblem.NotEmbeddable) {
            val name = OnlineSongs.name(source)
            showStatus(
                if (problem == VideoProblem.Offline) "Sin conexión con YouTube: las canciones de $name no pueden sonar"
                else "Este móvil no puede mostrar vídeos: las canciones de $name no pueden sonar",
                source,
            )
            pause()
            return
        }
        // Se salta, pero sin dar vueltas a la lista si ninguna tiene vídeo.
        val next = if (++skippedInARow < playlist.count) playlist.next() else null
        if (next != null) {
            showStatus("«$title» no tiene vídeo que se pueda ver aquí: pasa a la siguiente", source)
            playSong(next)
        } else {
            skippedInARow = 0
            showStatus("«$title» no tiene vídeo que se pueda ver aquí", source)
            pause()
        }
    }

    /**
     * Al cambiar de vista el sonido pasa a donde toca (el vídeo a la vista, el vídeo oculto o el
     * .mp3) y sigue por el mismo segundo.
     */
    private fun updateSongSource() {
        val key = playlist.current ?: return
        val wanted = wantedVideoHost(key)
        if (if (audioFromVideo) videoHost == wanted else wanted == null || videoFailedFor == key) {
            if (wanted == null && !audioFromVideo) disposeSongVideo()   // suena el .mp3: el navegador no hace falta
            return
        }

        // De la vista original al hueco oculto o al revés: el mismo vídeo cambia de sitio sin cortarse.
        if (audioFromVideo && wanted != null) {
            moveSongVideo(wanted)
            showSongVideo(wanted == VideoHost.Single && songVideoPlaying)
            if (wanted == VideoHost.Single && !songVideoPlaying) videoStatus = "Buscando el vídeo en YouTube…"
            return
        }

        val at = if (audioFromVideo) videoTime else player.currentPosition / 1000.0
        val playing = isPlaying
        if (audioFromVideo) {
            audioFromVideo = false
            disposeSongVideo()
        } else {
            player.stop()
            player.clearMediaItems()
        }

        if (wanted == null) {
            openMp3(key, at)
            if (playing) player.play()
        } else {
            startSongVideo(key, at, wanted)
            if (playing) songVideo?.play() else songVideo?.pause()
        }
    }

    /** El sonido de un .mp3 vuelve al archivo, por donde iba el vídeo (el vídeo se queda parado). */
    private fun switchToMp3(reason: String?) {
        val key = playlist.current
        if (!audioFromVideo || key == null) return
        if (reason != null) showStatus(reason)
        songVideoVersion++
        audioFromVideo = false
        songVideo?.pause()
        showSongVideo(false)
        videoStatus = null

        openMp3(key, videoTime)
        if (isPlaying) player.play()
    }

    /** Muestra el vídeo (marco 16:9) o la carátula (marco cuadrado) en la vista original. */
    private fun showSongVideo(visible: Boolean) {
        videoShown = visible
        if (visible) videoStatus = null
    }

    /** Pasa el navegador del vídeo (si lo hay) a otro hueco sin cerrarlo. */
    private fun moveSongVideo(host: VideoHost) {
        if (songVideo == null || videoHost == host) return
        videoHost = host
        if (host != VideoHost.Single) {
            showSongVideo(false)
            videoStatus = null
        }
    }

    /** Libera el navegador del vídeo. Quien lo llama decide antes de dónde sale el sonido. */
    private fun disposeSongVideo() {
        val video = songVideo ?: return
        songVideoVersion++
        songVideo = null
        songVideoStarted = false
        videoHost = null
        video.dispose()
        showSongVideo(false)
        videoStatus = null
    }

    // ─────────────────────────── Menú principal y buscador ───────────────────────────

    /** Vuelve al menú principal sin parar la música; desde allí se vuelve con «Volver al reproductor». */
    fun goHome() {
        editMosaic(false)
        playlistEditing = false
        searchQuery = ""
        playerVisible = false
        updateLibraryButtons()
        // La vista original deja de verse: el vídeo pasa al hueco oculto o suena el .mp3.
        updateSongSource()
    }

    /** Muestra el reproductor si hay algo cargado. */
    fun showPlayer() {
        if (playlist.count == 0 || playerVisible) return
        playerVisible = true
        updateSongSource()
    }

    val hasSongs: Boolean get() = tiles.isNotEmpty()

    /**
     * Busca por el nombre de la canción (y por el artista) sin distinguir mayúsculas ni tildes, entre
     * las cargadas y las guardadas en «Tus canciones». Primero las que empiezan por lo escrito.
     */
    fun searchResults(): List<SearchResult> {
        val query = fold(searchQuery.trim())
        if (query.isEmpty()) return emptyList()
        val available = availability()
        return searchCandidates()
            .mapNotNull { (key, title, artist) ->
                val at = fold(title).indexOf(query)
                val inArtist = fold(artist).contains(query)
                if (at < 0 && !inArtist) null
                else Triple(SearchResult(key, title, artist, if (at >= 0) at until at + query.length else null), at, key)
            }
            .sortedWith(
                compareBy<Triple<SearchResult, Int, String>> { if (it.second == 0) 0 else if (it.second > 0) 1 else 2 }
                    .thenComparator { a, b -> collator.compare(a.first.title, b.first.title) }
            )
            .filter { available(it.third) }   // un .mp3 guardado que ya no se puede abrir no se puede poner
            .take(MAX_SEARCH_RESULTS)
            .map { it.first }
    }

    /** Las canciones del mosaico y, además, las guardadas que aún no están cargadas. */
    private fun searchCandidates(): List<Triple<String, String, String>> {
        val seen = HashSet<String>()
        val result = mutableListOf<Triple<String, String, String>>()
        for (tile in tiles) {
            seen.add(tile.key)
            val known = meta[tile.key]
            val track = OnlineSongs.tryGet(tile.key)
            result.add(
                when {
                    known != null -> Triple(tile.key, known.title, known.artist)
                    track != null -> Triple(tile.key, track.title, track.artists)
                    else -> Triple(tile.key, SongTags.fallbackTitle(tile.key), "")
                }
            )
        }
        for (song in library.all) {
            if (seen.add(song.key)) result.add(Triple(song.key, song.title, song.artist))
        }
        return result
    }

    /** Pone la canción elegida en el buscador; si era una guardada sin cargar, la carga antes. */
    fun pickSearchResult(key: String) {
        searchQuery = ""
        if (key !in tileByKey) {
            library[key]?.let { saved ->
                if (saved.isOnline) OnlineSongs.register(saved.toTrack()) else SongTags.rememberName(key, saved.title)
            }
            addToPlaylist(listOf(key))
            if (playlist.current == key && isPlaying) return   // era la primera de la lista y ya ha empezado a sonar
        }
        selectSong(key)
    }

    // ─────────────────────── Pestañas: artistas y géneros ───────────────────────

    fun showSection(section: Section) {
        this.section = section
        openCategory = null
        searchQuery = ""
        playlistEditing = false
        syncPlaylistMosaic()
        applyView()
    }

    /** Abre un artista, un género, una playlist o una playlist del momento (null: vuelve a las tarjetas). */
    fun openCategory(name: String?) {
        openCategory = name
        playlistEditing = false
        if (section == Section.Trending) name?.let { trending[it] }?.let(::registerTrending)
        syncPlaylistMosaic()
    }

    /** Lee título y artista de las canciones nuevas y las clasifica por género con Deezer. */
    private fun classify(keys: List<String>) {
        val fresh = keys.distinct().filter { it !in meta }
        if (fresh.isEmpty()) return
        viewModelScope.launch {
            // Solo etiquetas, sin carátula, y fuera del hilo principal.
            val tags = withContext(Dispatchers.IO) { fresh.map { it to SongTags.read(context, it) } }
            var newLocal = false
            for ((key, info) in tags) {
                val track = OnlineSongs.tryGet(key)
                if (track != null) {
                    // Sin la marca de YouTube y solo el primer artista, para agrupar y buscar en Deezer.
                    meta[key] = SongMeta(track.title, track.mainArtist)
                } else {
                    meta[key] = SongMeta(info.title, info.artist)
                    localSongs[OnlineSongs.matchKey(info.artist, info.title)] = key
                    library.describe(key, info.title, info.artist)
                    newLocal = true
                }
                pending.addLast(key)
            }
            library.save()
            if (newLocal) removeOnlineDuplicates()
            runClassification()
        }
    }

    private suspend fun runClassification() {
        if (classifying) return   // el bucle que ya está en marcha se encargará también de estas
        classifying = true
        var done = 0
        try {
            while (pending.isNotEmpty()) {
                catalogStatus = "Clasificando con Deezer… $done/${done + pending.size}"
                val key = pending.first()
                meta[key]?.let { info ->
                    val genre = catalog.getGenre(info.artist, info.title)
                    // Puede haberse borrado mientras se consultaba.
                    meta[key]?.let { meta[key] = it.copy(genre = genre, classified = true) }
                }
                pending.removeFirst()
                if (++done % 25 == 0) catalog.save()
            }
            catalogStatus = ""
        } catch (_: CatalogUnavailableException) {
            // Las que faltan se quedan en la cola y se reintentan al añadir más canciones.
            catalogStatus = "Sin conexión con Deezer: faltan ${pending.size} por clasificar"
        } finally {
            classifying = false
            catalog.save()
        }
    }

    /** Las tarjetas de la pestaña abierta: artistas por orden alfabético, géneros de más a menos canciones. */
    fun categories(): List<Category> {
        val all = meta.values.toList()
        return if (section == Section.Artists) {
            all.groupBy { it.artist.lowercase() }.values
                .map { group -> Category(group[0].artist, group.size, artistPictures[group[0].artist], group[0].artist) }
                .sortedWith { a, b -> collator.compare(a.name, b.name) }
        } else {
            all.filter { it.classified }.groupBy { it.genreName.lowercase() }.values
                .map { group ->
                    // Deezer no tiene foto para los subgéneros ("Pop latino", "Flamenco"): se usa la del
                    // artista con más canciones de ese género.
                    val picture = group[0].genre?.picture
                    val topArtist = if (picture != null || group[0].genreName == UNCLASSIFIED) null
                    else group.groupingBy { it.artist }.eachCount().maxBy { it.value }.key
                    Category(group[0].genreName, group.size, picture ?: topArtist?.let { artistPictures[it] }, topArtist)
                }
                .sortedWith(
                    compareBy<Category> { it.name == UNCLASSIFIED }.thenByDescending { it.count }.thenBy { it.name }
                )
        }
    }

    /** Pide a Deezer la foto de un artista la primera vez que se va a ver su tarjeta. */
    fun requestArtistPicture(artist: String?) {
        if (artist == null || artist == SongTags.UNKNOWN_ARTIST || !picturesRequested.add(artist)) return
        viewModelScope.launch { artistPictures[artist] = catalog.getArtistPicture(artist) }
    }

    /** Canciones de un artista o de un género, por título. */
    fun categorySongs(name: String): List<Pair<String, SongMeta>> {
        val artists = section == Section.Artists
        return meta.entries
            .filter { (_, m) -> (if (artists) m.artist else m.genreName).equals(name, ignoreCase = true) && (artists || m.classified) }
            .map { it.key to it.value }
            .sortedWith { a, b -> collator.compare(a.second.title, b.second.title) }
    }

    // ───────────────────────────── Vista mosaico ─────────────────────────────

    fun toggleMosaicView() = showMosaic(!mosaicView)

    private fun showMosaic(mosaic: Boolean) {
        mosaicView = mosaic
        applyView()
    }

    /**
     * Entra o sale del modo edición: con él activo las portadas se mueven (manteniéndolas pulsadas) y
     * cambian de tamaño (con el tirador de su esquina).
     */
    fun editMosaic(editing: Boolean) {
        isEditing = editing
    }

    /** El mosaico solo se ve en la pestaña Canciones con la vista mosaico elegida. */
    val mosaicVisible: Boolean get() = section == Section.Songs && mosaicView

    /** La vista original (carátula grande) solo se ve en Canciones sin la vista mosaico. */
    val singleViewVisible: Boolean get() = playerVisible && section == Section.Songs && !mosaicView

    private fun applyView() {
        // Solo se edita el mosaico: al salir de él se deja la edición.
        if (!mosaicVisible && isEditing) editMosaic(false)
        // En la vista original suena el vídeo de la canción; fuera de ella, el .mp3 por donde iba
        // (las de Spotify y YouTube Music, su vídeo oculto).
        updateSongSource()
    }

    /**
     * Crea o quita portadas para que el mosaico coincida con la lista de reproducción: en orden
     * alfabético o en el que haya elegido el usuario arrastrándolas.
     */
    private fun syncMosaic() {
        val songs = arrangement.arrange(playlist.songList.toList())
        val present = songs.toHashSet()
        tileByKey.keys.retainAll(present)
        val ordered = songs.mapIndexed { i, key ->
            val tile = tileByKey.getOrPut(key) { Tile(key) }
            // Sin tamaño elegido, una de cada seis es grande.
            tile.baseSpan = arrangement.spanOf(key) ?: if (i % 6 == 0) 2 else 1
            tile
        }
        tiles.clear()
        tiles.addAll(ordered)
        updateLibraryButtons()
    }

    /** Se ha movido o cambiado de tamaño una portada: se guarda cómo ha quedado el mosaico. */
    fun rememberArrangement() {
        arrangement.remember(tiles.map { it.key to it.baseSpan })
    }

    /** Pone la portada [tile] en el sitio de [target]; las de en medio corren un puesto. */
    fun moveTileBefore(tile: Tile, target: Tile) {
        val to = tiles.indexOf(target)
        if (to < 0 || !tiles.remove(tile)) return
        tiles.add(to, tile)
    }

    // ─────────────────────────────── Favoritos y playlists ───────────────────────────────
    // Como en Windows: Favoritos (el corazón de los controles) y playlists propias, que se ven como
    // mosaico o como lista; mantener pulsada una canción abre el menú para ponerla o quitarla.

    /** Sube cada vez que cambian Favoritos o las playlists: lo que lo lee se redibuja. */
    var listsRevision by mutableIntStateOf(0)
        private set

    private fun listsChanged() {
        listsRevision++
    }

    fun userPlaylists(): List<UserPlaylist> {
        listsRevision
        return userLists.all.toList()
    }

    fun userPlaylist(id: String?): UserPlaylist? {
        listsRevision
        return id?.let { userLists[it] }
    }

    fun isFavorite(key: String?): Boolean {
        listsRevision
        return key != null && userLists.isFavorite(key)
    }

    /** Canción cuyo menú (Favoritos y playlists) está abierto, o null. */
    var songMenuKey by mutableStateOf<String?>(null)

    fun toggleFavorite(key: String) {
        keepInLibrary(key)
        val favorite = userLists.toggleFavorite(key)
        afterListChanged(UserPlaylists.FAVORITES_ID, listOf(key), favorite)
        val title = describeSong(key).first
        showStatus(if (favorite) "♥  «$title» está en Favoritos" else "«$title» ya no está en Favoritos")
    }

    /** Pone o quita una canción de una playlist. */
    fun setInList(id: String, key: String, add: Boolean) {
        val list = userLists[id] ?: return
        if (add) keepInLibrary(key)
        val changed = if (add) userLists.add(id, listOf(key)) else userLists.remove(id, listOf(key))
        if (changed == 0) return
        afterListChanged(id, listOf(key), add)
        val title = describeSong(key).first
        showStatus(if (add) "«$title» añadida a «${list.name}»" else "«$title» quitada de «${list.name}»")
    }

    /**
     * Tras poner o quitar canciones de una playlist. Si es la que se está escuchando, entran o salen
     * también del mosaico (la que suena sigue sonando).
     */
    private fun afterListChanged(id: String, keys: List<String>, added: Boolean) {
        val list = userLists[id]
        if (sessionListId == id && list != null) {
            if (added) {
                loadingSession = true
                try {
                    playlist.setCustomOrder(list.songs.toList())
                    addToPlaylist(prepareKeys(keys))
                } finally {
                    loadingSession = false
                }
            } else {
                val gone = keys.filter { it != playlist.current }
                if (playlist.remove(gone) > 0) {
                    forgetLoaded(gone)
                    syncMosaic()
                    updatePosition()
                }
            }
        }
        listsChanged()
        syncPlaylistMosaic()
    }

    /**
     * Una canción que no está en «Tus canciones» (de una playlist del momento) se guarda allí al
     * ponerla en Favoritos o en una playlist, para que siga disponible cuando la lista cambie.
     */
    private fun keepInLibrary(key: String) {
        val track = OnlineSongs.tryGet(key) ?: return
        if (key in library) return
        library.add(listOf(SavedSong.fromTrack(track)))
        updateLibraryButtons()
    }

    /**
     * Las que se pueden poner, en el mismo orden: las de Spotify y YouTube Music se recuperan de «Tus
     * canciones» y de los .mp3 hace falta poder abrirlos.
     */
    private fun prepareKeys(keys: List<String>): List<String> {
        val available = availability()
        return keys.filter { key ->
            val saved = library[key]
            when {
                key in tileByKey -> true
                OnlineSongs.isOnline(key) -> {
                    if (OnlineSongs.tryGet(key) == null) {
                        if (saved == null) return@filter false
                        OnlineSongs.register(saved.toTrack())
                    }
                    true
                }
                available(key) -> {
                    saved?.let { SongTags.rememberName(key, it.title) }
                    true
                }
                else -> false
            }
        }
    }

    /** Cuáles de [keys] se pueden poner (para atenuar las demás en el detalle de una playlist). */
    fun availableKeys(keys: List<String>): Set<String> = prepareKeys(keys).toHashSet()

    /** Título y artista que se conocen de una canción, esté cargada o no. */
    fun describeSong(key: String): Pair<String, String> {
        meta[key]?.let { return it.title to it.artist }
        library[key]?.takeIf { it.title.isNotEmpty() }?.let { return it.title to it.artist }
        OnlineSongs.tryGet(key)?.let { return it.title to it.artists }
        return SongTags.fallbackTitle(key) to ""
    }

    /** Olvida lo leído de canciones que salen del mosaico (la que suena, solo si se indica). */
    private fun forgetLoaded(keys: Collection<String>, keepCurrent: Boolean = true) {
        val gone = keys.toHashSet()
        if (keepCurrent) playlist.current?.let(gone::remove)
        gone.forEach { meta.remove(it) }
        localSongs.entries.removeAll { it.value in gone }
    }

    // ── Escuchar solo una playlist (propia o del momento) ──

    /** Playlist propia o del momento que se está escuchando (solo sus canciones), o null. */
    var sessionListId by mutableStateOf<String?>(null)
        private set
    var sessionTrendingId by mutableStateOf<String?>(null)
        private set
    private var loadingSession = false

    val sessionName: String? get() = userPlaylist(sessionListId)?.name ?: sessionTrendingId?.let { trending[it]?.name }

    /**
     * Deja en el mosaico y en la lista de reproducción solo [keys] y empieza a sonar [startWith] (o la
     * primera; con el aleatorio, una al azar). Sin aleatorio suenan en el orden de [order].
     */
    private fun loadSession(keys: List<String>, order: List<String>, startWith: String?) {
        editMosaic(false)
        stop()
        val keep = keys.toHashSet()
        forgetLoaded(playlist.songList.filter { it !in keep }, keepCurrent = false)
        playlist.clear()
        loadingSession = true
        try {
            playlist.setCustomOrder(order)
            addToPlaylist(keys, startWith)
        } finally {
            loadingSession = false
        }
    }

    /** Suena la playlist [id], desde [startWith]; con [goToSongs] se pasa al mosaico con sus canciones. */
    fun playUserList(id: String, startWith: String? = null, goToSongs: Boolean = false) {
        val list = userLists[id] ?: return
        val keys = prepareKeys(list.songs.toList())
        if (keys.isEmpty()) {
            showStatus(
                if (list.songs.isEmpty()) "«${list.name}» está vacía: añádele canciones primero"
                else "Ninguna canción de «${list.name}» se puede poner (¿se han movido los .mp3?)"
            )
            return
        }
        sessionTrendingId = null
        sessionListId = id
        loadSession(keys, list.songs.toList(), startWith)
        val missing = list.songs.size - keys.size
        showStatus("Escuchando «${list.name}»: ${songs(keys.size)}" + if (missing > 0) " ($missing ya no se pueden abrir)" else "")
        if (goToSongs) showSection(Section.Songs)
    }

    /** Pulsar una canción de una playlist: si ya suena esa playlist salta a ella; si no, la carga empezando por ella. */
    fun playFromList(id: String, key: String) {
        if (sessionListId == id && key in tileByKey) selectSong(key) else playUserList(id, key)
    }

    /** «Ver todas»: vuelven al mosaico todas las canciones guardadas, sin cortar la que suena. */
    fun showAllSongs() {
        leaveSession()
        loadLibrary()
    }

    private fun leaveSession() {
        sessionListId = null
        sessionTrendingId = null
        playlist.setCustomOrder(null)
    }

    // ── Cuadro del nombre (nueva playlist o renombrar) ──

    /** Cuadro abierto: [renamingId] null si es una playlist nueva; [songKey], la canción que entra en ella. */
    data class NameDialog(val renamingId: String?, val songKey: String?)

    var nameDialog by mutableStateOf<NameDialog?>(null)
        private set
    var nameText by mutableStateOf("")
    var nameError by mutableStateOf("")
        private set

    fun openNameDialog(renamingId: String? = null, songKey: String? = null) {
        songMenuKey = null
        nameText = renamingId?.let { userLists[it]?.name }.orEmpty()
        nameError = ""
        nameDialog = NameDialog(renamingId, songKey)
    }

    fun closeNameDialog() {
        nameDialog = null
    }

    fun saveNameDialog() {
        val dialog = nameDialog ?: return
        val name = nameText.trim()
        userLists.checkName(name, dialog.renamingId)?.let {
            nameError = it
            return
        }
        nameDialog = null
        if (dialog.renamingId != null) {
            userLists.rename(dialog.renamingId, name)
            listsChanged()
            showStatus("Playlist renombrada: «$name»")
            return
        }
        val list = userLists.create(name)
        dialog.songKey?.let { key ->
            keepInLibrary(key)
            userLists.add(list.id, listOf(key))
        }
        listsChanged()
        showStatus(dialog.songKey?.let { "«${describeSong(it).first}» añadida a la nueva playlist «$name»" } ?: "Playlist «$name» creada")
        if (section == Section.Playlists) openCategory(list.id)
        openPickDialog(list.id)   // una playlist nueva se llena al momento
    }

    /** Playlist que se va a borrar (se pregunta antes), o null. */
    var confirmDeleteList by mutableStateOf<String?>(null)

    fun deleteList(id: String) {
        confirmDeleteList = null
        val list = userLists[id] ?: return
        userLists.delete(id)
        if (sessionListId == id) leaveSession()   // lo que suena sigue sonando
        if (openCategory == id) openCategory(null)
        listsChanged()
        showStatus("Playlist «${list.name}» borrada")
    }

    // ── Panel Añadir canciones (con casillas y la carátula de cada una) ──

    var pickListId by mutableStateOf<String?>(null)
        private set
    var pickRows by mutableStateOf<List<SavedSong>>(emptyList())
        private set
    val pickChecked = mutableStateMapOf<String, Boolean>()
    var pickFilter by mutableStateOf("")

    fun openPickDialog(id: String) {
        val list = userLists[id] ?: return
        val available = availability()
        // Las que ya tiene no salen, ni los .mp3 que ya no se pueden abrir.
        val rows = library.all.filter { it.key !in list && available(it.key) }
        // Para sus carátulas, las de Spotify y YouTube Music tienen que estar registradas.
        rows.forEach { song -> if (song.isOnline && OnlineSongs.tryGet(song.key) == null) OnlineSongs.register(song.toTrack()) }
        pickRows = rows
        pickChecked.clear()
        pickFilter = ""
        pickListId = id
    }

    /** Las que deja el filtro (por título o artista, sin mayúsculas ni tildes). */
    fun visiblePickRows(): List<SavedSong> {
        val query = fold(pickFilter.trim())
        return if (query.isEmpty()) pickRows else pickRows.filter { fold(it.title).contains(query) || fold(it.artist).contains(query) }
    }

    fun togglePickVisible() {
        val visible = visiblePickRows()
        val select = visible.any { pickChecked[it.key] != true }
        visible.forEach { pickChecked[it.key] = select }
    }

    val pickedCount: Int get() = pickChecked.count { it.value }

    fun closePickDialog() {
        pickListId = null
        pickRows = emptyList()
        pickChecked.clear()
    }

    fun confirmPick() {
        val id = pickListId ?: return
        val list = userLists[id] ?: return
        val keys = pickRows.filter { pickChecked[it.key] == true }.map { it.key }
        closePickDialog()
        val added = userLists.add(id, keys)
        if (added == 0) return
        afterListChanged(id, keys, added = true)
        showStatus("${songs(added)} añadida${if (added == 1) "" else "s"} a «${list.name}»")
    }

    // ── Una playlist en vista mosaico (sus portadas se mueven y se agrandan con «Editar») ──

    val playlistTiles = mutableStateListOf<Tile>()
    private var playlistTilesId: String? = null
    var playlistEditing by mutableStateOf(false)
        private set

    fun editPlaylist(editing: Boolean) {
        playlistEditing = editing
    }

    val playlistMosaic = MosaicModel(
        tiles = playlistTiles,
        isEditing = { playlistEditing },
        activeKey = { null },
        onSelect = { key -> playlistTilesId?.let { playFromList(it, key) } },
        onLongPress = { songMenuKey = it },
        onMove = { tile, target ->
            val to = playlistTiles.indexOf(target)
            if (to >= 0 && playlistTiles.remove(tile)) playlistTiles.add(to, tile)
        },
        onRemember = ::rememberPlaylistLayout,
        bottomBar = false,
    )

    fun setListView(id: String, listView: Boolean) {
        userLists.setListView(id, listView)
        playlistEditing = false
        listsChanged()
        syncPlaylistMosaic()
    }

    /**
     * Pone en el mosaico de la playlist abierta sus portadas, en su orden y con sus tamaños. Si son las
     * mismas que ya hay no se toca nada (así no se deshace lo que se está colocando).
     */
    private fun syncPlaylistMosaic() {
        val list = openCategory?.let { userLists[it] }?.takeIf { section == Section.Playlists && !it.listView }
        if (list == null) {
            playlistTiles.clear()
            playlistTilesId = null
            playlistEditing = false
            return
        }
        val keys = prepareKeys(list.songs.toList())
        val current = playlistTiles.map { it.key }.toHashSet()
        if (playlistTilesId == list.id && keys.size == current.size && keys.all { it in current }) return
        val old = if (playlistTilesId == list.id) playlistTiles.associateBy { it.key } else emptyMap()
        playlistTilesId = list.id
        val tiles = keys.mapIndexed { i, key ->
            // Sin tamaño elegido, una de cada seis es grande (como en el mosaico de siempre).
            (old[key] ?: Tile(key)).also { it.baseSpan = list.spans[key] ?: if (i % 6 == 0) 2 else 1 }
        }
        playlistTiles.clear()
        playlistTiles.addAll(tiles)
    }

    /** Se ha movido o agrandado una portada: el orden del mosaico pasa a ser el de la playlist. */
    private fun rememberPlaylistLayout() {
        val id = playlistTilesId ?: return
        userLists.saveLayout(id, playlistTiles.map { it.key }, playlistTiles.associate { it.key to it.baseSpan })
        if (sessionListId == id) {
            userLists[id]?.let { playlist.setCustomOrder(it.songs.toList()) }   // si está sonando, vale ya
            updatePosition()
        }
        listsChanged()
    }

    /** Desde el menú principal se puede ir a las playlists o a las del momento aunque no haya nada cargado. */
    fun showSectionFromStart(section: Section) {
        playerVisible = true
        showSection(section)
    }

    // ─────────────────────────────── Buscar en YouTube ───────────────────────────────

    var youTubeOpen by mutableStateOf(false)
        private set
    var youTubeQuery by mutableStateOf("")
    var youTubeVideos by mutableStateOf(false)   // pestaña Vídeos (si no, Canciones)
        private set
    var youTubeResults by mutableStateOf<List<OnlineTrack>>(emptyList())
        private set
    var youTubeStatus by mutableStateOf("")
        private set
    var youTubeError by mutableStateOf(false)
        private set
    var youTubeBusy by mutableStateOf(false)
        private set
    private var youTubeVersion = 0   // descarta resultados de una búsqueda anterior que llegan tarde

    /** Abre el panel; con texto, busca (o lee el enlace) al momento. */
    fun openYouTube(query: String? = null) {
        searchQuery = ""
        youTubeOpen = true
        if (!query.isNullOrBlank()) {
            youTubeQuery = query.trim()
            searchYouTube()
        }
    }

    /** Se cierra; la búsqueda se queda para cuando se vuelva a abrir. */
    fun closeYouTube() {
        youTubeVersion++
        youTubeBusy = false
        youTubeOpen = false
    }

    fun setYouTubeTab(videos: Boolean) {
        if (youTubeVideos == videos) return
        youTubeVideos = videos
        if (youTubeQuery.isNotBlank() && YouTubeMusicLibrary.videoIdFromLink(youTubeQuery) == null) searchYouTube()
    }

    /**
     * Lo escrito puede ser el enlace de un vídeo (se lee ese vídeo), el de una lista o un álbum (se abre
     * el cuadro de listas) o el texto que se busca.
     */
    fun searchYouTube() {
        val text = youTubeQuery.trim()
        if (text.isEmpty()) return
        val version = ++youTubeVersion
        val videoId = YouTubeMusicLibrary.videoIdFromLink(text)
        if (videoId == null) {
            OnlineSongs.parseLink(text)?.let { link ->
                closeYouTube()
                openListDialog(link.source, text)
                addList()
                return
            }
        }
        youTubeBusy = true
        youTubeError = false
        youTubeStatus = if (videoId != null) "Leyendo el vídeo…" else "Buscando «$text» en YouTube…"
        viewModelScope.launch {
            val results = try {
                if (videoId != null) listOf(YouTubeMusicLibrary.loadVideo(videoId))
                else YouTubeMusicLibrary.search(text, youTubeVideos)
            } catch (e: ListImportException) {
                if (version == youTubeVersion) {
                    youTubeBusy = false
                    youTubeError = true
                    youTubeStatus = e.message ?: ""
                }
                return@launch
            }
            if (version != youTubeVersion) return@launch   // mientras tanto se ha buscado otra cosa o se ha cerrado
            youTubeBusy = false
            youTubeResults = results
            youTubeStatus = when {
                results.isEmpty() -> "YouTube no ha encontrado nada para «$text»."
                videoId != null -> "Vídeo del enlace:"
                youTubeVideos -> "Vídeos:"
                else -> "Canciones:"
            }
        }
    }

    /** El .mp3 cargado que es esta misma canción, si lo hay (entonces suena el archivo, que está entero). */
    fun ownedMp3(track: OnlineTrack): String? = localSongs[OnlineSongs.matchKey(track.artists, track.title)]

    fun isSaved(key: String): Boolean {
        libraryCount   // para redibujar el botón cuando se guarda
        return key in library
    }

    /** Guarda la canción en «Tus canciones» y la pone en el mosaico; con [play], además suena. */
    fun addFromYouTube(track: OnlineTrack, play: Boolean) {
        ownedMp3(track)?.let { mp3 ->
            if (play) selectSong(mp3)
            return
        }
        val key = track.key
        val isNew = key !in library
        OnlineSongs.register(track)
        library.add(listOf(SavedSong.fromTrack(track)))
        updateLibraryButtons()
        if (key !in tileByKey) addToPlaylist(listOf(key), startWith = if (play) key else null)
        if (play && (playlist.current != key || !isPlaying)) selectSong(key)
        showStatus(
            if (isNew) "YouTube: +1 canción  ·  «${track.title}»" else "«${track.title}» ya estaba en tus canciones",
            OnlineSource.YouTubeMusic,
        )
        youTubeError = false
        youTubeStatus = if (play) "Suena «${track.title}»" else "«${track.title}» se ha guardado en «Tus canciones»"
    }

    // ─────────────────────────────── Del momento ───────────────────────────────
    // Las playlists más escuchadas de Spotify, renovadas cada día a las 7:00 (TrendingWorker). Se
    // escuchan con el vídeo de YouTube de cada canción, como las demás de Spotify.

    var trending by mutableStateOf(TrendingPlaylists.load(app))
        private set
    var trendingRefreshing by mutableStateOf(false)
        private set
    var trendingError by mutableStateOf<String?>(null)
        private set
    private val trendingFile = File(app.filesDir, "del-momento.json")
    private var trendingStamp = trendingFile.lastModified()

    init {
        // La actualización de las 7:00 queda programada aunque la app se cierre (si ya lo estaba, no se toca).
        TrendingWorker.schedule(app)
        // Al abrir la app y cada 5 minutos: si el trabajo de las 7:00 ha dejado unas nuevas se leen, y si
        // ya ha pasado la hora y aún no se han renovado (el móvil estaba sin conexión), se renuevan.
        viewModelScope.launch {
            while (true) {
                checkTrending()
                delay(5 * 60_000L)
            }
        }
    }

    private fun checkTrending() {
        val stamp = trendingFile.lastModified()
        if (stamp != trendingStamp && !trendingRefreshing) {
            trendingStamp = stamp
            trending = TrendingPlaylists.load(context)
        }
        if (trending.isStale()) refreshTrending(manual = false)
    }

    fun refreshTrending(manual: Boolean = true) {
        if (trendingRefreshing) return
        trendingRefreshing = true
        viewModelScope.launch {
            try {
                trending = TrendingPlaylists.refresh(context, trending)
                trendingStamp = trendingFile.lastModified()
                trendingError = null
                if (manual) showStatus("Del momento: playlists actualizadas", OnlineSource.Spotify)
            } catch (e: ListImportException) {
                trendingError = e.message
                if (manual) showStatus(e.message ?: "", OnlineSource.Spotify)
            } finally {
                trendingRefreshing = false
            }
        }
    }

    /** La que suena: el .mp3 si ya se tiene esa canción (está entera), si no la de Spotify (con su vídeo). */
    fun trendingPlayKey(song: SavedSong): String = localSongs[OnlineSongs.matchKey(song.artist, song.title)] ?: song.key

    /** Da a conocer sus canciones (para sus portadas, sus vídeos y el menú de Favoritos y playlists). */
    private fun registerTrending(list: TrendingList) {
        list.tracks.forEach { if (OnlineSongs.tryGet(it.key) == null) OnlineSongs.register(it.toTrack()) }
    }

    /**
     * Suena la playlist del momento [id], desde [startWith]. No se guardan en «Tus canciones» (cambian
     * cada día); sí las que se pongan en Favoritos o en una playlist.
     */
    fun playTrending(id: String, startWith: String? = null, goToSongs: Boolean = false) {
        val list = trending[id] ?: return
        registerTrending(list)
        val keys = list.tracks.map(::trendingPlayKey).distinct()
        if (keys.isEmpty()) return
        sessionListId = null
        sessionTrendingId = id
        loadSession(keys, keys, startWith)
        showStatus("Escuchando «${list.name}»: ${songs(keys.size)}", OnlineSource.Spotify)
        if (goToSongs) showSection(Section.Songs)
    }

    fun playFromTrending(id: String, key: String) {
        if (sessionTrendingId == id && key in tileByKey) selectSong(key) else playTrending(id, key)
    }

    /** Copia la playlist tal como está hoy a «Tus playlists» (y sus canciones a «Tus canciones»). */
    fun saveTrending(id: String) {
        val list = trending[id] ?: return
        registerTrending(list)
        val date = SimpleDateFormat("d MMM", Locale.forLanguageTag("es-ES")).format(Date())
        val base = "${list.name} · $date"
        var name = base
        var n = 2
        while (userLists.checkName(name) != null && n < 100) name = "$base (${n++})"
        val keys = list.tracks.map(::trendingPlayKey).distinct()
        library.add(list.tracks.filter { it.key in keys })   // las que no son .mp3 tuyos
        val saved = userLists.create(name)
        userLists.add(saved.id, keys)
        updateLibraryButtons()
        listsChanged()
        showStatus("Guardada en tus playlists como «$name»")
    }

    /** «actualizadas hoy a las 7:00», «ayer a las 7:00» o la fecha. */
    fun trendingWhen(): String? {
        val updated = trending.updated ?: return null
        val spanish = Locale.forLanguageTag("es-ES")
        val hour = SimpleDateFormat("H:mm", spanish).format(Date(updated))
        val day = { t: Long -> SimpleDateFormat("yyyyMMdd", Locale.ROOT).format(Date(t)) }
        val now = System.currentTimeMillis()
        return when (day(updated)) {
            day(now) -> "hoy a las $hour"
            day(now - 24 * 3600_000L) -> "ayer a las $hour"
            else -> "el ${SimpleDateFormat("d 'de' MMMM", spanish).format(Date(updated))} a las $hour"
        }
    }

    // ─────────────────────────────── Actualizar ───────────────────────────────
    // Como en Windows: al abrir la app se mira sin molestar si hay una versión nueva en GitHub; si la
    // hay, sale «Actualizar a la X» (pantalla inicial y menú ⋮). Se descarga el .apk y se abre el
    // instalador de Android, que pide confirmación. La primera vez hay que permitir en Ajustes que
    // Launcherito instale apps.

    val appVersion: String = AppUpdater.currentVersion(app)
    var appUpdate by mutableStateOf<AppUpdate?>(null)        // versión nueva encontrada
        private set
    var updateStatus by mutableStateOf<String?>(null)        // «Buscando…», «Tienes la última versión», errores…
        private set
    var updateProgress by mutableStateOf<Float?>(null)       // de 0 a 1 mientras se descarga
        private set
    var updateConfirm by mutableStateOf(false)               // cuadro «¿Actualizar a la X?»
    private var updateBusy = false
    private var downloadedApk: File? = null                  // ya descargado, por si falta el permiso de instalar

    init {
        viewModelScope.launch { checkForUpdate(quiet = true) }
    }

    /** El botón: busca una versión nueva o, si ya la hay, pregunta si se instala. */
    fun updateButton() {
        if (updateBusy) return
        if (appUpdate != null) updateConfirm = true else viewModelScope.launch { checkForUpdate(quiet = false) }
    }

    private suspend fun checkForUpdate(quiet: Boolean) {
        updateBusy = true
        if (!quiet) updateStatus = "Buscando versiones nuevas…"
        try {
            appUpdate = AppUpdater.check(context)
            updateStatus = when {
                appUpdate != null -> null
                quiet -> updateStatus
                else -> "Tienes la última versión ($appVersion)."
            }
        } catch (e: UpdateException) {
            if (!quiet) updateStatus = e.message   // sin conexión: al abrir la app no se dice nada
        } finally {
            updateBusy = false
        }
    }

    /** «Sí» en el cuadro: descarga el .apk (si no estaba ya) y abre el instalador. */
    fun installUpdate() {
        updateConfirm = false
        val update = appUpdate ?: return
        if (updateBusy) return
        viewModelScope.launch {
            updateBusy = true
            try {
                val apk = downloadedApk?.takeIf { it.exists() } ?: run {
                    updateStatus = "Descargando la ${update.version}…"
                    updateProgress = 0f
                    AppUpdater.download(context, update) { p -> updateProgress = p }
                }
                downloadedApk = apk
                updateProgress = null
                if (!AppUpdater.sameSigner(context, apk)) {
                    // Android no la instalaría encima: dice «conflicto con un paquete existente».
                    updateStatus = "La ${update.version} está firmada con otra clave y Android no deja instalarla encima " +
                        "de esta. Hay que desinstalar Launcherito e instalarla desde GitHub (se pierden las canciones guardadas)."
                    showMessage(updateStatus!!, "No se puede actualizar desde aquí")
                    return@launch
                }
                if (AppUpdater.canInstall(context)) {
                    updateStatus = "Instalando la ${update.version}: confirma en el aviso de Android."
                    AppUpdater.install(context, apk)
                } else {
                    // Una sola vez: Android pide permitir que Launcherito instale apps.
                    updateStatus = "Permite «Instalar apps desconocidas» para Launcherito, vuelve y pulsa otra vez «Actualizar»."
                    AppUpdater.openInstallPermission(context)
                }
            } catch (e: UpdateException) {
                updateProgress = null
                updateStatus = e.message
            } finally {
                updateBusy = false
            }
        }
    }

    /** Si no se puede actualizar desde aquí: la página de la versión en GitHub. */
    fun openUpdatePage() {
        YouTubeLinks.open(context, appUpdate?.pageUrl ?: AppUpdater.RELEASES_PAGE)
    }

    // ─────────────────────────────────── Otros ───────────────────────────────────

    /** Guarda lo pendiente (la app puede cerrarse sin avisar estando en segundo plano). */
    fun saveAll() {
        catalog.save()
        library.save()
    }

    override fun onCleared() {
        player.removeListener(playerListener)
        PlayerHolder.onNext = null
        PlayerHolder.onPrevious = null
        player.stop()
        player.clearMediaItems()
        disposeSongVideo()
        saveAll()
    }

    companion object {
        private const val MAX_SEARCH_RESULTS = 50
        private const val LARGE_TEXT_KEY = "letraGrande"

        fun songs(count: Int) = if (count == 1) "1 canción" else "$count canciones"

        /** Texto sin mayúsculas ni tildes y con la misma longitud, para buscar y resaltar lo encontrado. */
        fun fold(text: String): String = buildString(text.length) {
            for (c in text) append(Normalizer.normalize(c.toString(), Normalizer.Form.NFD)[0].lowercaseChar())
        }
    }
}
