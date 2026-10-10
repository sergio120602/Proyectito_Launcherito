package io.github.sergio120602.launcherito.player

import android.content.Context
import androidx.media3.common.AudioAttributes
import androidx.media3.common.C
import androidx.media3.common.ForwardingPlayer
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer

/**
 * El reproductor de los .mp3, uno para toda la app. Lo manejan la pantalla (LauncheritoViewModel) y
 * la notificación (PlaybackService). La lista de reproducción la lleva la pantalla: el reproductor
 * solo tiene la canción que suena, y los botones de anterior/siguiente de la notificación le avisan.
 */
object PlayerHolder {
    private var player: ExoPlayer? = null

    /** Lo que hacen los botones de anterior y siguiente de la notificación y de los auriculares. */
    var onNext: (() -> Unit)? = null
    var onPrevious: (() -> Unit)? = null

    fun get(context: Context): ExoPlayer = player ?: ExoPlayer.Builder(context.applicationContext)
        // true: baja o pausa la música cuando suena otra app (una llamada, un vídeo…).
        .setAudioAttributes(
            AudioAttributes.Builder().setUsage(C.USAGE_MEDIA).setContentType(C.AUDIO_CONTENT_TYPE_MUSIC).build(),
            true,
        )
        // Se pausa al desconectar los auriculares.
        .setHandleAudioBecomingNoisy(true)
        .build()
        .also { player = it }

    /** El reproductor tal como lo ve la notificación: siempre con anterior y siguiente. */
    fun sessionPlayer(context: Context): Player = object : ForwardingPlayer(get(context)) {
        private val extra = listOf(
            COMMAND_SEEK_TO_NEXT, COMMAND_SEEK_TO_NEXT_MEDIA_ITEM,
            COMMAND_SEEK_TO_PREVIOUS, COMMAND_SEEK_TO_PREVIOUS_MEDIA_ITEM,
        )

        override fun getAvailableCommands(): Player.Commands =
            super.getAvailableCommands().buildUpon().addAll(*extra.toIntArray()).build()

        override fun isCommandAvailable(command: Int) = command in extra || super.isCommandAvailable(command)

        override fun seekToNext() {
            onNext?.invoke()
        }

        override fun seekToNextMediaItem() {
            onNext?.invoke()
        }

        override fun seekToPrevious() {
            onPrevious?.invoke()
        }

        override fun seekToPreviousMediaItem() {
            onPrevious?.invoke()
        }
    }
}
