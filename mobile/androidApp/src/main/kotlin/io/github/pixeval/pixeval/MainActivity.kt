package io.github.pixeval.pixeval

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.OnBackPressedCallback
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.core.view.WindowCompat
import io.github.pixeval.mobile.PixevalApp

class MainActivity : ComponentActivity() {
    private val session get() = (application as PixevalApplication).session
    private val document = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
        session.finishExport(if (result.resultCode == RESULT_OK) result.data?.data else null)
    }
    private val notifications = registerForActivityResult(ActivityResultContracts.RequestPermission()) {}
    private val back = object : OnBackPressedCallback(false) {
        override fun handleOnBackPressed() { session.backAction?.invoke() }
    }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        session.attach(this)
        onBackPressedDispatcher.addCallback(this, back)
        updateBackHandler(session.backAction != null)
        if (savedInstanceState == null) session.accept(intent)
        setContent {
            var controller by remember { mutableStateOf<io.github.pixeval.mobile.AppController?>(null) }
            var failure by remember { mutableStateOf<String?>(null) }
            LaunchedEffect(session) {
                try { controller = session.controller.await() }
                catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled }
                catch (error: Exception) { failure = error.message ?: error.toString() }
            }
            if (controller != null) {
                val state by controller!!.state.collectAsState()
                SideEffect {
                    WindowCompat.getInsetsController(window, window.decorView).apply {
                        isAppearanceLightStatusBars = !state.darkTheme
                        isAppearanceLightNavigationBars = !state.darkTheme
                    }
                }
                PixevalApp(controller!!)
            }
            else MaterialTheme { Column(Modifier.fillMaxSize().padding(24.dp)) {
                Text("Pixeval")
                failure?.let { Text(it) } ?: CircularProgressIndicator()
            } }
        }
    }
    override fun onNewIntent(intent: Intent) { super.onNewIntent(intent); setIntent(intent); session.accept(intent) }
    override fun onStart() { super.onStart(); session.attach(this); session.foreground.value = true }
    override fun onStop() { session.foreground.value = false; super.onStop() }
    override fun onDestroy() { session.detach(this); super.onDestroy() }
    fun createDocument(name: String, mime: String) {
        document.launch(Intent(Intent.ACTION_CREATE_DOCUMENT).apply {
            addCategory(Intent.CATEGORY_OPENABLE); type = mime; putExtra(Intent.EXTRA_TITLE, name)
        })
    }
    fun requestNotifications() {
        if (Build.VERSION.SDK_INT >= 33 && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
            notifications.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }
    fun updateBackHandler(enabled: Boolean) { back.isEnabled = enabled }
}
