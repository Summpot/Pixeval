@file:OptIn(kotlinx.cinterop.ExperimentalForeignApi::class)
package io.github.pixeval.mobile

import androidx.compose.ui.window.ComposeUIViewController
import platform.Foundation.*
import platform.UIKit.*
import uniffi.pixeval_native.MobileDirectories
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*

private val links = Channel<String>(Channel.UNLIMITED)
private val foreground = MutableStateFlow(true)
fun receiveLink(url: String) { links.trySend(url) }
fun setForeground(active: Boolean) { foreground.value = active }

fun mainViewController(videoEncoder: VideoEncoder): UIViewController {
    val base = NSHomeDirectory()
    val services = object : PlatformServices {
        override val directories = MobileDirectories("$base/Library/Application Support/Pixeval", "$base/Library/Caches/Pixeval", "$base/Documents/Downloads")
        override val systemLocale = NSLocale.preferredLanguages.firstOrNull() as? String ?: "zh-Hans"
        override val incomingLinks = links.receiveAsFlow()
        override val foreground = io.github.pixeval.mobile.foreground
        override val videoEncoder = videoEncoder
        override fun openUrl(url: String) { NSURL.URLWithString(url)?.let { UIApplication.sharedApplication.openURL(it) } }
        override fun shareText(text: String) {
            val root = UIApplication.sharedApplication.keyWindow?.rootViewController ?: return
            val sheet = UIActivityViewController(listOf(text), null)
            sheet.popoverPresentationController?.sourceView = root.view
            root.presentViewController(sheet, true, null)
        }
        override suspend fun exportFile(path: String) {
            val url = completedFile(path)
            val root = UIApplication.sharedApplication.keyWindow?.rootViewController ?: error("No active window")
            root.presentViewController(UIDocumentPickerViewController(forExportingURLs = listOf(url), asCopy = true), true, null)
        }
        override suspend fun shareFile(path: String) {
            val url = completedFile(path)
            val root = UIApplication.sharedApplication.keyWindow?.rootViewController ?: error("No active window")
            val sheet = UIActivityViewController(listOf(url), null)
            sheet.popoverPresentationController?.sourceView = root.view
            root.presentViewController(sheet, true, null)
        }
        private fun completedFile(path: String): NSURL {
            val root = NSURL.fileURLWithPath(directories.downloads).URLByResolvingSymlinksInPath!!.path!!
            val url = NSURL.fileURLWithPath(path).URLByResolvingSymlinksInPath!!
            check(url.path!!.startsWith("$root/") && NSFileManager.defaultManager.fileExistsAtPath(url.path!!)) {
                "The completed download is unavailable"
            }
            return url
        }
    }
    return ComposeUIViewController { PixevalApp(services) }
}
