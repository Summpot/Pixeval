package io.github.pixeval.bindings

import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.*
import kotlin.test.*
import uniffi.pixeval_download.DownloadFormatEncoder
import uniffi.pixeval_filters.*
import uniffi.pixeval_native.verifyMobileCallbacks
import uniffi.pixeval_native.UniffiStringException

class CallbackLifetimeTest {
    private val encoder = object : DownloadFormatEncoder {
        override fun encodeStaticImage(sourcePath: String, destinationPath: String, extension: String) = "done"
        override fun encodeAnimated(framePaths: List<String>, delaysMs: List<UInt>, destinationPath: String, extension: String) = "done"
        override fun encodeNovel(text: String, imagePaths: List<String>, destinationPath: String, extension: String) = "done"
    }
    @Test fun unexpectedCallbackExceptionsJoinAllWorkersAndBecomeAnError() = runBlocking {
        val attempts = AtomicInteger()
        val storage = object : FilterStorageProvider {
            override fun querySearchHistoryTags(pattern: String, limit: UInt): List<TagCandidate> {
                attempts.incrementAndGet()
                throw IllegalStateException("callback failure")
            }
            override fun querySubscriptionAuthors(pattern: String, limit: UInt) = emptyList<AuthorCandidate>()
        }
        val error = assertFailsWith<UniffiStringException> { verifyMobileCallbacks(storage, encoder) }
        assertEquals("Callback worker panicked", error.message)
        assertEquals(4, attempts.get())
    }
    @Test fun cancellingAFutureRetainsCallbacksUntilBlockedWorkersFinish() = runBlocking {
        repeat(10) {
            val entered = CountDownLatch(4)
            val release = CountDownLatch(1)
            val completed = AtomicInteger()
            val storage = object : FilterStorageProvider {
                override fun querySearchHistoryTags(pattern: String, limit: UInt): List<TagCandidate> {
                    entered.countDown()
                    check(release.await(10, TimeUnit.SECONDS))
                    return listOf(TagCandidate("retained", null))
                }
                override fun querySubscriptionAuthors(pattern: String, limit: UInt): List<AuthorCandidate> {
                    completed.incrementAndGet()
                    return emptyList()
                }
            }
            val pending = async(Dispatchers.Default) { verifyMobileCallbacks(storage, encoder) }
            try {
                assertTrue(withContext(Dispatchers.IO) { entered.await(10, TimeUnit.SECONDS) })
                withTimeout(5000) { pending.cancelAndJoin() }
            } finally { release.countDown() }
            withTimeout(5000) { while(completed.get() < 4) delay(5) }
            assertEquals(4, completed.get())
        }
    }
}
