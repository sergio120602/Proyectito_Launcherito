package io.github.sergio120602.launcherito.data

import android.content.Context
import coil3.ImageLoader
import coil3.decode.DataSource
import coil3.decode.ImageSource
import coil3.fetch.FetchResult
import coil3.fetch.Fetcher
import coil3.fetch.SourceFetchResult
import coil3.key.Keyer
import coil3.request.Options
import okio.Buffer

/**
 * Carátula de una canción para Coil: se lee del .mp3 (o de la caché de Spotify) cuando se va a
 * dibujar, y Coil la decodifica al tamaño con que se ve. Así una biblioteca grande no llena la memoria.
 */
data class SongCover(val key: String)

class SongCoverFetcher(private val context: Context, private val cover: SongCover, private val options: Options) : Fetcher {
    override suspend fun fetch(): FetchResult? {
        // Coil llama a los fetchers fuera del hilo principal: se puede leer el archivo aquí.
        val bytes = SongTags.coverData(context, cover.key) ?: throw NoCoverException()
        return SourceFetchResult(
            source = ImageSource(Buffer().write(bytes), options.fileSystem),
            mimeType = null,
            dataSource = DataSource.DISK,
        )
    }

    class Factory(private val context: Context) : Fetcher.Factory<SongCover> {
        override fun create(data: SongCover, options: Options, imageLoader: ImageLoader): Fetcher =
            SongCoverFetcher(context, data, options)
    }
}

/** La canción no trae carátula: se queda el icono de nota. */
class NoCoverException : Exception("La canción no tiene carátula")

/** Clave de la caché en memoria de Coil: la de la canción. */
class SongCoverKeyer : Keyer<SongCover> {
    override fun key(data: SongCover, options: Options): String = "cover:" + data.key
}
