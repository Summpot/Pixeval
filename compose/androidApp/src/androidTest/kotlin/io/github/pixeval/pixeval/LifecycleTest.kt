package io.github.pixeval.pixeval

import androidx.lifecycle.Lifecycle
import androidx.test.core.app.ActivityScenario
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import android.accessibilityservice.AccessibilityService
import android.view.accessibility.AccessibilityNodeInfo
import io.github.pixeval.mobile.localization.Messages
import org.junit.Assert.*
import org.junit.Test

class LifecycleTest {
    @Test fun systemBackReturnsFromSettingsWithoutClosingTheSession() = runBlocking {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val app = instrumentation.targetContext.applicationContext as PixevalApplication
        ActivityScenario.launch(MainActivity::class.java).use {
            val controller = app.session.controller.await()
            fun find(text: String, node: AccessibilityNodeInfo?): AccessibilityNodeInfo? {
                if (node == null) return null
                if (node.text?.toString() == text) return node
                for (index in 0 until node.childCount) find(text, node.getChild(index))?.let { return it }
                return null
            }
            suspend fun click(text: String) {
                val success = withTimeoutOrNull(5000) {
                    while (true) {
                        var target = find(text, instrumentation.uiAutomation.rootInActiveWindow)
                        while (target != null && !target.isClickable) target = target.parent
                        val clicked = target?.performAction(AccessibilityNodeInfo.ACTION_CLICK) == true
                        if (clicked) return@withTimeoutOrNull true
                        delay(50)
                    }
                }
                assertEquals("Could not click '$text' in the accessibility tree", true, success)
            }
            click("☰")
            click(Messages.text("MainPage.Tab-Settings", controller.state.value.locale))
            assertNotNull("Settings must register a system back handler", withTimeoutOrNull(5000) {
                while(app.session.backAction == null) delay(50)
                app.session.backAction
            })
            val network = Messages.text("AppSettings.SettingsGroup-Network-Header", controller.state.value.locale)
            withTimeout(5000) { while(find(network, instrumentation.uiAutomation.rootInActiveWindow) == null) delay(50) }
            assertTrue(instrumentation.uiAutomation.performGlobalAction(AccessibilityService.GLOBAL_ACTION_BACK))
            assertEquals("Back must return to the root page", true, withTimeoutOrNull(5000) {
                while(app.session.backAction != null) delay(50)
                true
            })
            assertSame(controller, app.session.controller.await())
            assertTrue(app.session.foreground.value)
        }
    }
    @Test fun recreationRetainsNativeSessionAndStoppingServiceEndsBackgroundLease() = runBlocking {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val app = instrumentation.targetContext.applicationContext as PixevalApplication
        instrumentation.uiAutomation.executeShellCommand("pm grant ${app.packageName} android.permission.POST_NOTIFICATIONS").close()
        ActivityScenario.launch(MainActivity::class.java).use { scenario ->
            val controller = app.session.controller.await()
            scenario.recreate()
            assertSame(controller, app.session.controller.await())
            withContext(Dispatchers.Main) { app.session.prepareDownload() }
            assertTrue(app.session.backgroundDownloads.value)
            scenario.moveToState(Lifecycle.State.CREATED)
            assertFalse(app.session.foreground.value)
            assertTrue(app.session.backgroundDownloads.value)
            withContext(Dispatchers.Main) { app.session.downloadQueued() }
            withTimeout(10_000) { while(app.session.backgroundDownloads.value) delay(50) }
            scenario.moveToState(Lifecycle.State.RESUMED)
            assertSame(controller, app.session.controller.await())
            assertTrue(app.session.foreground.value)
        }
    }
}
