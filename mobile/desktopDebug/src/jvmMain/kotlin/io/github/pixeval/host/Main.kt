package io.github.pixeval.host

import androidx.compose.ui.window.Window
import androidx.compose.ui.window.application
import androidx.compose.ui.window.rememberWindowState
import androidx.compose.ui.input.key.*
import androidx.compose.runtime.*
import androidx.compose.material3.*
import androidx.compose.foundation.layout.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import io.github.pixeval.mobile.*
import uniffi.pixeval_native.MobileDirectories
import java.awt.Desktop
import java.awt.Toolkit
import java.awt.datatransfer.StringSelection
import java.net.URI
import java.nio.file.Path
import java.util.Locale
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow

fun main() = application {
    val scope = rememberCoroutineScope()
    val windowState = rememberWindowState()
    val platform = remember { object : PlatformServices {
        private val root = Path.of(System.getProperty("user.home"), ".pixeval", "compose-debug")
        override val directories = MobileDirectories(root.resolve("data").toString(), root.resolve("cache").toString(), root.resolve("downloads").toString())
        override val systemLocale = Locale.getDefault().toLanguageTag()
        override val foreground = MutableStateFlow(true)
        var backAction: (() -> Unit)? = null
        override fun setBackHandler(handler: (() -> Unit)?) { backAction = handler }
        override fun openUrl(url: String) { Desktop.getDesktop().browse(URI(url)) }
        override fun shareText(text: String) { Toolkit.getDefaultToolkit().systemClipboard.setContents(StringSelection(text), null) }
    } }
    var controller by remember { mutableStateOf<AppController?>(null) }
    var startupError by remember { mutableStateOf<String?>(null) }
    var retry by remember { mutableIntStateOf(0) }
    var initialization by remember { mutableStateOf<Job?>(null) }
    var closing by remember { mutableStateOf(false) }
    LaunchedEffect(retry) {
        initialization = coroutineContext[Job]
        startupError = null
        try { controller = AppController.open(platform) }
        catch (cancelled: CancellationException) { throw cancelled }
        catch (error: Exception) { startupError = error.message ?: error.toString() }
    }
    LaunchedEffect(windowState) {
        snapshotFlow { windowState.isMinimized }.collect { platform.foreground.value = !it }
    }
    Window(state = windowState, onCloseRequest = {
        if (!closing) {
            closing = true
            scope.launch {
                try { initialization?.cancelAndJoin(); controller?.close() }
                finally { exitApplication() }
            }
        }
    }, title = "Pixeval · Mobile Debug", onKeyEvent = { event ->
        if (event.key == Key.Escape && event.type == KeyEventType.KeyUp) {
            platform.backAction?.let { it(); true } ?: false
        } else false
    }) {
        val current = controller
        if (current != null) PixevalApp(current)
        else MaterialTheme { Column(Modifier.padding(24.dp)) {
            Text("Pixeval · Mobile Debug")
            startupError?.let { Text(it); Button(onClick = { retry++ }) { Text("Retry") } } ?: CircularProgressIndicator()
        } }
    }
}
