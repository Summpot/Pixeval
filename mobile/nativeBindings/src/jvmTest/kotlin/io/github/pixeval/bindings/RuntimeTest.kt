package io.github.pixeval.bindings

import java.nio.file.Files
import kotlinx.coroutines.*
import kotlin.test.*
import uniffi.pixeval_native.*

class RuntimeTest {
    @Test fun stringErrorsKeepTheirOriginalMessage() {
        val error = assertFailsWith<UniffiStringException> {
            MobileRuntime.open(MobileDirectories("relative", "relative", "relative"))
        }
        assertEquals("Mobile directories must be nonempty absolute paths", error.message)
    }

    @Test fun nativeProfilesCannotBeReplacedWhileOwned() = runBlocking {
        val root = Files.createTempDirectory("pixeval-mobile-runtime-")
        val paths = MobileDirectories(root.resolve("data").toString(), root.resolve("cache").toString(), root.resolve("downloads").toString())
        val runtime = withContext(Dispatchers.IO) { MobileRuntime.open(paths) }
        try {
            val reopened = MobileRuntime.open(paths)
            reopened.destroy()
            assertFailsWith<UniffiStringException> {
                MobileRuntime.open(paths.copy(data = root.resolve("other").toString()))
            }
            val receiver = runtime.sessionEvents()
            try {
                val waiting = async { receiver.next() }
                yield()
                receiver.cancel()
                assertNull(withTimeout(5000) { waiting.await() })
            } finally { receiver.destroy() }
            val capabilities = runtime.capabilities()
            assertFalse(capabilities.dynamicDotnetPlugins)
            assertFalse(capabilities.desktopUpdater)
        } finally {
            withContext(Dispatchers.IO) { runtime.destroy() }
            root.toFile().deleteRecursively()
        }
    }
}
