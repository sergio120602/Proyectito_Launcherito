package io.github.sergio120602.launcherito

import android.app.Application
import coil3.ImageLoader
import coil3.PlatformContext
import coil3.SingletonImageLoader
import coil3.request.crossfade
import io.github.sergio120602.launcherito.data.SongCoverFetcher
import io.github.sergio120602.launcherito.data.SongCoverKeyer

class LauncheritoApp : Application(), SingletonImageLoader.Factory {
    /** Coil sabe cargar, además de URLs, las carátulas de las canciones (SongCover). */
    override fun newImageLoader(context: PlatformContext): ImageLoader =
        ImageLoader.Builder(context)
            .components {
                add(SongCoverFetcher.Factory(this@LauncheritoApp))
                add(SongCoverKeyer())
            }
            .crossfade(true)
            .build()
}
