package io.github.sergio120602.launcherito.player

import android.annotation.SuppressLint
import android.content.Context
import android.graphics.Color
import android.net.Uri
import android.os.Handler
import android.os.Looper
import android.view.ViewGroup
import android.webkit.JavascriptInterface
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import io.github.sergio120602.launcherito.data.YouTubeLinks
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import java.util.Locale

/**
 * Reproductor de YouTube dentro de un WebView, sin controles de YouTube: lo manejan los controles de
 * Launcherito con [play], [pause], [seek] y [load], y avisa del tiempo ([onProgress]) y de cuándo
 * termina ([onEnded]). Se crea con el contexto de la app, así sobrevive a los giros de pantalla, y
 * su vista se cambia de sitio (vista original o hueco oculto) sin recargarse.
 */
@SuppressLint("SetJavaScriptEnabled")
class YouTubeVideo(context: Context) {
    private companion object {
        // Página propia servida desde la memoria: YouTube solo deja insertar sus vídeos desde una web
        // con dirección (no desde un archivo ni una página en blanco).
        const val HOST = "launcherito.local"

        const val PAGE = """<!doctype html>
<html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<style>
  html,body{margin:0;height:100%;overflow:hidden;background:#000}#player{width:100%;height:100%}
  #player{pointer-events:none}
</style>
</head><body><div id="player"></div>
<script>
  var params = new URLSearchParams(location.search);
  var ids = params.get('v').split(','), index = 0, start = +(params.get('t') || 0);
  var player = null, ready = false, wantPlay = true, waiting = false, shown = ids[0];
  function send(message) { launcherito.postMessage(JSON.stringify(message)); }
  function openVideo(id) {
    shown = id;
    var video = { videoId: id, startSeconds: start };
    wantPlay ? player.loadVideoById(video) : player.cueVideoById(video);
  }
  function onYouTubeIframeAPIReady() {
    player = new YT.Player('player', {
      videoId: ids[0], width: '100%', height: '100%',
      playerVars: { autoplay: 0, playsinline: 1, rel: 0, controls: 0, disablekb: 1, fs: 0, iv_load_policy: 3, start: Math.floor(start) },
      events: {
        onReady: function () {
          ready = true;
          // No arranca solo: suena si Launcherito no lo ha pausado mientras cargaba.
          // Si mientras cargaba se ha pedido otra canción, se pone esa.
          if (shown !== ids[index]) openVideo(ids[index]);
          else if (wantPlay) player.playVideo();
        },
        onStateChange: function (e) { send({ type: 'state', state: e.data }); },
        // 101/150: el dueño no deja verlo fuera de YouTube; se prueba el siguiente resultado.
        onError: function () { ++index < ids.length ? openVideo(ids[index]) : send({ type: 'unavailable' }); },
      },
    });
    setInterval(function () {
      if (ready && !waiting && player.getCurrentTime)
        send({ type: 'time', time: player.getCurrentTime(), duration: player.getDuration() });
    }, 250);
  }
  // Órdenes de Launcherito.
  function load(list, from) { waiting = false; ids = list; index = 0; start = from; if (ready) openVideo(ids[0]); }
  function hold() { waiting = true; if (ready) player.stopVideo(); }
  function play() { wantPlay = true; if (ready && !waiting) player.playVideo(); }
  function pause() { wantPlay = false; if (ready) player.pauseVideo(); }
  function seek(seconds) { if (ready) player.seekTo(seconds, true); }
</script>
<script src="https://www.youtube.com/iframe_api" onerror="send({ type: 'unavailable' })"></script>
</body></html>"""
    }

    private val main = Handler(Looper.getMainLooper())
    private val queued = mutableListOf<String>()   // órdenes que llegan antes de que la página esté lista
    private var pageReady = false

    /** No se puede ver ninguno de los vídeos dentro de la app (o no hay conexión). */
    var onUnavailable: (() -> Unit)? = null

    /** Empieza a sonar el vídeo (también después de cada pausa o salto). */
    var onPlaying: (() -> Unit)? = null

    /** Ha llegado al final. */
    var onEnded: (() -> Unit)? = null

    /** Cada 250 ms: segundo actual y duración (0 mientras no se conoce). */
    var onProgress: ((time: Double, duration: Double) -> Unit)? = null

    /** Lanza una excepción si el móvil no tiene WebView (muy raro, pero posible). */
    val view: WebView = WebView(context.applicationContext).apply {
        setBackgroundColor(Color.BLACK)
        settings.javaScriptEnabled = true
        settings.domStorageEnabled = true
        // El "clic" que arranca el vídeo se da en Launcherito, no en la página: sin esto no sonaría.
        settings.mediaPlaybackRequiresUserGesture = false
        // El vídeo no se toca: lo manejan los controles de Launcherito.
        isFocusable = false
        @Suppress("ClickableViewAccessibility")
        setOnTouchListener { _, _ -> true }
        addJavascriptInterface(Bridge(), "launcherito")
        webViewClient = object : WebViewClient() {
            override fun shouldInterceptRequest(view: WebView, request: WebResourceRequest): WebResourceResponse? =
                if (request.url.host == HOST) WebResourceResponse("text/html", "utf-8", PAGE.byteInputStream())
                else null

            override fun onPageFinished(view: WebView, url: String) {
                if (Uri.parse(url).host != HOST) return
                pageReady = true
                queued.forEach { view.evaluateJavascript(it, null) }
                queued.clear()
            }

            // "Ver en YouTube" y demás enlaces del reproductor se abren fuera.
            override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean {
                if (!request.isForMainFrame || request.url.host == HOST) return false
                YouTubeLinks.open(view.context, request.url.toString())
                return true
            }
        }
    }

    /** Carga los vídeos (prueba el siguiente si uno no se deja insertar), empezando en el segundo [start]. */
    fun start(videoIds: List<String>, start: Double) {
        pageReady = false
        view.loadUrl("https://$HOST/?v=${videoIds.joinToString(",")}&t=${number(start)}")
    }

    /** Cambia a otros vídeos (otra canción) sin recargar la página. */
    fun load(videoIds: List<String>, start: Double) = run("load(${JSONArray(videoIds)}, ${number(start)})")

    /** Para el vídeo actual hasta el próximo [load] (mientras se busca el de otra canción). */
    fun hold() = run("hold()")

    fun play() = run("play()")

    fun pause() = run("pause()")

    fun seek(seconds: Double) = run("seek(${number(seconds)})")

    /** Libera el navegador (ocupa bastante memoria). */
    fun dispose() {
        onUnavailable = null
        onPlaying = null
        onEnded = null
        onProgress = null
        (view.parent as? ViewGroup)?.removeView(view)
        view.stopLoading()
        view.destroy()
    }

    private fun run(script: String) {
        if (pageReady) view.evaluateJavascript(script, null)
        else queued.add(script)   // se manda en cuanto la página termine de cargar
    }

    private fun number(value: Double) = String.format(Locale.ROOT, "%.3f", value)

    /** Mensajes de la página. Llegan en otro hilo: se pasan al principal. */
    private inner class Bridge {
        @JavascriptInterface
        fun postMessage(json: String) {
            main.post { onMessage(json) }
        }
    }

    private fun onMessage(json: String) {
        try {
            val message = JSONObject(json)
            when (message.getString("type")) {
                "unavailable" -> onUnavailable?.invoke()
                "state" -> when (message.getInt("state")) {
                    1 -> onPlaying?.invoke()
                    0 -> onEnded?.invoke()
                }
                "time" -> onProgress?.invoke(message.getDouble("time"), message.optDouble("duration", 0.0))
            }
        } catch (_: JSONException) {
            // Mensaje que no es de nuestra página: se ignora.
        }
    }
}
