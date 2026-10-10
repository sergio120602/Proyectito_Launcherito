package io.github.sergio120602.launcherito

import android.content.ComponentName
import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.key
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.media3.session.MediaController
import androidx.media3.session.SessionToken
import com.google.common.util.concurrent.ListenableFuture
import io.github.sergio120602.launcherito.player.PlaybackService
import io.github.sergio120602.launcherito.ui.Dialogs
import io.github.sergio120602.launcherito.ui.LauncheritoTheme
import io.github.sergio120602.launcherito.ui.LauncheritoViewModel
import io.github.sergio120602.launcherito.ui.PlayerScreen
import io.github.sergio120602.launcherito.ui.StartScreen
import io.github.sergio120602.launcherito.ui.VideoHost
import io.github.sergio120602.launcherito.ui.VideoSurface

class MainActivity : ComponentActivity() {
    private val vm: LauncheritoViewModel by viewModels()
    private var controller: ListenableFuture<MediaController>? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)
        // Al girar la pantalla la actividad se crea de nuevo con el mismo intent: no se vuelve a abrir.
        if (savedInstanceState == null) handleIntent(intent)
        setContent {
            LauncheritoTheme(largeText = vm.largeText) {
                val picker = rememberLauncherForActivityResult(ActivityResultContracts.OpenMultipleDocuments()) { uris ->
                    if (uris.isNotEmpty()) vm.addFiles(uris)
                }
                LauncheritoScreen(vm) { picker.launch(arrayOf("audio/mpeg")) }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handleIntent(intent)
    }

    /** «Abrir con → Launcherito» con un .mp3, o compartir una lista desde la app de Spotify. */
    private fun handleIntent(intent: Intent?) {
        when (intent?.action) {
            Intent.ACTION_VIEW -> intent.data?.let { vm.addFiles(listOf(it), persist = false) }
            Intent.ACTION_SEND -> intent.getStringExtra(Intent.EXTRA_TEXT)?.let { vm.openSpotifyDialog(it) }
        }
    }

    override fun onStart() {
        super.onStart()
        // Conectarse al servicio lo arranca; Media3 lo deja en primer plano mientras suena música.
        controller = MediaController.Builder(this, SessionToken(this, ComponentName(this, PlaybackService::class.java))).buildAsync()
    }

    override fun onStop() {
        controller?.let(MediaController::releaseFuture)
        controller = null
        vm.saveAll()
        super.onStop()
    }
}

@Composable
private fun LauncheritoScreen(vm: LauncheritoViewModel, onLoadFiles: () -> Unit) {
    Box(Modifier.fillMaxSize().background(Color.Black)) {
        // Hueco invisible para el vídeo de las canciones de Spotify fuera de la vista original: se oye
        // pero no se ve, porque la pantalla (con fondo negro) se dibuja encima. Va fuera del reproductor
        // para que siga sonando también en el menú principal; YouTube pide al menos 200 px de alto.
        val video = vm.songVideo
        if (video != null && vm.videoHost == VideoHost.Hidden) {
            key(video) { VideoSurface(video, visible = false, modifier = Modifier.size(400.dp, 225.dp)) }
        }
        Box(Modifier.fillMaxSize().background(Color.Black).windowInsetsPadding(WindowInsets.safeDrawing)) {
            if (vm.playerVisible) PlayerScreen(vm, onLoadFiles) else StartScreen(vm, onLoadFiles)
        }
        Dialogs(vm)
    }
}
