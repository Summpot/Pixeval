package io.github.pixeval.mobile

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import kotlinx.coroutines.channels.Channel
import uniffi.pixeval_cache.ImageCache
import uniffi.pixeval_cache.CachePreviewCallback
import uniffi.pixeval_cache.DecodedPreviewFrame

internal class ImageRepository(private val native: ImageCache, budgetBytes: Long) {
    private val budget = budgetBytes.coerceAtLeast(1024L * 1024)
    private val mutex = Mutex()
    private val decoding = Semaphore(4)
    private val decoded = linkedMapOf<Pair<String, Int>, ImageBitmap>()
    private var bytes = 0L
    suspend fun load(url: String, width: Int, preview: (DecodedPreviewFrame) -> Unit = {}): ImageBitmap {
        val key = url to width
        mutex.withLock { decoded.remove(key)?.let { decoded[key] = it; return it } }
        val image = decoding.withPermit {
            withContext(Dispatchers.IO) {
                val callback = object : CachePreviewCallback {
                    override fun onPreviewFrame(frame: DecodedPreviewFrame) { preview(frame) }
                    override fun onProgress(downloadedBytes: ULong, totalBytes: ULong) {}
                }
                decodeImage(native.getOrFetch(url, null, callback), width)
            }
        }
        currentCoroutineContext().ensureActive()
        val cost = image.width.toLong() * image.height * 4
        if (cost <= budget) mutex.withLock {
            decoded.remove(key)?.let { bytes -= it.width.toLong() * it.height * 4 }
            while (decoded.isNotEmpty() && (bytes + cost > budget || decoded.size >= 150)) {
                val first = decoded.entries.first()
                bytes -= first.value.width.toLong() * first.value.height * 4
                decoded.remove(first.key)
            }
            decoded[key] = image
            bytes += cost
        }
        return image
    }
}

@Composable
internal fun RustImage(repository: ImageRepository, url: String?, description: String, modifier: Modifier = Modifier, width: Int = 480) {
    var failed by remember(url, width) { mutableStateOf(false) }
    val bitmap by produceState<ImageBitmap?>(null, repository, url, width) {
        if (url != null) {
            val previews = Channel<DecodedPreviewFrame>(Channel.CONFLATED)
            val collector = launch {
                for(frame in previews) {
                    val image = withContext(Dispatchers.Default) {
                        runCatching {
                            require(frame.width > 0u && frame.height > 0u && frame.width.toLong() * frame.height.toLong() * 4 == frame.bgraData.size.toLong())
                            decodePreview(frame)
                        }.getOrNull()
                    }
                    if (image != null) value = image
                }
            }
            try {
                val final = repository.load(url, width) { previews.trySend(it) }
                collector.cancelAndJoin()
                value = final
            }
            catch (cancelled: CancellationException) { throw cancelled }
            catch (_: Exception) { failed = true }
            finally { previews.close(); collector.cancelAndJoin() }
        }
    }
    Box(modifier, contentAlignment = Alignment.Center) {
        bitmap?.let { Image(it, description, Modifier.matchParentSize(), contentScale = ContentScale.Fit) }
            ?: if (failed || url == null) Text(description) else CircularProgressIndicator(Modifier.size(24.dp))
    }
}
