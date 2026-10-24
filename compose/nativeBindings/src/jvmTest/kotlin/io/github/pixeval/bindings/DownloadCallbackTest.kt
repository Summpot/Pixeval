package io.github.pixeval.bindings

import com.sun.net.httpserver.HttpServer
import java.net.InetSocketAddress
import java.nio.file.Files
import java.util.concurrent.atomic.AtomicLong
import kotlinx.coroutines.*
import kotlin.test.*
import uniffi.pixeval_download.*

class DownloadCallbackTest {
    @Test fun rustDownloadWorkersPublishCompletedSnapshotsWithTheOriginalBytes() = runBlocking {
        val bytes = byteArrayOf(0, 1, 127, -128, -1, 42)
        val server = HttpServer.create(InetSocketAddress("127.0.0.1", 0), 0)
        server.createContext("/original.png") { exchange ->
            exchange.sendResponseHeaders(200, bytes.size.toLong())
            exchange.responseBody.use { it.write(bytes) }
        }
        server.start()
        val root = Files.createTempDirectory("pixeval-download-callback-")
        val manager = DownloadManager(1u, null, null)
        val finished = CompletableDeferred<DownloadItemSnapshot>()
        val callbackThread = AtomicLong()
        val callback = object : DownloadPageCallback {
            override fun onPageSnapshot(snapshot: DownloadPageSnapshot) {
                snapshot.ordinaryItems.firstOrNull()?.let { item ->
                    if (item.state == DownloadState.ERROR) finished.completeExceptionally(AssertionError(item.errorMessage))
                    if (item.state == DownloadState.COMPLETED) {
                        callbackThread.set(Thread.currentThread().threadId())
                        finished.complete(item)
                    }
                }
            }
        }
        try {
            manager.setPageCallback(callback)
            val destination = manager.enqueueExternalImage(ExternalImageRequest(
                artworkId = "9223372036854775807", title = "Unicode 日本語", author = "作者", thumbnailUrl = "",
                websiteUri = "", appUri = "", originalUrl = "http://127.0.0.1:${server.address.port}/original.png",
                pathMacro = "result.@{ext}", baseDir = root.toString(), payloadJson = "{}", serializeKey = "test",
                workSubscriptionId = 0, subscriptionType = "", isR18 = false, isR18g = false, createDate = "",
            ))
            val completed = withTimeout(15_000) { finished.await() }
            assertEquals(destination, completed.key.destination)
            assertEquals("Unicode 日本語", completed.title)
            assertContentEquals(bytes, Files.readAllBytes(java.nio.file.Path.of(completed.openDestination)))
            assertNotEquals(Thread.currentThread().threadId(), callbackThread.get())
        } finally {
            manager.setPageCallback(null)
            manager.destroy()
            server.stop(0)
            root.toFile().deleteRecursively()
        }
    }
}
