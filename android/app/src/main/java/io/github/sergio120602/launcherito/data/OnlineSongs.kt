package io.github.sergio120602.launcherito.data

import android.content.Context
import org.json.JSONException
import java.io.File
import java.io.IOException
import java.text.Normalizer
import java.util.concurrent.ConcurrentHashMap

/** De dónde viene una canción que no es un .mp3 del móvil. */
enum class OnlineSource { Spotify, YouTubeMusic }

/**
 * Canción de una lista de Spotify o de YouTube Music. Dentro de Launcherito se identifica por [key].
 * No tiene .mp3: suena con su vídeo de YouTube ([YouTubeLinks]).
 *
 * @param id id de Spotify o, en YouTube Music, el id del vídeo.
 * @param coverUrl portada que da la propia lista (YouTube Music); null si se busca aparte.
 */
data class OnlineTrack(
    val source: OnlineSource,
    val id: String,
    val title: String,
    val artists: String,
    val durationMs: Long,
    val coverUrl: String? = null,
) {
    val key: String get() = OnlineSongs.keyPrefix(source) + id

    /** Primer artista ("Drake, Future" → "Drake"), el que se usa para agrupar y para Deezer. */
    val mainArtist: String get() = artists.split(',')[0].trim()
}

/** Lista o álbum ya leído. [coverUrl]: portada de la propia lista, si el servicio la da (Spotify). */
data class OnlineList(val source: OnlineSource, val name: String, val tracks: List<OnlineTrack>, val coverUrl: String? = null)

/** Enlace de una lista reconocido: de qué servicio es, qué es ("playlist", "album"…) y su id. */
data class ListLink(val source: OnlineSource, val type: String, val id: String)

/** No se ha podido leer una lista; el mensaje se muestra tal cual. */
class ListImportException(message: String) : Exception(message)

/**
 * Las canciones de Spotify y YouTube Music que conoce Launcherito, con lo que tienen en común:
 * reconocer sus enlaces, leer sus listas, sus portadas y si ya se tienen en .mp3.
 */
object OnlineSongs {
    private val tracks = ConcurrentHashMap<String, OnlineTrack>()

    fun keyPrefix(source: OnlineSource) = when (source) {
        OnlineSource.Spotify -> SpotifyLibrary.KEY_PREFIX
        OnlineSource.YouTubeMusic -> YouTubeMusicLibrary.KEY_PREFIX
    }

    /** Nombre del servicio para los textos ("Spotify", "YouTube Music"). */
    fun name(source: OnlineSource) = when (source) {
        OnlineSource.Spotify -> "Spotify"
        OnlineSource.YouTubeMusic -> "YouTube Music"
    }

    /** De qué servicio es la clave, o null si es un .mp3. */
    fun sourceOf(key: String): OnlineSource? = when {
        SpotifyLibrary.isSpotify(key) -> OnlineSource.Spotify
        YouTubeMusicLibrary.isYouTubeMusic(key) -> OnlineSource.YouTubeMusic
        else -> null
    }

    /** La canción no es un .mp3: viene de Spotify o de YouTube Music y suena con su vídeo. */
    fun isOnline(key: String) = sourceOf(key) != null

    fun tryGet(key: String): OnlineTrack? = tracks[key]

    /** Vuelve a dar a conocer una canción guardada («Tus canciones») sin pedirla otra vez. */
    fun register(track: OnlineTrack) {
        tracks[track.key] = track
    }

    /** Reconoce un enlace de una lista o un álbum de Spotify o de YouTube Music; null si no lo es. */
    fun parseLink(text: String?): ListLink? = SpotifyLibrary.parseLink(text) ?: YouTubeMusicLibrary.parseLink(text)

    /** Descarga la lista o el álbum y registra sus canciones. */
    @Throws(ListImportException::class)
    suspend fun load(link: ListLink): OnlineList {
        val list = when (link.source) {
            OnlineSource.Spotify -> SpotifyLibrary.load(link.type, link.id)
            OnlineSource.YouTubeMusic -> YouTubeMusicLibrary.load(link.type, link.id)
        }
        list.tracks.forEach(::register)
        return list
    }

    /**
     * Bytes de la portada de una canción, o null si no se puede descargar. Se guarda en la caché de
     * la app, así solo se descarga una vez. Bloquea: llamarlo fuera del hilo principal.
     */
    fun coverData(context: Context, track: OnlineTrack): ByteArray? = when (track.source) {
        OnlineSource.Spotify -> cachedCover(context, "spotify", track.id, SpotifyLibrary.coverUrls(track))
        OnlineSource.YouTubeMusic -> cachedCover(context, "ytmusic", track.id, YouTubeMusicLibrary.coverUrls(track))
    }

    /**
     * Portada guardada en la caché, en la carpeta [folder], o la primera de [urls] que se pueda
     * descargar (se piden una a una, solo si hace falta).
     */
    private fun cachedCover(context: Context, folder: String, id: String, urls: Sequence<String>): ByteArray? {
        val directory = File(context.cacheDir, folder)
        val file = File(directory, "$id.jpg")
        return try {
            if (file.exists()) return file.readBytes()
            for (url in urls) {
                val bytes = try {
                    Http.getBytes(url)
                } catch (_: IOException) {
                    continue   // p. ej. un vídeo sin la imagen grande: se prueba la siguiente
                }
                // Primero a un temporal para no dejar una portada a medias.
                directory.mkdirs()
                val temp = File(directory, "$id.jpg.tmp")
                temp.writeBytes(bytes)
                temp.renameTo(file)
                return bytes
            }
            null
        } catch (_: IOException) {
            null   // sin portada esta vez; se reintentará la próxima vez que se necesite
        } catch (_: JSONException) {
            null
        }
    }

    /**
     * Clave para reconocer la misma canción en Spotify, YouTube Music y un .mp3 aunque se escriba
     * distinto: sin mayúsculas, tildes, signos ni añadidos como "(feat. X)" o "- Remastered".
     */
    fun matchKey(artist: String, title: String): String {
        var main = artist
        for (separator in listOf(",", "&", ";", " feat", " ft.", " x ")) {
            val cut = main.indexOf(separator, ignoreCase = true)
            if (cut > 0) main = main.substring(0, cut)
        }
        return simplify(main) + "|" + simplify(cleanTitle(title))
    }

    /** Solo letras y números, en minúscula y sin tildes. */
    internal fun simplify(text: String): String = buildString {
        for (c in Normalizer.normalize(text, Normalizer.Form.NFD)) {
            if (c.isLetterOrDigit()) append(c.lowercaseChar())
        }
    }
}
