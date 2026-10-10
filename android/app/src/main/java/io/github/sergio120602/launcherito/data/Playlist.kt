package io.github.sergio120602.launcherito.data

import java.text.Collator
import kotlin.random.Random

/**
 * Lista de reproducción. Guarda las canciones en orden alfabético y mantiene aparte
 * el orden en que se reproducen, que puede ser alfabético o aleatorio.
 */
class Playlist(private val titleOf: (String) -> String) {
    // SECONDARY: sin distinguir mayúsculas, como CurrentCultureIgnoreCase en la versión de Windows.
    private val collator = Collator.getInstance().apply { strength = Collator.SECONDARY }

    private val songs = mutableListOf<String>()        // claves en orden alfabético
    private val known = HashSet<String>()
    private var order = mutableListOf<Int>()           // índices de songs en orden de reproducción

    /** Posición (empezando en 0) de la canción actual en el orden de reproducción. */
    var position = -1
        private set

    val count: Int get() = songs.size

    /** Todas las canciones en orden alfabético (el orden del mosaico). */
    val songList: List<String> get() = songs

    val current: String? get() = if (position < 0) null else songs[order[position]]

    /**
     * true: las canciones se reproducen desordenadas. false: en orden alfabético. Al cambiarlo, la
     * canción actual sigue sonando y solo cambia cuál va después. Empieza activado.
     */
    var shuffle = true
        set(value) {
            if (field == value) return
            field = value
            rebuildOrder(current)
        }

    /** Añade canciones ignorando las repetidas. Devuelve cuántas se han añadido. */
    fun add(keys: Iterable<String>): Int {
        var added = 0
        for (key in keys) {
            if (known.add(key)) {
                songs.add(key)
                added++
            }
        }
        if (added > 0) {
            val current = current
            songs.sortWith(::compareTitles)
            rebuildOrder(current)
        }
        return added
    }

    /** Quita canciones de la lista, salvo la que está sonando. Devuelve cuántas se han quitado. */
    fun remove(keys: Iterable<String>): Int {
        val current = current
        val gone = HashSet<String>()
        for (key in keys) {
            if (key != current && known.remove(key)) gone.add(key)
        }
        if (gone.isNotEmpty()) {
            songs.removeAll(gone)
            rebuildOrder(current)
        }
        return gone.size
    }

    /** Hace que la canción indicada sea la actual sin cambiar el orden de reproducción. */
    fun jumpTo(key: String): String? {
        val index = songs.indexOf(key)
        if (index >= 0) position = order.indexOf(index)
        return current
    }

    fun next(): String? = move(+1)

    fun previous(): String? = move(-1)

    /** Quita la canción actual (p. ej. si no se puede reproducir) y pasa a la siguiente. */
    fun removeCurrent(): String? {
        if (position < 0) return null
        val removed = order[position]
        known.remove(songs[removed])
        songs.removeAt(removed)
        order.removeAt(position)
        for (i in order.indices) {
            if (order[i] > removed) order[i]--
        }
        if (order.isEmpty()) position = -1
        else if (position >= order.size) position = 0
        return current
    }

    /** Orden alfabético por título, el mismo que el del mosaico. */
    fun compareTitles(a: String, b: String): Int = collator.compare(titleOf(a), titleOf(b))

    private fun move(step: Int): String? {
        if (order.isEmpty()) return null
        // Al llegar al final vuelve al principio (y al revés).
        position = (position + step + order.size) % order.size
        return current
    }

    private fun rebuildOrder(current: String?) {
        order = songs.indices.toMutableList()
        val currentIndex = if (current == null) -1 else songs.indexOf(current)
        if (shuffle) {
            order.shuffle(Random)   // Fisher-Yates
            // La canción que está sonando pasa a ser la primera del nuevo orden aleatorio.
            if (currentIndex >= 0) {
                order.remove(currentIndex)
                order.add(0, currentIndex)
            }
        }
        position = if (currentIndex >= 0) order.indexOf(currentIndex) else if (order.isNotEmpty()) 0 else -1
    }
}
