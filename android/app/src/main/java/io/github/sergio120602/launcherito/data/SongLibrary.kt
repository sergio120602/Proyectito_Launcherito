package io.github.sergio120602.launcherito.data

import android.content.Context
import android.util.AtomicFile
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import java.io.File
import java.io.IOException
import java.text.Collator

/**
 * Canción guardada. [key] es la dirección content:// del .mp3 o la clave de Spotify ("spotify:track:…").
 * De las de Spotify se guardan todos sus datos, porque sin ellos no se pueden volver a mostrar.
 */
class SavedSong(val key: String, var title: String = "", var artist: String = "", val durationMs: Long = 0) {
    val isSpotify: Boolean get() = SpotifyLibrary.isSpotify(key)

    fun toTrack() = SpotifyTrack(key.removePrefix(SpotifyLibrary.KEY_PREFIX), title, artist, durationMs)

    companion object {
        fun fromTrack(track: SpotifyTrack) = SavedSong(track.key, track.title, track.artists, track.durationMs)
    }
}

/**
 * Las canciones que se han cargado alguna vez («Tus canciones»), guardadas en canciones.json dentro
 * de los archivos de la app para que sigan ahí al volver a abrirla. Borrar una solo la quita de aquí:
 * el archivo .mp3 no se toca.
 */
class SongLibrary(context: Context) {
    // AtomicFile escribe primero en un temporal: el archivo nunca se queda a medias.
    private val file = AtomicFile(File(context.filesDir, "canciones.json"))
    private val songs = LinkedHashMap<String, SavedSong>()
    private val collator = Collator.getInstance().apply { strength = Collator.SECONDARY }
    private var dirty = false

    init {
        for (song in load()) songs.putIfAbsent(song.key, song)
    }

    val count: Int get() = songs.size

    /** Todas, por orden alfabético de título. */
    val all: List<SavedSong> get() = songs.values.sortedWith { a, b -> collator.compare(a.title, b.title) }

    operator fun contains(key: String) = key in songs

    operator fun get(key: String): SavedSong? = songs[key]

    /** Guarda las canciones nuevas (las que ya estaban se dejan como están). */
    fun add(newSongs: Iterable<SavedSong>) {
        for (song in newSongs) {
            if (songs.putIfAbsent(song.key, song) == null) dirty = true
        }
        save()
    }

    /** Apunta el título y el artista de un .mp3 en cuanto se leen sus etiquetas. Se escribe con [save]. */
    fun describe(key: String, title: String, artist: String) {
        val song = songs[key] ?: return
        if (song.title != title || song.artist != artist) {
            song.title = title
            song.artist = artist
            dirty = true
        }
    }

    fun remove(keys: Iterable<String>) {
        for (key in keys) {
            if (songs.remove(key) != null) dirty = true
        }
        save()
    }

    /** Escribe el archivo si algo ha cambiado. */
    fun save() {
        if (!dirty) return
        val json = JSONArray()
        for (song in songs.values) {
            json.put(JSONObject().apply {
                put("Key", song.key)
                put("Title", song.title)
                put("Artist", song.artist)
                put("DurationMs", song.durationMs)
            })
        }
        val stream = try {
            file.startWrite()
        } catch (_: IOException) {
            return   // si no se puede guardar ahora, se reintenta con el siguiente cambio
        }
        try {
            stream.write(json.toString(2).toByteArray())
            file.finishWrite(stream)
            dirty = false
        } catch (_: IOException) {
            file.failWrite(stream)
        }
    }

    private fun load(): List<SavedSong> = try {
        val json = JSONArray(String(file.readFully()))
        (0 until json.length()).map { i ->
            val item = json.getJSONObject(i)
            SavedSong(item.getString("Key"), item.optString("Title"), item.optString("Artist"), item.optLong("DurationMs"))
        }
    } catch (_: IOException) {
        emptyList()   // primera vez (aún no hay archivo)
    } catch (_: JSONException) {
        emptyList()   // archivo dañado
    }
}
