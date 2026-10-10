package io.github.sergio120602.launcherito.data

import android.content.Context
import android.util.AtomicFile
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import java.io.File
import java.io.IOException

/**
 * Orden y tamaño de las portadas que el usuario ha elegido en el mosaico, guardados en disco para
 * que se mantengan al cerrar la app. Mientras no se toque nada, el mosaico va en orden alfabético
 * con los tamaños automáticos.
 */
class MosaicArrangement(context: Context) {
    private val file = AtomicFile(File(context.filesDir, "mosaico.json"))
    private var order = mutableListOf<String>()
    private val spans = HashMap<String, Int>()

    init {
        load()
    }

    /**
     * Ordena las canciones, que llegan en orden alfabético: primero las ya colocadas, en el orden
     * elegido; las nuevas, al final y entre ellas por orden alfabético.
     */
    fun arrange(songs: List<String>): List<String> {
        if (order.isEmpty()) return songs
        val position = HashMap<String, Int>()
        order.forEachIndexed { i, key -> position.putIfAbsent(key, i) }
        return songs.sortedBy { position[it] ?: Int.MAX_VALUE }   // sortedBy es estable, como OrderBy
    }

    /** Tamaño elegido para la canción (en celdas de lado), o null si no se ha guardado ninguno. */
    fun spanOf(key: String): Int? = spans[key]

    /** Guarda el orden y el tamaño de todas las portadas tal como están ahora. */
    fun remember(tiles: List<Pair<String, Int>>) {
        val shown = tiles.mapTo(HashSet()) { it.first }
        // Las que ahora no están (una lista de Spotify quitada, p. ej.) conservan su orden detrás.
        order = (tiles.map { it.first } + order.filter { it !in shown }).toMutableList()
        for ((key, span) in tiles) spans[key] = span
        save()
    }

    private fun save() {
        val json = JSONObject()
            .put("Order", JSONArray(order))
            .put("Spans", JSONObject().also { o -> spans.forEach { (k, v) -> o.put(k, v) } })
        val stream = try {
            file.startWrite()
        } catch (_: IOException) {
            return   // si no se puede guardar, el orden se mantiene mientras la app siga abierta
        }
        try {
            stream.write(json.toString().toByteArray())
            file.finishWrite(stream)
        } catch (_: IOException) {
            file.failWrite(stream)
        }
    }

    private fun load() {
        try {
            val json = JSONObject(String(file.readFully()))
            json.optJSONArray("Order")?.let { a -> for (i in 0 until a.length()) order.add(a.getString(i)) }
            json.optJSONObject("Spans")?.let { o -> o.keys().forEach { spans[it] = o.getInt(it) } }
        } catch (_: IOException) {
        } catch (_: JSONException) {
        }
    }
}
