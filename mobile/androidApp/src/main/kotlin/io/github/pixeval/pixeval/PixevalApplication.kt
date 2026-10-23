package io.github.pixeval.pixeval

import android.app.Application
import android.content.Intent
import android.net.Uri
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import io.github.pixeval.mobile.*
import kotlinx.coroutines.*
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import uniffi.pixeval_native.MobileDirectories
import java.io.File
import java.lang.ref.WeakReference
import java.util.Locale
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream

/** A process owns exactly one native session; activities only attach UI. */
class PixevalApplication : Application() {
    val session by lazy { AndroidSession(this) }
}

class AndroidSession(private val app: PixevalApplication) : PlatformServices {
    val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private var activity = WeakReference<MainActivity>(null)
    var backAction: (() -> Unit)? = null
        private set
    private val links = Channel<String>(Channel.UNLIMITED)
    override val foreground = MutableStateFlow(false)
    override val backgroundDownloads = MutableStateFlow(false)
    override val directories = MobileDirectories(app.filesDir.resolve("profile").absolutePath,
        app.cacheDir.resolve("images").absolutePath, app.filesDir.resolve("downloads").absolutePath)
    override val systemLocale get() = Locale.getDefault().toLanguageTag()
    override val incomingLinks = links.receiveAsFlow()
    override val videoEncoder = AndroidVideoEncoder()
    override val imageMemoryBudgetBytes = (java.lang.Runtime.getRuntime().maxMemory() / 8).coerceIn(8L * 1024 * 1024, 64L * 1024 * 1024)
    val controller = scope.async(start = CoroutineStart.LAZY) { AppController.open(this@AndroidSession) }
    private val exportLock = Mutex()
    private var exportResult: CompletableDeferred<Uri?>? = null
    private val serviceLock = Mutex()
    private var serviceReady = CompletableDeferred<Unit>()
    private val serviceEnding = MutableStateFlow(false)
    var preparingDownloads = 0
        private set

    fun attach(host: MainActivity) { activity = WeakReference(host); controller.start() }
    fun detach(host: MainActivity) { if (activity.get() === host) activity.clear() }
    override fun setBackHandler(handler: (() -> Unit)?) {
        backAction = handler
        activity.get()?.updateBackHandler(handler != null)
    }
    fun accept(intent: Intent?) {
        val value = intent?.dataString ?: intent?.getStringExtra(Intent.EXTRA_TEXT)
        if (!value.isNullOrBlank()) links.trySend(value)
    }
    private fun host() = activity.get() ?: error("Open Pixeval to use this action")
    override fun openUrl(url: String) { host().startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) }
    override fun shareText(text: String) {
        host().startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).apply {
            type = "text/plain"; putExtra(Intent.EXTRA_TEXT, text)
        }, null))
    }
    override suspend fun prepareDownload() {
        serviceLock.withLock {
            check(foreground.value) { "Start or resume downloads while Pixeval is open" }
            if (serviceEnding.value) backgroundDownloads.first { !it }
            preparingDownloads++
            try {
                if (!backgroundDownloads.value) {
                    serviceReady = CompletableDeferred()
                    host().requestNotifications()
                    ContextCompat.startForegroundService(app, Intent(app, DownloadService::class.java))
                    withTimeout(10_000) { serviceReady.await() }
                }
            } catch (failure: Exception) { preparingDownloads--; throw failure }
        }
    }
    override fun downloadQueued() { preparingDownloads = (preparingDownloads - 1).coerceAtLeast(0) }
    fun serviceStarted() { serviceEnding.value = false; backgroundDownloads.value = true; serviceReady.complete(Unit) }
    fun serviceStopping() { serviceEnding.value = true }
    fun serviceStopped() { backgroundDownloads.value = false; serviceEnding.value = false }
    fun finishExport(uri: Uri?) { exportResult?.complete(uri) }
    override suspend fun exportFile(path: String) = exportLock.withLock {
        val file = withContext(Dispatchers.IO) { transferableFile(path) }
        val result = CompletableDeferred<Uri?>()
        exportResult = result
        try {
            host().createDocument(file.name, mimeType(file))
            val uri = result.await() ?: return@withLock
            withContext(Dispatchers.IO) {
                app.contentResolver.openOutputStream(uri, "wt")?.use { output ->
                    file.inputStream().use { it.copyTo(output) }
                } ?: error("The selected document is no longer writable; choose a new destination")
            }
        } finally { exportResult = null }
    }
    override suspend fun shareFile(path: String) {
        val file = withContext(Dispatchers.IO) { transferableFile(path) }
        val uri = FileProvider.getUriForFile(app, "${app.packageName}.files", file)
        host().startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).apply {
            type = mimeType(file)
            putExtra(Intent.EXTRA_STREAM, uri)
            clipData = android.content.ClipData.newRawUri(file.name, uri)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }, null))
    }
    private fun transferableFile(path: String): File {
        val root = File(directories.downloads).canonicalFile
        val source = File(path).canonicalFile
        require(source.path.startsWith(root.path + File.separator) && source.exists()) { "The completed download is unavailable" }
        if (source.isFile) return source
        val exports = app.cacheDir.resolve("exports").apply { mkdirs() }
        val zip = File.createTempFile("${source.name.take(60)}-", ".zip", exports)
        try {
            ZipOutputStream(zip.outputStream().buffered()).use { output ->
                source.walkTopDown().filter { it.isFile }.forEach { file ->
                    require(file.canonicalPath.startsWith(root.path + File.separator)) { "Invalid download path" }
                    output.putNextEntry(ZipEntry(file.relativeTo(source).invariantSeparatorsPath))
                    file.inputStream().use { it.copyTo(output) }; output.closeEntry()
                }
            }
        } catch (failure: Exception) { zip.delete(); throw failure }
        return zip
    }
    private fun mimeType(file: File) = android.webkit.MimeTypeMap.getSingleton()
        .getMimeTypeFromExtension(file.extension.lowercase(Locale.ROOT)) ?: "application/octet-stream"
}
