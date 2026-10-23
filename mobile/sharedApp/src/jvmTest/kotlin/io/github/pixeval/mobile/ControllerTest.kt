package io.github.pixeval.mobile

import kotlinx.coroutines.*
import kotlinx.coroutines.test.*
import uniffi.pixeval_native.MobileDirectories
import java.nio.file.Files
import kotlin.test.*

@OptIn(ExperimentalCoroutinesApi::class)
class ControllerTest {
    @Test fun concurrentSettingsKeepIndependentFieldsAndLogoutRetainsPreferences() = runTest {
        Dispatchers.setMain(UnconfinedTestDispatcher(testScheduler))
        val root = Files.createTempDirectory("pixeval-controller-")
        val platform = object : PlatformServices {
            override val directories = MobileDirectories(root.resolve("data").toString(), root.resolve("cache").toString(), root.resolve("downloads").toString())
            override val systemLocale = "zh-Hans"
            override fun openUrl(url: String) {}
            override fun shareText(text: String) {}
        }
        val controller = AppController.open(platform)
        try {
            val theme = controller.setDarkTheme(true)
            val locale = controller.setLocale("en-US")
            joinAll(theme, locale)
            assertTrue(controller.state.value.darkTheme)
            assertEquals("en-US", controller.state.value.locale)
            controller.logout().join()
            assertTrue(controller.state.value.darkTheme)
            assertEquals("en-US", controller.state.value.preferences!!.language)
            assertNull(controller.state.value.user)
            assertTrue(controller.state.value.works.isEmpty())
            assertNull(controller.state.value.novelContent)
            controller.changePreferences { it.copy(downloadConcurrency = 0u) }.join()
            assertNotNull(controller.state.value.error)
            assertEquals(3u, controller.runtime.preferences().downloadConcurrency)
        } finally {
            controller.close()
            root.toFile().deleteRecursively()
            Dispatchers.resetMain()
        }
    }
}
