package io.github.sergio120602.launcherito.data

import android.content.Context
import android.media.MediaMetadataRetriever
import android.net.Uri
import android.provider.OpenableColumns
import java.util.concurrent.ConcurrentHashMap

/** Título y artista de una canción, leídos de sus etiquetas ID3 o de Spotify. */
data class SongInfo(val title: String, val artist: String)

/**
 * Lee las etiquetas de los .mp3. En Android cada canción se identifica por la dirección content://
 * que da el selector de archivos (o por "spotify:track:…"), no por una ruta del disco.
 */
object SongTags {
    /** Artista que se muestra cuando la canción no lo trae en sus etiquetas. */
    const val UNKNOWN_ARTIST = "Artista desconocido"

    // Nombre del archivo (sin .mp3) de cada dirección content://, que no lo lleva dentro.
    private val names = ConcurrentHashMap<String, String>()

    fun rememberName(key: String, name: String) {
        names[key] = name
    }

    /** Nombre del archivo que da el sistema para una dirección content://, sin la extensión. */
    fun queryName(context: Context, uri: Uri): String? = try {
        context.contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { cursor ->
            if (cursor.moveToFirst()) cursor.getString(0)?.substringBeforeLast('.') else null
        }
    } catch (_: Exception) {
        null
    }

    /** Título que se puede mostrar antes de leer las etiquetas: el de Spotify o el nombre del archivo. */
    fun fallbackTitle(key: String): String {
        SpotifyLibrary.tryGet(key)?.let { return it.title }
        names[key]?.let { return it }
        val segment = Uri.decode(Uri.parse(key).lastPathSegment ?: key)
        return segment.substringAfterLast('/').substringAfterLast(':').substringBeforeLast('.')
    }

    /** Lee título y artista. Bloquea: llamarlo fuera del hilo principal. */
    fun read(context: Context, key: String): SongInfo {
        SpotifyLibrary.tryGet(key)?.let { track ->
            // El artista lleva la marca de YouTube porque suena con su vídeo (no hay .mp3).
            return SongInfo(track.title, "${track.artists}  ·  vídeo de YouTube")
        }
        var title = fallbackTitle(key)
        var artist = UNKNOWN_ARTIST
        withRetriever(context, key) { retriever ->
            retriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_TITLE)?.takeIf { it.isNotBlank() }?.let { title = it }
            retriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_ARTIST)?.takeIf { it.isNotBlank() }?.let { artist = it }
        }
        return SongInfo(title, artist)
    }

    /** Bytes de la carátula (la incrustada en el .mp3 o la de Spotify), o null si no tiene. Bloquea. */
    fun coverData(context: Context, key: String): ByteArray? {
        val track = SpotifyLibrary.tryGet(key)
        return if (track != null) SpotifyLibrary.coverData(context, track)
        else withRetriever(context, key) { it.embeddedPicture }
    }

    private inline fun <T> withRetriever(context: Context, key: String, block: (MediaMetadataRetriever) -> T): T? {
        val retriever = MediaMetadataRetriever()
        return try {
            retriever.setDataSource(context, Uri.parse(key))
            block(retriever)
        } catch (_: Exception) {
            null   // etiquetas ilegibles o archivo que ya no está: se usa el nombre y la carátula por defecto
        } finally {
            retriever.release()
        }
    }
}
