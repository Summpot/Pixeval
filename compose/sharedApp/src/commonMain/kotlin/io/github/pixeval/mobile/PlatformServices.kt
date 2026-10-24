package io.github.pixeval.mobile

import androidx.compose.ui.graphics.ImageBitmap
import uniffi.pixeval_native.MobileDirectories
import kotlinx.coroutines.flow.*

interface PlatformServices {
    val directories: MobileDirectories
    val systemLocale: String
    val incomingLinks: Flow<String> get() = emptyFlow()
    val foreground: Flow<Boolean> get() = flowOf(true)
    val backgroundDownloads: Flow<Boolean> get() = flowOf(false)
    val videoEncoder: VideoEncoder? get() = null
    val imageMemoryBudgetBytes: Long get() = 64L * 1024 * 1024
    suspend fun prepareDownload() {}
    fun downloadQueued() {}
    fun setBackHandler(handler: (() -> Unit)?) {}
    suspend fun exportFile(path: String) { error("File export is unavailable on this host") }
    suspend fun shareFile(path: String) { error("File sharing is unavailable on this host") }
    suspend fun saveToGallery(path: String) { error("Saving to gallery is unavailable on this host") }
    fun openUrl(url: String)
    fun shareText(text: String)
}

/** Synchronous worker-thread service. An empty result means a complete MP4 file. */
interface VideoEncoder {
    fun encode(framePaths: List<String>, delaysMs: List<Long>, destination: String): String
}

internal expect fun decodeImage(bytes: ByteArray, width: Int): ImageBitmap
internal expect fun decodePreview(frame: uniffi.pixeval_cache.DecodedPreviewFrame): ImageBitmap
