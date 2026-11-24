package io.github.sergio120602.launcherito.data

import android.content.Context
import android.util.AtomicFile
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import java.io.File
import java.io.IOException
import java.util.UUID

/**
 * Playlist hecha por el usuario: un nombre y sus canciones en el orden en que se han añadido (o en el
 * que se han colocado en su mosaico). Cada canción es su clave de «Tus canciones».
 *
 * @param listView se ve como lista sencilla en lugar de como mosaico de portadas.
 * @param spans tamaño elegido para cada portada de su mosaico (celdas de lado).
 */
class UserPlaylist(
    val id: String,
    var name: String,
    val songs: MutableList<String> = mutableListOf(),
    var listView: Boolean = false,
    val spans: MutableMap<String, Int> = HashMap(),
) {
    /** La lista fija de Favoritos: no se puede renombrar ni borrar. */
    val isFavorites: Boolean get() = id == UserPlaylists.FAVORITES_ID

    operator fun contains(key: String) = key in songs
}

/**
 * Favoritos y las playlists del usuario, guardadas en playlists.json dentro de los archivos de la app
 * (el mismo formato que en Windows). Cada cambio se escribe al momento. Quitar una canción de una
 * playlist no la borra de «Tus canciones».
 */
class UserPlaylists(context: Context) {
    private val file = AtomicFile(File(context.filesDir, "playlists.json"))
    private val lists = load()

    init {
        // Favoritos siempre existe y va la primera.
        val favorites = lists.firstOrNull { it.isFavorites } ?: UserPlaylist(FAVORITES_ID, FAVORITES_NAME)
        favorites.name = FAVORITES_NAME
        lists.remove(favorites)
        lists.add(0, favorites)
    }

    val favorites: UserPlaylist get() = lists[0]

    /** Favoritos y después las demás, en el orden en que se crearon. */
    val all: List<UserPlaylist> get() = lists

    operator fun get(id: String): UserPlaylist? = lists.firstOrNull { it.id == id }

    fun isFavorite(key: String) = key in favorites

    /** Comprueba un nombre para una playlist nueva o renombrada. Devuelve el motivo si no vale, o null. */
    fun checkName(name: String, renamingId: String? = null): String? = when {
        name.isEmpty() -> "Escribe un nombre para la playlist."
        name.length > MAX_NAME_LENGTH -> "El nombre es demasiado largo (como mucho $MAX_NAME_LENGTH letras)."
        lists.any { it.id != renamingId && it.name.equals(name, ignoreCase = true) } -> "Ya hay una playlist que se llama «$name»."
        else -> null
    }

    fun create(name: String): UserPlaylist {
        val list = UserPlaylist(UUID.randomUUID().toString().replace("-", ""), name)
        lists.add(list)
        save()
        return list
    }

    fun rename(id: String, name: String) {
        val list = get(id) ?: return
        if (list.isFavorites || list.name == name) return
        list.name = name
        save()
    }

    fun delete(id: String) {
        if (lists.removeAll { it.id == id && !it.isFavorites }) save()
    }

    /** Añade al final las canciones que aún no están en la playlist. Devuelve cuántas se han añadido. */
    fun add(id: String, keys: Iterable<String>): Int {
        val list = get(id) ?: return 0
        var added = 0
        for (key in keys) {
            if (key !in list) {
                list.songs.add(key)
                added++
            }
        }
        if (added > 0) save()
        return added
    }

    /** Quita canciones de una playlist. Devuelve cuántas se han quitado. */
    fun remove(id: String, keys: Iterable<String>): Int {
        val list = get(id) ?: return 0
        val gone = keys.toHashSet()
        val before = list.songs.size
        list.songs.removeAll(gone)
        val removed = before - list.songs.size
        if (removed > 0) save()
        return removed
    }

    /** Pone o quita una canción de Favoritos. Devuelve si queda como favorita. */
    fun toggleFavorite(key: String): Boolean {
        if (isFavorite(key)) {
            remove(FAVORITES_ID, listOf(key))
            return false
        }
        add(FAVORITES_ID, listOf(key))
        return true
    }

    /** Recuerda si la playlist se ve como lista o como mosaico. */
    fun setListView(id: String, listView: Boolean) {
        val list = get(id) ?: return
        if (list.listView == listView) return
        list.listView = listView
        save()
    }

    /**
     * Guarda cómo ha quedado su mosaico: el orden de las portadas pasa a ser el de la playlist (las
     * que no se veían, p. ej. un .mp3 que ya no se puede abrir, siguen detrás) y el tamaño de cada una.
     */
    fun saveLayout(id: String, order: List<String>, spans: Map<String, Int>) {
        val list = get(id) ?: return
        val shown = order.toHashSet()
        val songs = order.filter { it in list } + list.songs.filter { it !in shown }
        list.songs.clear()
        list.songs.addAll(songs)
        list.spans.putAll(spans)
        save()
    }

    /** Quita de todas las playlists canciones que se han borrado de «Tus canciones». */
    fun forget(keys: Iterable<String>) {
        val gone = keys.toHashSet()
        var changed = false
        for (list in lists) {
            changed = list.songs.removeAll(gone) or changed
            list.spans.keys.removeAll(gone)
        }
        if (changed) save()
    }

    /**
     * Cambia una canción por otra en todas las playlists, en el mismo sitio (p. ej. una de Spotify por
     * el .mp3 que ya se tiene). Si la nueva ya estaba en esa playlist, solo se quita la vieja.
     */
    fun replace(oldKey: String, newKey: String) {
        var changed = false
        for (list in lists) {
            val at = list.songs.indexOf(oldKey)
            if (at < 0) continue
            if (newKey in list) list.songs.removeAt(at) else list.songs[at] = newKey
            list.spans.remove(oldKey)?.let { list.spans.putIfAbsent(newKey, it) }
            changed = true
        }
        if (changed) save()
    }

    private fun save() {
        val json = JSONArray()
        for (list in lists) {
            json.put(JSONObject().apply {
                put("Id", list.id)
                put("Name", list.name)
                put("Songs", JSONArray(list.songs))
                put("ListView", list.listView)
                put("Spans", JSONObject().also { o -> list.spans.forEach { (k, v) -> o.put(k, v) } })
            })
        }
        val stream = try {
            file.startWrite()
        } catch (_: IOException) {
            return   // si no se puede guardar ahora, se vuelve a intentar con el siguiente cambio
        }
        try {
            stream.write(json.toString(2).toByteArray())
            file.finishWrite(stream)
        } catch (_: IOException) {
            file.failWrite(stream)
        }
    }

    private fun load(): MutableList<UserPlaylist> = try {
        val json = JSONArray(String(file.readFully()))
        val seen = HashSet<String>()
        (0 until json.length()).mapNotNull { i ->
            val item = json.optJSONObject(i) ?: return@mapNotNull null
            val id = item.optString("Id")
            if (id.isEmpty() || !seen.add(id)) return@mapNotNull null
            val songs = item.optJSONArray("Songs")?.let { a -> (0 until a.length()).map { a.optString(it) } }.orEmpty()
            val spans = HashMap<String, Int>()
            item.optJSONObject("Spans")?.let { o -> o.keys().forEach { k -> spans[k] = o.optInt(k, 1) } }
            UserPlaylist(
                id, item.optString("Name"),
                songs.filter { it.isNotEmpty() }.distinct().toMutableList(),
                item.optBoolean("ListView"), spans,
            )
        }.toMutableList()
    } catch (_: IOException) {
        mutableListOf()   // primera vez (aún no hay archivo)
    } catch (_: JSONException) {
        mutableListOf()   // archivo dañado
    }

    companion object {
        const val FAVORITES_ID = "favoritos"
        const val FAVORITES_NAME = "Favoritos"
        const val MAX_NAME_LENGTH = 60
    }
}
