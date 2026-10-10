package io.github.sergio120602.launcherito.data

import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.IOException
import java.util.concurrent.TimeUnit

/** Cliente HTTP compartido (Deezer, Spotify y YouTube). Las llamadas bloquean: hacerlas fuera del hilo principal. */
object Http {
    val client: OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .callTimeout(20, TimeUnit.SECONDS)
        .build()

    /** Texto de la respuesta. Lanza IOException si falla la red o la respuesta no es 2xx. */
    fun get(url: String, vararg headers: Pair<String, String>, client: OkHttpClient = this.client): String {
        val request = Request.Builder().url(url).apply { headers.forEach { header(it.first, it.second) } }.build()
        // use equivale al using de C#: la respuesta (y su conexión) se cierra al salir del bloque.
        client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) throw IOException("HTTP ${response.code}")
            return response.body.string()
        }
    }

    fun getBytes(url: String): ByteArray {
        client.newCall(Request.Builder().url(url).build()).execute().use { response ->
            if (!response.isSuccessful) throw IOException("HTTP ${response.code}")
            return response.body.bytes()
        }
    }
}
