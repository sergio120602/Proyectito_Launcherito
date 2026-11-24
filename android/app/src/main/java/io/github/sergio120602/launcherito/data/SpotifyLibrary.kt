package io.github.sergio120602.launcherito.data

import android.net.Uri
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONException
import org.json.JSONObject
import java.io.IOException

/**
 * Lee listas públicas y álbumes de Spotify sin cuenta ni clave. La API oficial exige desde febrero
 * de 2026 que el dueño de la app tenga Premium, así que se usa la página que Spotify ofrece para
 * insertar listas en otras webs (trae título y artistas de cada canción, hasta 100 canciones) y su
 * servicio oEmbed para las portadas. Spotify no da el audio completo: si no se tiene el .mp3, la
 * canción suena con su vídeo de YouTube ([YouTubeLinks]). Las canciones leídas se guardan en
 * [OnlineSongs].
 */
object SpotifyLibrary {
    const val KEY_PREFIX = "spotify:track:"

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

    /** Saca el tipo ("playlist" o "album") y el id de un enlace de Spotify, o null si no lo es. */
    fun parseLink(text: String?): ListLink? {
        val match = text?.let { linkPattern.find(it) } ?: return null
        return ListLink(OnlineSource.Spotify, match.groups["type"]!!.value.lowercase(), match.groups["id"]!!.value)
    }

    /** Descarga la lista o el álbum. */
    @Throws(ListImportException::class)
    suspend fun load(type: String, id: String): OnlineList = withContext(Dispatchers.IO) {
        val html = try {
            Http.get("https://open.spotify.com/embed/$type/$id", "User-Agent" to "Launcherito")
        } catch (_: IOException) {
            throw ListImportException("No se puede conectar con Spotify. Comprueba la conexión a Internet.")
        }

        val data = dataPattern.find(html)
            ?: throw ListImportException("Spotify ha cambiado su página y no se puede leer la lista.")

        try {
            val notFound = ListImportException(
                if (type == "album") "No se encuentra ese álbum en Spotify."
                else "No se encuentra la lista. Comprueba el enlace y que la lista sea pública."
            )
            val entity = JSONObject(data.groups["json"]!!.value)
                .optJSONObject("props")?.optJSONObject("pageProps")?.optJSONObject("state")
                ?.optJSONObject("data")?.optJSONObject("entity") ?: throw notFound
            val list = entity.optJSONArray("trackList") ?: throw notFound

            val result = mutableListOf<OnlineTrack>()
            for (i in 0 until list.length()) {
                val item = list.optJSONObject(i) ?: continue
                val uri = item.optStringOrNull("uri")
                if (uri == null || !uri.startsWith(KEY_PREFIX)) continue   // episodios de pódcast u otros elementos
                result.add(
                    OnlineTrack(
                        OnlineSource.Spotify,
                        uri.removePrefix(KEY_PREFIX),
                        item.optStringOrNull("title") ?: "Sin título",
                        item.optStringOrNull("subtitle")?.takeIf { it.isNotEmpty() } ?: SongTags.UNKNOWN_ARTIST,
                        item.optLongOrNull("duration") ?: 0,
                    )
                )
            }
            OnlineList(OnlineSource.Spotify, entity.optStringOrNull("name") ?: "Lista de Spotify", result, largestCover(entity))
        } catch (_: JSONException) {
            throw ListImportException("Spotify ha cambiado su página y no se puede leer la lista.")
        }
    }

    /** La portada más grande de la lista ("coverArt": {"sources": [{url, width}, …]}); el ancho puede venir vacío. */
    private fun largestCover(entity: JSONObject): String? {
        val sources = entity.optJSONObject("coverArt")?.optJSONArray("sources") ?: return null
        return (0 until sources.length()).mapNotNull { sources.optJSONObject(it) }
            .filter { it.optStringOrNull("url") != null }
            .maxByOrNull { (it.opt("width") as? Number)?.toInt() ?: 0 }
            ?.optStringOrNull("url")
    }

    /**
     * Dirección de la portada (640 px) de una canción. Se pregunta a Spotify solo cuando se recorre,
     * es decir, si la portada no está ya en la caché. Bloquea; lanza IOException o JSONException.
     */
    internal fun coverUrls(track: OnlineTrack): Sequence<String> = sequence {
        // oEmbed es el servicio público de Spotify para mostrar una canción en otra web; da la
        // portada a 300 px. Cambiando el código de tamaño de la URL se obtiene la de 640 px.
        val oembed = JSONObject(
            Http.get("https://open.spotify.com/oembed?url=" + Uri.encode("https://open.spotify.com/track/${track.id}"))
        )
        oembed.optStringOrNull("thumbnail_url")?.let { yield(it.replace("ab67616d00001e02", "ab67616d0000b273")) }
    }
}
