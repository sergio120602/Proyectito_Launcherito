package io.github.sergio120602.launcherito.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import android.net.Uri
import java.io.IOException

/**
 * Lee listas y álbumes de YouTube Music sin cuenta ni clave, con el mismo servicio que usa su web
 * (music.youtube.com/youtubei/v1/browse): da el título, los artistas, la duración, la portada y el
 * vídeo de cada canción, de 100 en 100. Cada canción suena con su propio vídeo de YouTube (no hace
 * falta buscarlo, como con las de Spotify). Las canciones leídas se guardan en [OnlineSongs].
 */
object YouTubeMusicLibrary {
    const val KEY_PREFIX = "ytmusic:"

    /** Como mucho se leen estas canciones de una lista (5 peticiones de 100). */
    const val MAX_TRACKS = 500

    /** Resultados que se muestran de una búsqueda (los de la primera página de YouTube Music). */
    const val MAX_SEARCH_RESULTS = 20

    private const val BROWSE_URL = "https://music.youtube.com/youtubei/v1/browse?prettyPrint=false"
    private const val SEARCH_URL = "https://music.youtube.com/youtubei/v1/search?prettyPrint=false"
    // Filtros de la búsqueda de YouTube Music: solo canciones o solo vídeos (los mismos que usa su web).
    private const val SONGS_FILTER = "EgWKAQIIAWoMEA4QChADEAQQCRAF"
    private const val VIDEOS_FILTER = "EgWKAQIQAWoMEA4QChADEAQQCRAF"
    // Versión del cliente web de YouTube Music con la que se presenta la petición. Si YouTube dejara
    // de aceptarla, basta con poner una más reciente (se ve en las peticiones de music.youtube.com).
    private const val CLIENT_VERSION = "1.20241106.01.00"

    // music.youtube.com/playlist?list=ID, www.youtube.com/watch?v=…&list=ID, youtu.be/…?list=ID
    private val listPattern = Regex("""(?:youtube\.com|youtu\.be)/\S*?[?&]list=(?<id>[A-Za-z0-9_-]+)""", RegexOption.IGNORE_CASE)
    // music.youtube.com/browse/MPREb_… (página de un álbum) o …/browse/VLPL… (página de una lista)
    private val browsePattern = Regex(
        """music\.youtube\.com/browse/(?<id>MPREb_[A-Za-z0-9_-]+|VL[A-Za-z0-9_-]+)""", RegexOption.IGNORE_CASE,
    )
    // "Artista - Canción" en el título de un vídeo
    private val dashPattern = Regex("""^(?<left>.+?)\s+[-–—]\s+(?<right>.+)$""")
    // "(Official Video)", "[Lyric Video]", "(Videoclip Oficial)"… en el título de un vídeo
    private val videoTagPattern = Regex(
        """\s*[(\[][^)\]]*\b(?:official|oficial|video|v[ií]deo|videoclip|audio|lyrics?|letra|visuali[sz]er|mv|hd|hq|4k)\b[^)\]]*[)\]]""",
        RegexOption.IGNORE_CASE,
    )
    // Tamaño en la dirección de una portada de Google ("=w120-h120-l90-rj")
    private val thumbSizePattern = Regex("""=w\d+-h\d+""")
    // Enlace de un vídeo: youtube.com/watch?v=ID (también music.youtube.com), youtu.be/ID, /shorts/ID, /embed/ID, /live/ID
    private val videoLinkPattern = Regex(
        """(?:youtube\.com/(?:watch\?(?:\S*?&)?v=|shorts/|embed/|live/)|youtu\.be/)(?<id>[A-Za-z0-9_-]{11})""",
        RegexOption.IGNORE_CASE,
    )
    // Duración escrita entre los datos de un resultado de búsqueda ("3:58", "1:02:03")
    private val durationPattern = Regex("""^\d{1,2}(?::\d{2}){1,2}$""")

    fun isYouTubeMusic(key: String) = key.startsWith(KEY_PREFIX)

    /** Id del vídeo de una canción de YouTube Music, o null si la clave es de otra cosa. */
    fun videoId(key: String): String? = if (isYouTubeMusic(key)) key.removePrefix(KEY_PREFIX) else null

    /** Reconoce el enlace de una lista ("playlist") o un álbum ("album") de YouTube Music, o null. */
    fun parseLink(text: String?): ListLink? {
        if (text == null) return null
        browsePattern.find(text)?.let { match ->
            val id = match.groups["id"]!!.value
            return if (id.startsWith("MPREb_")) ListLink(OnlineSource.YouTubeMusic, "album", id)
            else ListLink(OnlineSource.YouTubeMusic, "playlist", id.removePrefix("VL"))
        }
        val list = listPattern.find(text) ?: return null
        return ListLink(OnlineSource.YouTubeMusic, "playlist", list.groups["id"]!!.value)
    }

    /** Id del vídeo de un enlace de YouTube o YouTube Music, o null si no es el enlace de un vídeo. */
    fun videoIdFromLink(text: String?): String? = text?.let { videoLinkPattern.find(it)?.groups?.get("id")?.value }

    /**
     * Busca en YouTube Music: solo canciones (con la portada de su álbum) o solo vídeos (videoclips,
     * directos, versiones…). Las canciones encontradas no se registran hasta que se añaden.
     */
    @Throws(ListImportException::class)
    suspend fun search(query: String, videos: Boolean): List<OnlineTrack> = withContext(Dispatchers.IO) {
        val page = post(
            SEARCH_URL,
            mapOf("query" to query, "params" to if (videos) VIDEOS_FILTER else SONGS_FILTER),
            "No se ha podido buscar en YouTube. Inténtalo otra vez dentro de un rato.",
            "YouTube ha cambiado su forma de responder y no se puede leer la búsqueda.",
        )
        val seen = HashSet<String>()
        findAll(page, "musicResponsiveListItemRenderer").filterIsInstance<JSONObject>()
            .mapNotNull { readTrack(it)?.first }
            .filter { seen.add(it.id) }
            .take(MAX_SEARCH_RESULTS)
            .toList()
    }

    /**
     * Datos de un vídeo suelto a partir de su id (para un enlace pegado): título y artista sacados del
     * título del vídeo y de su canal, con el servicio público oEmbed de YouTube.
     */
    @Throws(ListImportException::class)
    suspend fun loadVideo(videoId: String): OnlineTrack = withContext(Dispatchers.IO) {
        val url = "https://www.youtube.com/oembed?format=json&url=" + Uri.encode("https://www.youtube.com/watch?v=$videoId")
        val json = try {
            JSONObject(Http.get(url, "User-Agent" to "Mozilla/5.0 (Linux; Android 14)"))
        } catch (_: HttpStatusException) {
            throw ListImportException("Ese vídeo no existe, es privado o su autor no deja verlo fuera de YouTube.")
        } catch (_: IOException) {
            throw ListImportException("No se puede conectar con YouTube. Comprueba la conexión a Internet.")
        } catch (_: JSONException) {
            throw ListImportException("YouTube ha cambiado su forma de responder y no se puede leer el vídeo.")
        }
        val title = json.optStringOrNull("title") ?: throw ListImportException("No se ha podido leer ese vídeo.")
        val channel = cleanChannel(json.optStringOrNull("author_name").orEmpty())
        // Como en los vídeos subidos por cualquiera: "Artista - Canción" manda sobre el nombre del canal.
        val (clean, artists) = cleanVideoTitle(title, channel.ifEmpty { SongTags.UNKNOWN_ARTIST }, uploadedByUser = true)
        OnlineTrack(OnlineSource.YouTubeMusic, videoId, clean, artists, 0)
    }

    /** Imagen pequeña para la lista de resultados: la portada del álbum o la del vídeo. */
    fun thumbnailUrl(track: OnlineTrack): String =
        track.coverUrl?.let { thumbSizePattern.replace(it, "=w120-h120") } ?: "https://i.ytimg.com/vi/${track.id}/mqdefault.jpg"

    /** Nombre del canal como artista: sin " - Topic" (canales automáticos) ni "VEVO". */
    private fun cleanChannel(channel: String): String {
        var name = channel.trim()
        if (name.endsWith(" - Topic", ignoreCase = true)) name = name.dropLast(" - Topic".length)
        if (name.endsWith("VEVO") && name.length > 4) name = name.dropLast(4)
        return name.trim()
    }

    /** Descarga la lista o el álbum (hasta [MAX_TRACKS] canciones). */
    @Throws(ListImportException::class)
    suspend fun load(type: String, linkId: String): OnlineList = withContext(Dispatchers.IO) {
        var id = linkId
        var name: String? = null
        if (type == "album") {
            // La página del álbum dice cuál es su lista de canciones (OLAK5uy_…), que se lee como cualquier otra.
            val notFound = "No se encuentra ese álbum en YouTube Music."
            val album = browse("browseId", id, notFound)
            name = headerTitle(album)
            id = findAll(album, "playlistId").filterIsInstance<String>().firstOrNull { it.startsWith("OLAK5uy_") }
                ?: throw ListImportException(notFound)
        }

        if (id == "LM" || id == "LL" || id == "WL") {
            throw ListImportException(
                "Esa lista es privada de tu cuenta («Me gusta» o «Ver más tarde») y Launcherito no entra en tu cuenta. " +
                    "Copia sus canciones a una lista pública o no listada y pega su enlace."
            )
        }
        if (id.startsWith("RD") && !id.startsWith("RDCLAK")) {
            throw ListImportException(
                "Los mix de YouTube Music cambian cada vez que se abren y no se pueden añadir. " +
                    "Guarda sus canciones en una lista y pega el enlace de esa lista."
            )
        }

        val notFound = "No se encuentra la lista. Comprueba el enlace y que la lista sea pública o no listada."
        val reader = TrackReader()
        val page = browse("browseId", "VL$id", notFound)
        if (name == null) name = headerTitle(page)
        val shelf = findAll(page, "musicPlaylistShelfRenderer").filterIsInstance<JSONObject>().firstOrNull()
        val contents = shelf?.optJSONArray("contents") ?: throw ListImportException(notFound)
        var token = reader.read(contents)
        // Las siguientes, de 100 en 100.
        while (token != null && reader.tracks.size < MAX_TRACKS) {
            val next = browse("continuation", token, notFound)
            val items = findAll(next, "continuationItems").filterIsInstance<JSONArray>().firstOrNull()
            token = items?.let { reader.read(it) }
        }

        // Un álbum leído por su lista (OLAK5uy_…) no trae cabecera: se usa el álbum de sus canciones.
        val listName = name ?: reader.albums.singleOrNull() ?: "Lista de YouTube Music"
        OnlineList(OnlineSource.YouTubeMusic, listName, reader.tracks)
    }

    /**
     * Portadas que se prueban, en orden: la cuadrada que da la lista (544 px) y, si es un vídeo, su
     * imagen grande o, si no la tiene, la mediana. El mosaico las recorta al cuadrado.
     */
    internal fun coverUrls(track: OnlineTrack): Sequence<String> = sequence {
        track.coverUrl?.let { yield(it) }
        yield("https://i.ytimg.com/vi/${track.id}/hq720.jpg")
        yield("https://i.ytimg.com/vi/${track.id}/mqdefault.jpg")
    }

    /** Va leyendo las canciones de las páginas de una lista, sin repetir vídeos. */
    private class TrackReader {
        private val seen = HashSet<String>()
        val tracks = mutableListOf<OnlineTrack>()
        val albums = LinkedHashSet<String>()

        /** Lee las canciones de una página y devuelve la marca para pedir la siguiente (null si no hay más). */
        fun read(items: JSONArray): String? {
            var token: String? = null
            for (i in 0 until items.length()) {
                val entry = items.optJSONObject(i) ?: continue
                val item = entry.optJSONObject("musicResponsiveListItemRenderer")
                val more = entry.optJSONObject("continuationItemRenderer")
                if (item != null) {
                    if (tracks.size >= MAX_TRACKS) continue
                    val (track, album) = readTrack(item) ?: continue
                    if (seen.add(track.id)) {
                        tracks.add(track)
                        album?.let { albums.add(it) }
                    }
                } else if (more != null) {
                    token = findAll(more, "token").filterIsInstance<String>().firstOrNull()
                }
            }
            return token
        }
    }

    /** Una fila de la lista y su álbum; null si no se puede escuchar (vídeo borrado o privado). */
    private fun readTrack(item: JSONObject): Pair<OnlineTrack, String?>? {
        val flex = item.optJSONArray("flexColumns") ?: return null
        val columns = (0 until flex.length()).map { runs(flex.opt(it)).toList() }
        if (columns.isEmpty()) return null

        val videoId = item.optJSONObject("playlistItemData")?.optStringOrNull("videoId")
            ?: columns[0].asSequence().flatMap { findAll(it, "videoId") }.filterIsInstance<String>().firstOrNull()
        var title = columns[0].joinToString("") { it.optString("text") }.trim()
        if (videoId == null || title.isEmpty()) return null

        // Los artistas son los textos que llevan a su página; los separadores (" & ", ", ") no.
        // Lo que va tras " • " (visualizaciones, año) no es del artista.
        val artistRuns = columns.getOrNull(1).orEmpty().takeWhile { it.optString("text") != " • " }
        val linked = artistRuns.filter { it.has("navigationEndpoint") }.map { it.optString("text") }
        var artists = if (linked.isNotEmpty()) linked.joinToString(", ")
        else artistRuns.joinToString("") { it.optString("text") }.trim()
        if (artists.isEmpty()) artists = SongTags.UNKNOWN_ARTIST

        val album = columns.getOrNull(2)?.joinToString("") { it.optString("text") }?.trim()?.takeIf { it.isNotEmpty() }

        // "Canción" de YouTube Music (con su portada cuadrada) o vídeo: videoclip oficial (OMV) o subido por alguien (UGC).
        val videoType = findAll(item, "musicVideoType").filterIsInstance<String>().firstOrNull() ?: ""
        if (videoType != "MUSIC_VIDEO_TYPE_ATV") {
            val clean = cleanVideoTitle(title, artists, uploadedByUser = videoType == "MUSIC_VIDEO_TYPE_UGC")
            title = clean.first
            artists = clean.second
        }

        val thumbnail = item.optJSONObject("thumbnail")?.let { thumb ->
            findAll(thumb, "url").filterIsInstance<String>().lastOrNull()
        }
        // La portada cuadrada de Google se pide a 544 px; la de un vídeo (i.ytimg.com) se busca con su id.
        val cover = thumbnail?.takeIf { !it.contains("ytimg.com", ignoreCase = true) }
            ?.let { thumbSizePattern.replace(it, "=w544-h544") }

        return OnlineTrack(OnlineSource.YouTubeMusic, videoId, title, artists, readDuration(item, columns), cover) to album
    }

    /**
     * Título de un vídeo como título de canción: "Artista - Canción (Official Video)" → "Canción".
     * En los subidos por cualquiera el canal no es el artista: se toma el de delante del guion.
     */
    private fun cleanVideoTitle(title: String, artists: String, uploadedByUser: Boolean): Pair<String, String> {
        var newTitle = title
        var newArtists = artists
        dashPattern.find(title)?.let { dash ->
            val left = dash.groups["left"]!!.value.trim()
            val mainArtist = OnlineSongs.simplify(artists.split(',')[0])
            if (uploadedByUser) {
                newArtists = left
                newTitle = dash.groups["right"]!!.value
            } else if (mainArtist.isNotEmpty() && OnlineSongs.simplify(left).contains(mainArtist)) {
                newTitle = dash.groups["right"]!!.value
            }
        }
        val clean = videoTagPattern.replace(newTitle, "").trim()
        return (clean.ifEmpty { newTitle.trim() }) to newArtists
    }

    /**
     * Duración en milisegundos ("3:04" o "1:02:03"): en una lista va en la columna de la derecha; en una
     * búsqueda, al final de la segunda columna ("Artista • Álbum • 3:04"). Cero si no viene.
     */
    private fun readDuration(item: JSONObject, columns: List<List<JSONObject>>): Long {
        val text = item.optJSONArray("fixedColumns")?.let { fixed -> runs(fixed).map { it.optString("text") }.firstOrNull { it.isNotEmpty() } }
            ?: columns.getOrNull(1)?.map { it.optString("text").trim() }?.lastOrNull { durationPattern.matches(it) }
            ?: return 0
        var seconds = 0L
        for (part in text.split(':')) {
            seconds = seconds * 60 + (part.toIntOrNull() ?: return 0)
        }
        return seconds * 1000
    }

    /** Título de la cabecera de la página (nombre de la lista o del álbum), o null. */
    private fun headerTitle(root: JSONObject): String? {
        for (header in listOf("musicResponsiveHeaderRenderer", "musicDetailHeaderRenderer", "musicImmersiveHeaderRenderer")) {
            for (found in findAll(root, header).filterIsInstance<JSONObject>()) {
                val text = runs(found.opt("title")).joinToString("") { it.optString("text") }.trim()
                if (text.isNotEmpty()) return text
            }
        }
        return null
    }

    /** Pide una página a YouTube Music. Bloquea. */
    private fun browse(field: String, value: String, notFound: String): JSONObject =
        post(BROWSE_URL, mapOf(field to value), notFound, "YouTube Music ha cambiado su forma de responder y no se puede leer la lista.")

    /** Petición al servicio de la web de YouTube Music (browse o search) con los datos de su cliente. Bloquea. */
    private fun post(url: String, fields: Map<String, String>, failed: String, unreadable: String): JSONObject {
        val body = JSONObject()
            .put("context", JSONObject().put("client", JSONObject()
                .put("clientName", "WEB_REMIX").put("clientVersion", CLIENT_VERSION).put("hl", "es").put("gl", "ES")))
        fields.forEach { (name, value) -> body.put(name, value) }
        val text = try {
            Http.postJson(
                url, body.toString(),
                "User-Agent" to "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                "Origin" to "https://music.youtube.com",
                "Referer" to "https://music.youtube.com/",
            )
        } catch (_: HttpStatusException) {
            throw ListImportException(failed)
        } catch (_: IOException) {
            throw ListImportException("No se puede conectar con YouTube Music. Comprueba la conexión a Internet.")
        }
        return try {
            JSONObject(text)
        } catch (_: JSONException) {
            throw ListImportException(unreadable)
        }
    }

    /** Partes ("runs") de un texto de YouTube Music: {"runs":[{"text":"…"},…]}, también dentro de una columna. */
    private fun runs(element: Any?): Sequence<JSONObject> =
        findAll(element, "runs").filterIsInstance<JSONArray>().take(1)
            .flatMap { runs -> (0 until runs.length()).asSequence().mapNotNull { runs.optJSONObject(it) } }

    /**
     * Todos los valores de la propiedad [name] a cualquier profundidad, en el orden del documento.
     * No entra dentro de los que encuentra. Así no depende de la ruta exacta, que YouTube cambia a menudo.
     */
    private fun findAll(element: Any?, name: String): Sequence<Any> = sequence {
        when (element) {
            is JSONObject -> for (key in element.keys()) {
                val value = element.opt(key) ?: continue
                if (key == name) yield(value) else yieldAll(findAll(value, name))
            }
            is JSONArray -> for (i in 0 until element.length()) yieldAll(findAll(element.opt(i), name))
        }
    }
}
