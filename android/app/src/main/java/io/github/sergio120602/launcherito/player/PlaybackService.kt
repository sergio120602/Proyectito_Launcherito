package io.github.sergio120602.launcherito.player

import android.app.PendingIntent
import android.content.Intent
import androidx.media3.session.MediaSession
import androidx.media3.session.MediaSessionService
import io.github.sergio120602.launcherito.MainActivity

/**
 * Servicio que mantiene la música sonando con la app en segundo plano o la pantalla apagada, y pone
 * la notificación con los controles (también en la pantalla de bloqueo y en los auriculares).
 * Media3 lo pasa a primer plano solo mientras suena algo.
 */
class PlaybackService : MediaSessionService() {
    private var session: MediaSession? = null

    override fun onCreate() {
        super.onCreate()
        // Al pulsar la notificación se vuelve a la app.
        val openApp = PendingIntent.getActivity(
            this, 0,
            Intent(this, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        session = MediaSession.Builder(this, PlayerHolder.sessionPlayer(this))
            .setSessionActivity(openApp)
            .build()
    }

    override fun onGetSession(controllerInfo: MediaSession.ControllerInfo): MediaSession? = session

    // Si se quita la app de recientes sin nada sonando, el servicio no tiene por qué seguir.
    override fun onTaskRemoved(rootIntent: Intent?) {
        val player = session?.player
        if (player == null || !player.playWhenReady || player.mediaItemCount == 0) stopSelf()
    }

    override fun onDestroy() {
        // El reproductor es de toda la app (PlayerHolder): aquí solo se suelta la sesión.
        session?.release()
        session = null
        super.onDestroy()
    }
}
