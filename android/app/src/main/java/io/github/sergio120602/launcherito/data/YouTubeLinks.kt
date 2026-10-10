package io.github.sergio120602.launcherito.data

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.net.Uri
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.IOException
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.TimeUnit

/**
 * Vídeos de YouTube de las canciones: las de Spotify (que no se pueden escuchar enteras dentro de
 * Launcherito) y las de los .mp3 en la vista original. Se busca "artista título" en YouTube (sin
 * clave) y se toman los primeros vídeos.
 */
object YouTubeLinks {
    private const val MAX_VIDEOS = 5   // si el primero no se deja ver dentro de otra app, se prueban los siguientes

    private val client = Http.client.newBuilder().callTimeout(8, TimeUnit.SECONDS).build()
    private val videos = ConcurrentHashMap<String, List<String>>()   // clave de la canción → ids
    private val videoPattern = Regex(""""videoId":"(?<id>[A-Za-z0-9_-]{11})"""")

    /**
     * Ids de los primeros vídeos que da YouTube para la canción, en orden; vacío si no hay conexión.
     * Se recuerdan mientras la app está abierta.
     */
    suspend fun findVideos(key: String, artist: String, title: String): List<String> {
        videos[key]?.let { return it }
        return try {
            val html = withContext(Dispatchers.IO) {
                Http.get(
                    searchUrl(artist, title),
                    "User-Agent" to "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    // Sin esta cookie, en Europa YouTube responde con su página de aceptar cookies.
                    "Cookie" to "SOCS=CAI",
                    client = client,
                )
            }
            val ids = videoPattern.findAll(html).map { it.groups["id"]!!.value }.distinct().take(MAX_VIDEOS).toList()
            if (ids.isNotEmpty()) videos[key] = ids
            ids
        } catch (_: IOException) {
            emptyList()   // sin conexión o YouTube no responde
        }
    }

    /** Abre la dirección en el navegador (o en la app de YouTube, si la tiene el móvil). */
    fun open(context: Context, url: String) {
        try {
            context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        } catch (_: ActivityNotFoundException) {
            // Sin navegador: no se puede hacer nada.
        }
    }

    // Sin artista conocido se busca solo por el título.
    private fun searchUrl(artist: String, title: String) =
        "https://www.youtube.com/results?search_query=" + Uri.encode(
            (if (artist == SongTags.UNKNOWN_ARTIST) "" else "$artist ") + cleanTitle(title)
        )
}
