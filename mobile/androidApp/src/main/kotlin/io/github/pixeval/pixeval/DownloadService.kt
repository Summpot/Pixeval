package io.github.pixeval.pixeval

import android.app.*
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import kotlinx.coroutines.*
import uniffi.pixeval_download.DownloadState
import io.github.pixeval.mobile.localization.Messages

/** Only user initiated work starts this service. Queue checkpoints live in Rust storage. */
class DownloadService : Service() {
    private val session get() = (application as PixevalApplication).session
    private var monitor: Job? = null
    override fun onCreate() {
        super.onCreate()
        if (Build.VERSION.SDK_INT >= 26) getSystemService(NotificationManager::class.java)
            .createNotificationChannel(NotificationChannel(CHANNEL, Messages.text("Mobile.DownloadChannel", session.systemLocale), NotificationManager.IMPORTANCE_LOW))
        ServiceCompat.startForeground(this, NOTIFICATION, notification(0),
            if (Build.VERSION.SDK_INT >= 29) ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC else 0)
        session.serviceStarted()
    }
    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == STOP) { pauseAndStop(); return START_NOT_STICKY }
        if (monitor?.isActive != true) monitor = session.scope.launch {
            val controller = session.controller.await()
            while (isActive) {
                val snapshot = withContext(Dispatchers.IO) { controller.downloads.currentPageSnapshot() }
                val active = (snapshot.ordinaryItems + snapshot.folders.flatMap { it.items }).count {
                    it.state == DownloadState.QUEUED || it.state == DownloadState.RUNNING || it.isProcessing
                }
                val syncing = withContext(Dispatchers.IO) { controller.subscriptions.isSyncInProgress() || controller.subscriptions.getPendingCount() > 0u }
                if (active == 0 && !syncing && snapshot.folders.none { it.isFetching } && session.preparingDownloads == 0) {
                    session.serviceStopping(); stopSelf(); break
                }
                getSystemService(NotificationManager::class.java).notify(NOTIFICATION, notification(active))
                delay(1000)
            }
        }
        return START_NOT_STICKY
    }
    private fun notification(count: Int): Notification {
        val flags = PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        val open = PendingIntent.getActivity(this, 0, Intent(this, MainActivity::class.java), flags)
        val stop = PendingIntent.getService(this, 1, Intent(this, DownloadService::class.java).setAction(STOP), flags)
        return NotificationCompat.Builder(this, CHANNEL).setSmallIcon(android.R.drawable.stat_sys_download)
            .setContentTitle("Pixeval").setContentText(Messages.text("Mobile.ActiveDownloads", session.systemLocale, mapOf("count" to count.toString())))
            .setContentIntent(open).setOngoing(true).setOnlyAlertOnce(true)
            .addAction(android.R.drawable.ic_media_pause, Messages.text("Misc.Pause", session.systemLocale), stop).build()
    }
    private fun pauseAndStop() {
        monitor?.cancel()
        session.serviceStopping()
        stopSelf()
    }
    override fun onTimeout(startId: Int, fgsType: Int) { pauseAndStop() }
    override fun onDestroy() {
        monitor?.cancel()
        session.serviceStopping()
        session.scope.launch {
            try { session.controller.await().suspendApp().join() }
            finally { session.serviceStopped() }
        }
        super.onDestroy()
    }
    override fun onBind(intent: Intent?): IBinder? = null
    companion object {
        private const val CHANNEL = "downloads"
        private const val NOTIFICATION = 1
        private const val STOP = "io.github.pixeval.pixeval.PAUSE_DOWNLOADS"
    }
}
