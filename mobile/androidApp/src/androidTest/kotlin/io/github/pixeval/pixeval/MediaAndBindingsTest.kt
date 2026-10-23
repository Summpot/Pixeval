package io.github.pixeval.pixeval

import android.graphics.Bitmap
import android.graphics.Color
import android.media.MediaExtractor
import android.media.MediaMetadataRetriever
import androidx.test.platform.app.InstrumentationRegistry
import io.github.pixeval.mobile.AndroidVideoEncoder
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Test
import uniffi.pixeval_download.DownloadFormatEncoder
import uniffi.pixeval_filters.*
import uniffi.pixeval_native.verifyMobileCallbacks
import java.io.File

class MediaAndBindingsTest {
    @Test fun mp4KeepsFrameTimestampsAndFinalDelay() {
        val root = File(InstrumentationRegistry.getInstrumentation().targetContext.cacheDir, "mp4-test-${System.nanoTime()}").apply { mkdirs() }
        try {
            val frames = listOf(Color.RED, Color.GREEN, Color.BLUE).mapIndexed { index, color ->
                root.resolve("$index.png").also { file ->
                    val image = Bitmap.createBitmap(64, 64, Bitmap.Config.ARGB_8888)
                    try { image.eraseColor(color); file.outputStream().use { image.compress(Bitmap.CompressFormat.PNG, 100, it) } }
                    finally { image.recycle() }
                }.absolutePath
            }
            val output = root.resolve("result.mp4")
            assertEquals("", AndroidVideoEncoder().encode(frames, listOf(40, 140, 320), output.absolutePath))
            val extractor = MediaExtractor()
            try {
                extractor.setDataSource(output.absolutePath)
                assertEquals(1, extractor.trackCount)
                extractor.selectTrack(0)
                val times = mutableListOf<Long>()
                while (extractor.sampleTime >= 0) { times += extractor.sampleTime; extractor.advance() }
                assertEquals(listOf(0L, 40_000L, 180_000L), times)
            } finally { extractor.release() }
            val reader = MediaMetadataRetriever()
            try {
                reader.setDataSource(output.absolutePath)
                assertEquals(500L, reader.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION)!!.toLong())
                val decoded = reader.getFrameAtTime(180_000, MediaMetadataRetriever.OPTION_CLOSEST)!!
                try { assertTrue(Color.blue(decoded.getPixel(32, 32)) > 200) }
                finally { decoded.recycle() }
            } finally { reader.release() }
        } finally { root.deleteRecursively() }
    }
    @Test fun invalidMediaDoesNotLeaveACompletedFile() {
        val output = File(InstrumentationRegistry.getInstrumentation().targetContext.cacheDir, "invalid-${System.nanoTime()}.mp4")
        assertTrue(AndroidVideoEncoder().encode(emptyList(), emptyList(), output.absolutePath).isNotBlank())
        assertFalse(output.exists())
    }
    @Test fun actualAndroidNativeWorkersReceiveCallbackResults() = runBlocking {
        val storage = object : FilterStorageProvider {
            override fun querySearchHistoryTags(pattern: String, limit: UInt) = listOf(TagCandidate("日本語", null))
            override fun querySubscriptionAuthors(pattern: String, limit: UInt) = listOf(AuthorCandidate("作者", null))
        }
        val encoder = object : DownloadFormatEncoder {
            override fun encodeStaticImage(sourcePath: String, destinationPath: String, extension: String) = "Android result"
            override fun encodeAnimated(framePaths: List<String>, delaysMs: List<UInt>, destinationPath: String, extension: String) = "unused"
            override fun encodeNovel(text: String, imagePaths: List<String>, destinationPath: String, extension: String) = "unused"
        }
        repeat(10) {
            val report = verifyMobileCallbacks(storage, encoder)
            assertEquals(4u, report.completedWorkers)
            assertEquals(List(4) { "日本語" }, report.tagNames)
            assertEquals("Android result", report.encoderResult)
        }
    }
}
