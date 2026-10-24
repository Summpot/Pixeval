package io.github.pixeval.bindings

import kotlinx.coroutines.runBlocking
import kotlin.test.*
import uniffi.pixeval_download.DownloadFormatEncoder
import uniffi.pixeval_filters.*
import uniffi.pixeval_native.verifyMobileCallbacks

class CallbackTest {
    private val storage = object : FilterStorageProvider {
        override fun querySearchHistoryTags(pattern: String, limit: UInt): List<TagCandidate> {
            assertEquals("probe", pattern)
            assertEquals(8u, limit)
            return listOf(TagCandidate("日本語", null), TagCandidate("tag", "translated"))
        }
        override fun querySubscriptionAuthors(pattern: String, limit: UInt) = listOf(AuthorCandidate("作者", null))
    }
    private val encoder = object : DownloadFormatEncoder {
        override fun encodeStaticImage(sourcePath: String, destinationPath: String, extension: String): String {
            assertEquals("source", sourcePath)
            assertEquals("destination", destinationPath)
            return "returned:$extension"
        }
        override fun encodeAnimated(framePaths: List<String>, delaysMs: List<UInt>, destinationPath: String, extension: String) = "animation"
        override fun encodeNovel(text: String, imagePaths: List<String>, destinationPath: String, extension: String) = "novel"
    }

    @Test fun callbacksReturnCollectionsAndStringsFromRustWorkers() = runBlocking {
        repeat(10) {
            val report = verifyMobileCallbacks(storage, encoder)
            assertEquals(4u, report.completedWorkers)
            assertEquals(List(4) { listOf("日本語", "tag") }.flatten(), report.tagNames)
            assertEquals(List(4) { "作者" }, report.authorNames)
            assertEquals("returned:probe", report.encoderResult)
        }
    }
}
