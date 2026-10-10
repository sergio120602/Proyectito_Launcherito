package io.github.sergio120602.launcherito.data

import android.content.Context
import android.net.Uri
import android.util.AtomicFile
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import org.json.JSONException
import org.json.JSONObject
import java.io.File
import java.io.IOException

/** Género asignado a una canción, con la foto que lo representa. */
data class GenreInfo(val name: String, val picture: String?)

/** Deezer no responde (sin conexión, caído o bloqueado). Lo ya consultado no se pierde. */
class CatalogUnavailableException : Exception("No se puede conectar con Deezer.")

/**
 * Clasifica canciones con la API pública de Deezer (no necesita clave): busca la canción, mira el
 * género de su álbum y obtiene fotos de artistas y géneros. Todo se guarda en una caché en disco,
 * así cada canción solo se consulta una vez aunque se cierre la app.
 *
 * Se usa desde el hilo principal: las cachés solo se tocan ahí y la red va en Dispatchers.IO.
 */
class MusicCatalog(context: Context) {
    private companion object {
        const val API = "https://api.deezer.com"
        // Deezer admite 50 peticiones cada 5 s; con 120 ms entre peticiones se queda por debajo.
        const val MIN_INTERVAL_MS = 120L
        const val POP_GENRE_ID = 132L
    }

    private val file = AtomicFile(File(context.filesDir, "catalogo.json"))
    private val gate = Mutex()
    private var lastRequest = 0L
    private var dirty = false

    private val tracks = HashMap<String, Long?>()            // "artista|título" → id de álbum
    private val albums = HashMap<Long, Long?>()              // id de álbum → id de género
    private val genres = HashMap<Long, GenreInfo>()          // id de género → nombre y foto
    private val artistPictures = HashMap<String, String?>()

    init {
        loadCache()
    }

    /** Busca la canción en Deezer y devuelve el género de su álbum, o null si no se encuentra. */
    @Throws(CatalogUnavailableException::class)
    suspend fun getGenre(artist: String, title: String): GenreInfo? {
        val key = "${normalize(artist)}|${normalize(title)}"
        val albumId = if (key in tracks) tracks[key] else searchAlbumId(artist, cleanTitle(title)).also {
            tracks[key] = it
            dirty = true
        }
        albumId ?: return null

        val genreId = if (albumId in albums) albums[albumId] else getAlbumGenreId(albumId).also {
            albums[albumId] = it
            dirty = true
        }
        return genreId?.let { getGenreInfo(it) }
    }

    /** URL de la foto de un artista, o null si Deezer no la conoce. */
    suspend fun getArtistPicture(artist: String): String? {
        val key = normalize(artist)
        if (key in artistPictures) return artistPictures[key]
        val json = getJson("$API/search/artist?limit=1&q=${Uri.encode(artist)}") ?: return null
        val picture = firstData(json)?.optStringOrNull("picture_big")
        artistPictures[key] = picture
        dirty = true
        return picture
    }

    /** Guarda la caché en disco si ha cambiado. */
    fun save() {
        if (!dirty) return
        val json = JSONObject().apply {
            put("Tracks", JSONObject().also { o -> tracks.forEach { (k, v) -> o.put(k, v ?: JSONObject.NULL) } })
            put("Albums", JSONObject().also { o -> albums.forEach { (k, v) -> o.put(k.toString(), v ?: JSONObject.NULL) } })
            put("Genres", JSONObject().also { o ->
                genres.forEach { (k, v) ->
                    o.put(k.toString(), JSONObject().put("Name", v.name).put("Picture", v.picture ?: JSONObject.NULL))
                }
            })
            put("ArtistPictures", JSONObject().also { o -> artistPictures.forEach { (k, v) -> o.put(k, v ?: JSONObject.NULL) } })
        }
        val stream = try {
            file.startWrite()
        } catch (_: IOException) {
            return   // si no se puede guardar, se volverá a consultar la próxima vez
        }
        try {
            stream.write(json.toString().toByteArray())
            file.finishWrite(stream)
            dirty = false
        } catch (_: IOException) {
            file.failWrite(stream)
        }
    }

    private suspend fun searchAlbumId(artist: String, title: String): Long? {
        // Primero una búsqueda exacta por artista y título; si no da nada, una búsqueda libre.
        for (query in listOf("artist:\"$artist\" track:\"$title\"", "$artist $title")) {
            val json = getJson("$API/search?limit=1&q=${Uri.encode(query)}") ?: throw CatalogUnavailableException()
            val album = firstData(json)?.optJSONObject("album")
            if (album != null && album.has("id")) return album.getLong("id")
        }
        return null
    }

    private suspend fun getAlbumGenreId(albumId: Long): Long? {
        val json = getJson("$API/album/$albumId") ?: throw CatalogUnavailableException()
        // Un álbum puede tener varios géneros ("Pop", "Pop latino", "Latino"); "Pop" es el que Deezer
        // pone a casi todo, así que se prefiere el primero que sea más concreto.
        var chosen: Long? = null
        val list = json.optJSONObject("genres")?.optJSONArray("data")
        if (list != null) {
            for (i in 0 until list.length()) {
                val id = list.getJSONObject(i).getLong("id")
                if (chosen == null) chosen = id
                if (id != POP_GENRE_ID) {
                    chosen = id
                    break
                }
            }
        }
        if (chosen == null && json.optLong("genre_id") > 0) chosen = json.getLong("genre_id")
        return chosen
    }

    private suspend fun getGenreInfo(genreId: Long): GenreInfo? {
        genres[genreId]?.let { return it }
        val json = getJson("$API/genre/$genreId") ?: throw CatalogUnavailableException()
        val name = json.optStringOrNull("name") ?: return null
        val info = GenreInfo(name, json.optStringOrNull("picture_big"))
        genres[genreId] = info
        dirty = true
        return info
    }

    /** Hace una petición respetando el límite de Deezer. Devuelve null si falla la red. */
    private suspend fun getJson(url: String): JSONObject? = gate.withLock {
        repeat(3) {
            val wait = lastRequest + MIN_INTERVAL_MS - System.currentTimeMillis()
            if (wait > 0) delay(wait)
            lastRequest = System.currentTimeMillis()

            val json = try {
                withContext(Dispatchers.IO) {
                    // Accept-Language: nombres de géneros en español.
                    JSONObject(Http.get(url, "Accept-Language" to "es", "User-Agent" to "Launcherito"))
                }
            } catch (_: IOException) {
                return@withLock null
            } catch (_: JSONException) {
                return@withLock null
            }

            // Si se supera el límite, Deezer responde {"error":{"code":4,...}}: se espera y se reintenta.
            if (json.optJSONObject("error")?.optInt("code") == 4) {
                delay(5_000)
            } else {
                return@withLock json
            }
        }
        null
    }

    private fun firstData(root: JSONObject): JSONObject? =
        root.optJSONArray("data")?.takeIf { it.length() > 0 }?.optJSONObject(0)

    private fun normalize(text: String) = text.trim().lowercase()

    private fun loadCache() {
        try {
            val json = JSONObject(String(file.readFully()))
            json.optJSONObject("Tracks")?.let { o -> o.keys().forEach { tracks[it] = o.optLongOrNull(it) } }
            json.optJSONObject("Albums")?.let { o -> o.keys().forEach { albums[it.toLong()] = o.optLongOrNull(it) } }
            json.optJSONObject("Genres")?.let { o ->
                o.keys().forEach { k ->
                    val g = o.getJSONObject(k)
                    genres[k.toLong()] = GenreInfo(g.optString("Name", "Desconocido"), g.optStringOrNull("Picture"))
                }
            }
            json.optJSONObject("ArtistPictures")?.let { o -> o.keys().forEach { artistPictures[it] = o.optStringOrNull(it) } }
        } catch (_: IOException) {
            // primera vez: aún no hay caché
        } catch (_: JSONException) {
            // caché dañada: se empieza de cero
        } catch (_: NumberFormatException) {
        }
    }
}

/** Quita añadidos como "- Remastered 2005" o "(feat. X)" que estorban en la búsqueda. */
fun cleanTitle(title: String): String {
    var text = title
    val dash = text.indexOf(" - ")
    if (dash > 0) text = text.substring(0, dash)
    val paren = text.indexOfAny(charArrayOf('(', '['))
    if (paren > 0) text = text.substring(0, paren)
    return text.trim()
}

/** Texto de la propiedad, o null si no existe o no es un texto (optString devolvería ""). */
internal fun JSONObject.optStringOrNull(name: String): String? = (opt(name) as? String)

internal fun JSONObject.optLongOrNull(name: String): Long? = (opt(name) as? Number)?.toLong()
