package io.github.sergio120602.launcherito.data

import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import java.util.concurrent.TimeUnit

/**
 * La «tarea programada» del móvil: cada día a las 7:00 renueva las playlists del momento
 * ([TrendingPlaylists]) aunque la app esté cerrada. Se hace con WorkManager, que sigue programado
 * tras reiniciar el móvil y espera a que haya conexión. Al terminar, se programa para el día siguiente
 * a las 7:00 (así la hora no se va desplazando como con un trabajo periódico).
 */
class TrendingWorker(context: Context, params: WorkerParameters) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result {
        val result = try {
            TrendingPlaylists.refresh(applicationContext)
            Result.success()
        } catch (_: ListImportException) {
            // Sin conexión o Spotify no responde: WorkManager lo reintenta un poco más tarde.
            if (runAttemptCount < MAX_ATTEMPTS) return Result.retry() else Result.failure()
        }
        schedule(applicationContext, replace = true)
        return result
    }

    companion object {
        private const val WORK_NAME = "playlists-del-momento"
        private const val MAX_ATTEMPTS = 5

        /**
         * Programa la próxima actualización (hoy a las 7:00 si aún no han llegado, si no mañana). Con
         * [replace] false no toca la que ya esté programada (lo que hace la app cada vez que se abre).
         */
        fun schedule(context: Context, replace: Boolean = false) {
            val now = System.currentTimeMillis()
            val request = OneTimeWorkRequestBuilder<TrendingWorker>()
                .setInitialDelay(TrendingPlaylists.nextRefreshTime(now) - now, TimeUnit.MILLISECONDS)
                .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
                .build()
            WorkManager.getInstance(context).enqueueUniqueWork(
                WORK_NAME,
                // Desde el propio trabajo, la siguiente va detrás de él (sin cancelarlo mientras termina).
                if (replace) ExistingWorkPolicy.APPEND_OR_REPLACE else ExistingWorkPolicy.KEEP,
                request,
            )
        }
    }
}
