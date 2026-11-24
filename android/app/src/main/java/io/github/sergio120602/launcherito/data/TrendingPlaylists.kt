package io.github.sergio120602.launcherito.data

import android.content.Context
import android.util.AtomicFile
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import java.io.File
import java.io.IOException
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Date
import java.util.Locale

/** Una de las playlists del momento de Spotify, tal como se leyó la última vez. [tracks]: por puesto. */
class TrendingList(val id: String, val name: String, val coverUrl: String?, val tracks: List<SavedSong>)

/**
 * «Del momento»: las playlists más escuchadas de Spotify (Top 50, Éxitos…), que se leen cada día a las
 * 7:00. La información (canciones, artistas, portadas) es la de Spotify; la música suena con el vídeo de
 * YouTube de cada canción, como las demás de Spotify. Se guardan en del-momento.json, así se ven
 * aunque no haya conexión. Las renueva [TrendingWorker] (WorkManager) y también la app al abrirse si
 * ya ha pasado la hora.
 *
 * @param updated cuándo se leyeron por última vez (milisegundos), o null si nunca.
 */
class TrendingPlaylists(val updated: Long?, val lists: List<TrendingList>) {

    operator fun get(id: String): TrendingList? = lists.firstOrNull { it.id == id }

    /** Ya ha pasado la hora de renovarlas y no se ha hecho (o nunca se han leído). */
    fun isStale(now: Long = System.currentTimeMillis()): Boolean = updated == null || updated < lastRefreshTime(now)

    companion object {
        /** Hora del día a la que se renuevan. */
        const val REFRESH_HOUR = 7

        /**
         * Las que se siguen (id de Spotify y nombre por si aún no se han leído): listas de la propia
         * Spotify que se renuevan solas (los Top 50 cada día, las demás cada semana). Las mismas que en Windows.
         */
        val SOURCES = listOf(
            "37i9dQZEVXbNFJfN1Vw8d9" to "Top 50 - España",
            "37i9dQZEVXbMDoHDwVN2tF" to "Top 50 - Global",
            "37i9dQZF1DXaxEKcoCdWHD" to "Éxitos España",
            "37i9dQZF1DXcBWIGoYBM5M" to "Today's Top Hits",
            "37i9dQZF1DX10zKzsJ2jva" to "Viva Latino",
            "37i9dQZF1DWY7IeIP1cdjF" to "Baila Reggaeton",
            "37i9dQZF1DWUa8ZRTfalHk" to "Pop Rising",
            "37i9dQZF1DX0XUsuxWHRQd" to "RapCaviar",
        )

        private fun file(context: Context) = AtomicFile(File(context.filesDir, "del-momento.json"))

        /** La última vez que tocaba renovarlas: hoy a las 7:00, o ayer si aún no son las 7. */
        fun lastRefreshTime(now: Long): Long {
            val today = Calendar.getInstance().apply {
                timeInMillis = now
                set(Calendar.HOUR_OF_DAY, REFRESH_HOUR); set(Calendar.MINUTE, 0); set(Calendar.SECOND, 0); set(Calendar.MILLISECOND, 0)
            }
            if (today.timeInMillis > now) today.add(Calendar.DAY_OF_MONTH, -1)
            return today.timeInMillis
        }

        /** La próxima vez que toca renovarlas: hoy a las 7:00 si aún no han llegado, si no mañana. */
        fun nextRefreshTime(now: Long): Long = Calendar.getInstance().apply {
            timeInMillis = lastRefreshTime(now)
            add(Calendar.DAY_OF_MONTH, 1)
        }.timeInMillis

        /** Lo guardado, o vacío si no hay nada (o está dañado). */
        fun load(context: Context): TrendingPlaylists = try {
            val json = JSONObject(String(file(context).readFully()))
            val lists = json.optJSONArray("Lists") ?: JSONArray()
            TrendingPlaylists(
                json.optLongOrNull("Updated"),
                (0 until lists.length()).mapNotNull { i ->
                    val item = lists.optJSONObject(i) ?: return@mapNotNull null
                    val tracks = item.optJSONArray("Tracks") ?: JSONArray()
                    TrendingList(
                        item.optString("Id"), item.optString("Name"), item.optStringOrNull("CoverUrl"),
                        (0 until tracks.length()).mapNotNull { t ->
                            tracks.optJSONObject(t)?.let {
                                SavedSong(it.optString("Key"), it.optString("Title"), it.optString("Artist"), it.optLong("DurationMs"))
                            }
                        },
                    )
                },
            )
        } catch (_: IOException) {
            TrendingPlaylists(null, emptyList())
        } catch (_: JSONException) {
            TrendingPlaylists(null, emptyList())
        }

        /**
         * Vuelve a leer todas de Spotify y lo guarda. Si alguna no se puede leer se queda la de la vez
         * anterior; si no se puede leer ninguna (sin conexión), no se toca nada y se lanza la excepción.
         */
        @Throws(ListImportException::class)
        suspend fun refresh(context: Context, previous: TrendingPlaylists = load(context)): TrendingPlaylists {
            val lists = mutableListOf<TrendingList>()
            var read = 0
            var lastError: String? = null
            for ((id, name) in SOURCES) {
                try {
                    val list = SpotifyLibrary.load("playlist", id)
                    if (list.tracks.isEmpty()) throw ListImportException("La lista ha llegado vacía.")
                    lists.add(TrendingList(id, list.name, list.coverUrl, list.tracks.map(SavedSong::fromTrack)))
                    read++
                } catch (e: Exception) {
                    // Un fallo con una lista no debe impedir las demás: esa se queda como estaba.
                    lastError = e.message ?: e.javaClass.simpleName
                    lists.add(previous[id] ?: TrendingList(id, name, null, emptyList()))
                }
            }
            if (read == 0) {
                log(context, "No se ha podido leer ninguna playlist: $lastError")
                throw ListImportException(lastError ?: "No se ha podido leer ninguna playlist de Spotify.")
            }
            val fresh = TrendingPlaylists(System.currentTimeMillis(), lists)
            fresh.save(context)
            log(
                context,
                if (read == SOURCES.size) "Actualizadas las $read playlists."
                else "Actualizadas $read de ${SOURCES.size} playlists (las demás se quedan como estaban): $lastError",
            )
            return fresh
        }

        /** Una línea en del-momento.log, para saber qué pasó con la actualización de las 7:00. */
        private fun log(context: Context, message: String) {
            try {
                val file = File(context.filesDir, "del-momento.log")
                val lines = if (file.exists()) file.readLines().takeLast(199) else emptyList()
                val time = SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.ROOT).format(Date())
                file.writeText((lines + "$time  $message").joinToString("\n") + "\n")
            } catch (_: IOException) {
                // Sin registro: no es importante.
            }
        }
    }

    private fun save(context: Context) {
        val json = JSONObject()
            .put("Updated", updated)
            .put("Lists", JSONArray().also { array ->
                for (list in lists) {
                    array.put(JSONObject().apply {
                        put("Id", list.id)
                        put("Name", list.name)
                        list.coverUrl?.let { put("CoverUrl", it) }
                        put("Tracks", JSONArray().also { tracks ->
                            for (song in list.tracks) {
                                tracks.put(JSONObject().put("Key", song.key).put("Title", song.title)
                                    .put("Artist", song.artist).put("DurationMs", song.durationMs))
                            }
                        })
                    })
                }
            })
        val file = file(context)
        val stream = try {
            file.startWrite()
        } catch (_: IOException) {
            return
        }
        try {
            stream.write(json.toString().toByteArray())
            file.finishWrite(stream)
        } catch (_: IOException) {
            file.failWrite(stream)
        }
    }
}
