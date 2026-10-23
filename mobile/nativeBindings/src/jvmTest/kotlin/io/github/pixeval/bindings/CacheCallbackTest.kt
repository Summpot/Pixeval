package io.github.pixeval.bindings

import com.sun.net.httpserver.HttpServer
import java.net.InetSocketAddress
import java.nio.file.Files
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicLong
import kotlinx.coroutines.runBlocking
import kotlin.test.*
import uniffi.pixeval_cache.CachePreviewCallback
import uniffi.pixeval_cache.DecodedPreviewFrame
import uniffi.pixeval_cache.ImageCache

class CacheCallbackTest {
    @Test fun rustCacheStreamsProgressiveFramesAndReusesOriginalBytes() = runBlocking {
        val bytes = javaClass.getResourceAsStream("/progressive.jpg")!!.use { it.readBytes() }
        val requests = AtomicInteger()
        val server = HttpServer.create(InetSocketAddress("127.0.0.1", 0), 0)
        server.createContext("/progressive.jpg") { exchange ->
            requests.incrementAndGet()
            exchange.sendResponseHeaders(200, bytes.size.toLong())
            exchange.responseBody.use { stream ->
                var offset = 0
                while (offset < bytes.size) {
                    val count = minOf(256, bytes.size - offset)
                    stream.write(bytes, offset, count); stream.flush()
                    offset += count
                    Thread.sleep(30)
                }
            }
        }
        server.start()
        val root = Files.createTempDirectory("pixeval-cache-callback-")
        val cache = ImageCache(root.toString(), 16uL * 1024uL * 1024uL)
        val previews = AtomicInteger()
        val progress = AtomicLong()
        val callback = object : CachePreviewCallback {
            override fun onPreviewFrame(frame: DecodedPreviewFrame) {
                assertTrue(frame.width > 0u && frame.height > 0u)
                assertEquals(frame.width.toLong() * frame.height.toLong() * 4, frame.bgraData.size.toLong())
                previews.incrementAndGet()
            }
            override fun onProgress(downloadedBytes: ULong, totalBytes: ULong) {
                assertTrue(downloadedBytes <= totalBytes)
                progress.set(downloadedBytes.toLong())
            }
        }
        try {
            val url = "http://127.0.0.1:${server.address.port}/progressive.jpg"
            assertContentEquals(bytes, cache.getOrFetch(url, null, callback))
            assertTrue(previews.get() > 0)
            assertEquals(bytes.size.toLong(), progress.get())
            assertContentEquals(bytes, cache.getOrFetch(url, null, callback))
            assertEquals(1, requests.get())
        } finally {
            cache.closeNative(); cache.destroy()
            server.stop(0)
            root.toFile().deleteRecursively()
        }
    }
}
