package io.github.pixeval.pixeval_flutter

import android.content.Intent
import android.os.Build
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    companion object {
        private const val FOREGROUND_CHANNEL = "io.github.pixeval.pixeval_flutter/foreground"
        private const val DEEPLINK_CHANNEL = "io.github.pixeval.pixeval_flutter/deeplink"
        private const val DISPLAY_CHANNEL = "io.github.pixeval.pixeval_flutter/display"
    }

    private var deeplinkChannel: MethodChannel? = null
    private var initialLink: String? = null
    private var dartReady = false
    private val pendingLinks = mutableListOf<String>()

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        // Capture initial link if launched via intent
        initialLink = extractLinkFromIntent(intent)
        initialLink?.let { pendingLinks.add(it) }

        // Foreground Service Channel
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, FOREGROUND_CHANNEL).setMethodCallHandler { call, result ->
            when (call.method) {
                "startService" -> {
                    val title = call.argument<String>("title") ?: "Downloading..."
                    val intent = Intent(this, DownloadForegroundService::class.java).apply {
                        action = DownloadForegroundService.ACTION_START
                        putExtra(DownloadForegroundService.EXTRA_TITLE, title)
                    }
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                        startForegroundService(intent)
                    } else {
                        startService(intent)
                    }
                    result.success(null)
                }
                "updateProgress" -> {
                    val title = call.argument<String>("title") ?: "Downloading..."
                    val progress = call.argument<Int>("progress") ?: 0
                    val max = call.argument<Int>("max") ?: 100
                    val intent = Intent(this, DownloadForegroundService::class.java).apply {
                        action = DownloadForegroundService.ACTION_UPDATE
                        putExtra(DownloadForegroundService.EXTRA_TITLE, title)
                        putExtra(DownloadForegroundService.EXTRA_PROGRESS, progress)
                        putExtra(DownloadForegroundService.EXTRA_MAX, max)
                    }
                    startService(intent)
                    result.success(null)
                }
                "stopService" -> {
                    val intent = Intent(this, DownloadForegroundService::class.java).apply {
                        action = DownloadForegroundService.ACTION_STOP
                    }
                    startService(intent)
                    result.success(null)
                }
                else -> result.notImplemented()
            }
        }

        // Deeplink & Share Intent Channel
        deeplinkChannel = MethodChannel(flutterEngine.dartExecutor.binaryMessenger, DEEPLINK_CHANNEL)
        deeplinkChannel?.setMethodCallHandler { call, result ->
            when (call.method) {
                "getPendingLinks" -> {
                    dartReady = true
                    result.success(pendingLinks.toList())
                    pendingLinks.clear()
                    initialLink = null
                }
                "getInitialLink" -> {
                    result.success(initialLink)
                    initialLink = null
                }
                else -> result.notImplemented()
            }
        }

        // Display Mode Channel (High Refresh Rate 120Hz support)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, DISPLAY_CHANNEL).setMethodCallHandler { call, result ->
            when (call.method) {
                "setHighRefreshRate" -> {
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                        try {
                            val disp = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                                display
                            } else {
                                @Suppress("DEPRECATION")
                                windowManager.defaultDisplay
                            }
                            val modes = disp?.supportedModes ?: emptyArray()
                            val maxMode = modes.maxByOrNull { it.refreshRate }
                            if (maxMode != null) {
                                val params = window.attributes
                                params.preferredDisplayModeId = maxMode.modeId
                                window.attributes = params
                                result.success(maxMode.refreshRate.toDouble())
                                return@setMethodCallHandler
                            }
                        } catch (e: Exception) {
                            // Silently fallback if device vendor doesn't permit mode switching
                        }
                    }
                    result.success(60.0)
                }
                "getSupportedModes" -> {
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                        val disp = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                            display
                        } else {
                            @Suppress("DEPRECATION")
                            windowManager.defaultDisplay
                        }
                        val modes = disp?.supportedModes?.map {
                            mapOf(
                                "id" to it.modeId,
                                "width" to it.physicalWidth,
                                "height" to it.physicalHeight,
                                "refreshRate" to it.refreshRate.toDouble()
                            )
                        } ?: emptyList<Map<String, Any>>()
                        result.success(modes)
                    } else {
                        result.success(emptyList<Map<String, Any>>())
                    }
                }
                else -> result.notImplemented()
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        val link = extractLinkFromIntent(intent)
        if (link != null) {
            if (dartReady) deeplinkChannel?.invokeMethod("onLink", link)
            else pendingLinks.add(link)
        }
    }

    private fun extractLinkFromIntent(intent: Intent?): String? {
        if (intent == null) return null
        return when (intent.action) {
            Intent.ACTION_VIEW -> intent.dataString
            Intent.ACTION_SEND -> {
                if (intent.type == "text/plain") {
                    intent.getStringExtra(Intent.EXTRA_TEXT)
                } else null
            }
            else -> null
        }
    }
}
