package io.github.sergio120602.launcherito.data

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.content.FileProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.Request
import org.json.JSONException
import org.json.JSONObject
import java.io.File
import java.io.IOException
import java.util.concurrent.TimeUnit

/**
 * Una versión publicada en GitHub más nueva que la instalada.
 *
 * @param version número, sin la "v" ("1.12").
 * @param title título de la versión ("v1.12 - Barra de reproducción").
 * @param apkUrl el .apk de esa versión.
 * @param pageUrl página de la versión en GitHub, para descargarla a mano si hace falta.
 */
data class AppUpdate(val version: String, val title: String, val apkUrl: String, val pageUrl: String)

/** No se ha podido buscar o descargar la actualización; el mensaje se muestra tal cual. */
class UpdateException(message: String) : Exception(message)

/**
 * Actualizaciones desde las versiones publicadas en GitHub (Releases), que llevan el .apk. Se
 * descarga en la caché de la app y se abre el instalador de Android, que pide confirmación (una
 * app no puede actualizarse sola sin preguntar). La primera vez Android pide además permitir que
 * Launcherito instale apps.
 */
object AppUpdater {
    private const val REPO = "https://github.com/sergio120602/Proyectito_Launcherito"
    private const val LATEST_API = "https://api.github.com/repos/sergio120602/Proyectito_Launcherito/releases/latest"
    const val RELEASES_PAGE = "$REPO/releases/latest"

    // Sin seguir redirecciones: para ver a qué versión lleva la página «latest».
    private val noRedirect = Http.client.newBuilder().followRedirects(false).followSslRedirects(false).build()
    // La descarga (unos MB) puede tardar más que las consultas.
    private val downloader = Http.client.newBuilder().callTimeout(10, TimeUnit.MINUTES).readTimeout(60, TimeUnit.SECONDS).build()

    /** Versión instalada ("1.11"), la del archivo VERSION con que se compiló. */
    fun currentVersion(context: Context): String =
        context.packageManager.getPackageInfo(context.packageName, 0).versionName ?: "0"

    /** La última versión publicada si es más nueva que la instalada; null si ya se tiene la última. */
    @Throws(UpdateException::class)
    suspend fun check(context: Context): AppUpdate? = withContext(Dispatchers.IO) {
        val current = parse(currentVersion(context)) ?: listOf(0)
        val latest = try {
            checkWithApi()
        } catch (_: IOException) {
            null
        } catch (_: JSONException) {
            null
        } ?: checkWithRedirect()   // la API admite 60 consultas por hora desde una conexión sin cuenta
        val version = parse(latest.version) ?: return@withContext null
        if (compare(version, current) > 0) latest else null
    }

    private fun checkWithApi(): AppUpdate? {
        val root = JSONObject(Http.get(LATEST_API, "User-Agent" to "Launcherito", "Accept" to "application/vnd.github+json"))
        val tag = root.optString("tag_name")
        val assets = root.optJSONArray("assets") ?: return null
        for (i in 0 until assets.length()) {
            val asset = assets.optJSONObject(i) ?: continue
            if (asset.optString("name").endsWith(".apk", ignoreCase = true)) {
                return AppUpdate(
                    tag.trimStart('v', 'V'), root.optString("name").ifEmpty { tag },
                    asset.optString("browser_download_url"), root.optString("html_url").ifEmpty { RELEASES_PAGE },
                )
            }
        }
        return null
    }

    /** Sin la API: «latest» lleva a …/releases/tag/v1.12, y el .apk se llama Launcherito-v1.12.apk. */
    private fun checkWithRedirect(): AppUpdate {
        val request = Request.Builder().url(RELEASES_PAGE).head().header("User-Agent", "Launcherito").build()
        val location = try {
            noRedirect.newCall(request).execute().use { it.header("Location") }
        } catch (_: IOException) {
            null
        } ?: throw UpdateException("No se puede comprobar si hay versiones nuevas. Comprueba la conexión a Internet.")
        val tag = location.substringAfterLast('/')
        return AppUpdate(tag.trimStart('v', 'V'), tag, "$REPO/releases/download/$tag/Launcherito-$tag.apk", "$REPO/releases/tag/$tag")
    }

    /**
     * Descarga el .apk en la caché (bloquea; llamarlo con un hilo de fondo). [progress] va de 0 a 1.
     */
    @Throws(UpdateException::class)
    suspend fun download(context: Context, update: AppUpdate, progress: (Float) -> Unit): File = withContext(Dispatchers.IO) {
        val folder = File(context.cacheDir, "updates").apply { mkdirs() }
        folder.listFiles()?.forEach { it.delete() }   // descargas anteriores
        val file = File(folder, "Launcherito-${update.version}.apk")
        try {
            val request = Request.Builder().url(update.apkUrl).header("User-Agent", "Launcherito").build()
            downloader.newCall(request).execute().use { response ->
                if (!response.isSuccessful) {
                    throw UpdateException("La versión ${update.version} no trae el .apk. Descárgala a mano desde GitHub.")
                }
                val body = response.body
                val total = body.contentLength()
                body.byteStream().use { input ->
                    file.outputStream().use { output ->
                        val buffer = ByteArray(64 * 1024)
                        var done = 0L
                        while (true) {
                            val read = input.read(buffer)
                            if (read < 0) break
                            output.write(buffer, 0, read)
                            done += read
                            if (total > 0) progress(done.toFloat() / total)
                        }
                    }
                }
            }
            file
        } catch (e: IOException) {
            file.delete()
            throw UpdateException("Se ha cortado la descarga. Comprueba la conexión e inténtalo otra vez.")
        }
    }

    /**
     * Si el .apk está firmado con la misma clave que la app instalada. Android solo deja actualizar
     * con la misma firma; si no, el instalador dice «conflicto con un paquete existente» y no hace
     * nada. Si no se puede saber, se deja intentar.
     */
    fun sameSigner(context: Context, apk: File): Boolean = try {
        val pm = context.packageManager
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            val installed = pm.getPackageInfo(context.packageName, PackageManager.GET_SIGNING_CERTIFICATES)
                .signingInfo?.apkContentsSigners?.map { it.toCharsString() }?.toSet()
            val downloaded = pm.getPackageArchiveInfo(apk.path, PackageManager.GET_SIGNING_CERTIFICATES)
                ?.signingInfo?.apkContentsSigners?.map { it.toCharsString() }?.toSet()
            installed == null || downloaded == null || installed == downloaded
        } else {
            @Suppress("DEPRECATION")
            val installed = pm.getPackageInfo(context.packageName, PackageManager.GET_SIGNATURES).signatures?.map { it.toCharsString() }?.toSet()
            @Suppress("DEPRECATION")
            val downloaded = pm.getPackageArchiveInfo(apk.path, PackageManager.GET_SIGNATURES)?.signatures?.map { it.toCharsString() }?.toSet()
            installed == null || downloaded == null || installed == downloaded
        }
    } catch (_: Exception) {
        true
    }

    /** Si Android ya deja a Launcherito instalar apps (se concede una vez, en Ajustes). */
    fun canInstall(context: Context) = context.packageManager.canRequestPackageInstalls()

    /** Abre en Ajustes el permiso de instalar apps de Launcherito. */
    fun openInstallPermission(context: Context) {
        val intent = Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${context.packageName}"))
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        try {
            context.startActivity(intent)
        } catch (_: ActivityNotFoundException) {
        }
    }

    /** Abre el instalador de Android con el .apk descargado. */
    fun install(context: Context, apk: File) {
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.updates", apk)
        val intent = Intent(Intent.ACTION_VIEW)
            .setDataAndType(uri, "application/vnd.android.package-archive")
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
        context.startActivity(intent)
    }

    /** "v1.12" o "1.10.1" → [1, 12] o [1, 10, 1]; null si no es un número de versión. */
    private fun parse(text: String): List<Int>? =
        text.trim().trimStart('v', 'V').split('.').map { it.toIntOrNull() ?: return null }.takeIf { it.isNotEmpty() }

    /** Compara parte a parte; las que faltan cuentan como 0 ("1.11" = "1.11.0"). */
    private fun compare(a: List<Int>, b: List<Int>): Int {
        for (i in 0 until maxOf(a.size, b.size)) {
            val diff = a.getOrElse(i) { 0 } - b.getOrElse(i) { 0 }
            if (diff != 0) return diff
        }
        return 0
    }
}
