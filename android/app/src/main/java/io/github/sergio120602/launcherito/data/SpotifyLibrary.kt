package io.github.sergio120602.launcherito.data

import android.content.Context
import android.net.Uri
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONException
import org.json.JSONObject
import java.io.File
import java.io.IOException
import java.text.Normalizer
import java.util.concurrent.ConcurrentHashMap

/** Canción de una lista de Spotify. Dentro de Launcherito se identifica por [key]. */
data class SpotifyTrack(val id: String, val title: String, val artists: String, val durationMs: Long) {
    val key: String get() = SpotifyLibrary.KEY_PREFIX + id

    /** Primer artista ("Drake, Future" → "Drake"), el que se usa para agrupar y para Deezer. */
    val mainArtist: String get() = artists.split(',')[0].trim()
}

/** Lista o álbum de Spotify ya leído. */
data class SpotifyList(val name: String, val owner: String, val tracks: List<SpotifyTrack>)

/** No se ha podido leer una lista de Spotify; el mensaje se muestra tal cual. */
class SpotifyImportException(message: String) : Exception(message)

/**
 * Lee listas públicas y álbumes de Spotify sin cuenta ni clave. La API oficial exige desde febrero
 * de 2026 que el dueño de la app tenga Premium, así que se usa la página que Spotify ofrece para
 * insertar listas en otras webs (trae título y artistas de cada canción, hasta 100 canciones) y su
 * servicio oEmbed para las portadas. Spotify no da el audio completo: si no se tiene el .mp3, la
 * canción suena con su vídeo de YouTube ([YouTubeLinks]).
 */
object SpotifyLibrary {
    const val KEY_PREFIX = "spotify:track:"

    private val tracks = ConcurrentHashMap<String, SpotifyTrack>()

    // open.spotify.com/playlist/ID, open.spotify.com/intl-es/album/ID?si=…, spotify:playlist:ID
    private val linkPattern = Regex(
        """(?:open\.spotify\.com/(?:intl-[a-z-]+/)?|spotify:)(?<type>playlist|album)[/:](?<id>[A-Za-z0-9]{22})""",
        RegexOption.IGNORE_CASE,
    )
    private val dataPattern = Regex(
        """<script id="__NEXT_DATA__" type="application/json">(?<json>.*?)</script>""",
        RegexOption.DOT_MATCHES_ALL,
    )

    fun isSpotify(key: String) = key.startsWith(KEY_PREFIX)

    fun tryGet(key: String): SpotifyTrack? = tracks[key]

    /** Vuelve a dar a conocer una canción guardada («Tus canciones») sin pedirla a Spotify. */
    fun register(track: SpotifyTrack) {
        tracks[track.key] = track
    }

    /** Saca el tipo ("playlist" o "album") y el id de un enlace de Spotify, o null si no lo es. */
    fun parseLink(text: String?): Pair<String, String>? {
        val match = text?.let { linkPattern.find(it) } ?: return null
        return match.groups["type"]!!.value.lowercase() to match.groups["id"]!!.value
    }

    /** Descarga la lista o el álbum y registra sus canciones. */
    @Throws(SpotifyImportException::class)
    suspend fun load(type: String, id: String): SpotifyList = withContext(Dispatchers.IO) {
        val html = try {
            Http.get("https://open.spotify.com/embed/$type/$id", "User-Agent" to "Launcherito")
        } catch (_: IOException) {
            throw SpotifyImportException("No se puede conectar con Spotify. Comprueba la conexión a Internet.")
        }

        val data = dataPattern.find(html)
            ?: throw SpotifyImportException("Spotify ha cambiado su página y no se puede leer la lista.")

        try {
            val notFound = SpotifyImportException(
                if (type == "album") "No se encuentra ese álbum en Spotify."
                else "No se encuentra la lista. Comprueba el enlace y que la lista sea pública."
            )
            val entity = JSONObject(data.groups["json"]!!.value)
                .optJSONObject("props")?.optJSONObject("pageProps")?.optJSONObject("state")
                ?.optJSONObject("data")?.optJSONObject("entity") ?: throw notFound
            val list = entity.optJSONArray("trackList") ?: throw notFound

            val result = mutableListOf<SpotifyTrack>()
            for (i in 0 until list.length()) {
                val item = list.optJSONObject(i) ?: continue
                val uri = item.optStringOrNull("uri")
                if (uri == null || !uri.startsWith(KEY_PREFIX)) continue   // episodios de pódcast u otros elementos
                val track = SpotifyTrack(
                    uri.removePrefix(KEY_PREFIX),
                    item.optStringOrNull("title") ?: "Sin título",
                    item.optStringOrNull("subtitle")?.takeIf { it.isNotEmpty() } ?: SongTags.UNKNOWN_ARTIST,
                    item.optLongOrNull("duration") ?: 0,
                )
                tracks[track.key] = track
                result.add(track)
            }
            SpotifyList(entity.optStringOrNull("name") ?: "Lista de Spotify", entity.optStringOrNull("subtitle") ?: "", result)
        } catch (_: JSONException) {
            throw SpotifyImportException("Spotify ha cambiado su página y no se puede leer la lista.")
        }
    }

    /**
     * Bytes de la portada (640 px) de una canción, o null si no se puede descargar. Se guarda en la
     * caché de la app, así solo se descarga una vez. Bloquea: llamarlo fuera del hilo principal.
     */
    fun coverData(context: Context, track: SpotifyTrack): ByteArray? {
        val folder = File(context.cacheDir, "spotify")
        val file = File(folder, "${track.id}.jpg")
        return try {
            if (file.exists()) return file.readBytes()

            // oEmbed es el servicio público de Spotify para mostrar una canción en otra web; da la
            // portada a 300 px. Cambiando el código de tamaño de la URL se obtiene la de 640 px.
            val oembed = JSONObject(
                Http.get("https://open.spotify.com/oembed?url=" + Uri.encode("https://open.spotify.com/track/${track.id}"))
            )
            val url = oembed.optStringOrNull("thumbnail_url")?.replace("ab67616d00001e02", "ab67616d0000b273")
                ?: return null
            val bytes = Http.getBytes(url)

            // Primero a un temporal para no dejar una portada a medias.
            folder.mkdirs()
            val temp = File(folder, "${track.id}.jpg.tmp")
            temp.writeBytes(bytes)
            temp.renameTo(file)
            bytes
        } catch (_: IOException) {
            null   // sin portada esta vez; se reintentará la próxima vez que se necesite
        } catch (_: JSONException) {
            null
        }
    }

    /**
     * Clave para reconocer la misma canción en Spotify y en un .mp3 aunque se escriba distinto:
     * sin mayúsculas, tildes, signos ni añadidos como "(feat. X)" o "- Remastered".
     */
    fun matchKey(artist: String, title: String): String {
        var main = artist
        for (separator in listOf(",", "&", ";", " feat", " ft.", " x ")) {
            val cut = main.indexOf(separator, ignoreCase = true)
            if (cut > 0) main = main.substring(0, cut)
        }
        return simplify(main) + "|" + simplify(cleanTitle(title))
    }

    private fun simplify(text: String): String = buildString {
        for (c in Normalizer.normalize(text, Normalizer.Form.NFD)) {
            if (c.isLetterOrDigit()) append(c.lowercaseChar())
        }
    }
}
