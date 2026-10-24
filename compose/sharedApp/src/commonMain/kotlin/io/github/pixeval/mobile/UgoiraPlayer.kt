package io.github.pixeval.mobile

import androidx.compose.foundation.Image
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.gestures.detectTransformGestures
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.*
import uniffi.pixeval_cache.ImageCache
import uniffi.pixeval_mako.MakoClient
import uniffi.pixeval_native.extractUgoiraFramesFromZipBytes

@Composable
internal fun UgoiraPlayer(
    client: MakoClient,
    cache: ImageCache,
    illustId: Long,
    description: String,
    modifier: Modifier = Modifier.fillMaxWidth().height(500.dp),
    width: Int = 1080
) {
    var loading by remember { mutableStateOf(true) }
    var error by remember { mutableStateOf<String?>(null) }
    var frames by remember { mutableStateOf<List<ImageBitmap>>(emptyList()) }
    var delays by remember { mutableStateOf<List<Long>>(emptyList()) }
    var currentFrameIndex by remember { mutableIntStateOf(0) }
    var playing by remember { mutableStateOf(true) }

    LaunchedEffect(illustId) {
        loading = true
        error = null
        try {
            val meta = withContext(Dispatchers.IO) { client.getUgoiraMetadata(illustId) }
            val zipUrl = meta.zipUrls.medium
            val frameDelays = meta.frames.map { it.delay.toUInt() }
            val zipBytes = withContext(Dispatchers.IO) {
                cache.getOrFetch(zipUrl, null, null)
            }
            val extracted = withContext(Dispatchers.IO) {
                extractUgoiraFramesFromZipBytes(zipBytes, frameDelays)
            }
            val decodedBitmaps = withContext(Dispatchers.Default) {
                extracted.frameBytes.map { decodeImage(it, width) }
            }
            frames = decodedBitmaps
            delays = extracted.delays.map { it.toLong() }
            currentFrameIndex = 0
            loading = false
        } catch (c: CancellationException) {
            throw c
        } catch (e: Exception) {
            error = e.message ?: e.toString()
            loading = false
        }
    }

    LaunchedEffect(playing, frames.size, currentFrameIndex) {
        if (playing && frames.isNotEmpty()) {
            val delayMs = delays.getOrElse(currentFrameIndex) { 100L }.coerceAtLeast(10L)
            delay(delayMs)
            currentFrameIndex = (currentFrameIndex + 1) % frames.size
        }
    }

    var scale by remember { mutableFloatStateOf(1f) }
    var offset by remember { mutableStateOf(Offset.Zero) }

    Box(
        modifier = modifier
            .pointerInput(illustId) {
                detectTapGestures(
                    onDoubleTap = {
                        if (scale > 1.05f) {
                            scale = 1f
                            offset = Offset.Zero
                        } else {
                            scale = 2.5f
                            offset = Offset.Zero
                        }
                    },
                    onTap = { playing = !playing }
                )
            }
            .pointerInput(illustId) {
                detectTransformGestures { _, pan, zoom, _ ->
                    val newScale = (scale * zoom).coerceIn(1f, 5f)
                    if (newScale <= 1.01f) {
                        scale = 1f
                        offset = Offset.Zero
                    } else {
                        scale = newScale
                        val maxPan = 1200f * (scale - 1f)
                        offset = Offset(
                            x = (offset.x + pan.x).coerceIn(-maxPan, maxPan),
                            y = (offset.y + pan.y).coerceIn(-maxPan, maxPan)
                        )
                    }
                }
            }
    ) {
        if (loading) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    CircularProgressIndicator(Modifier.size(36.dp))
                    Text("Loading Ugoira...", style = MaterialTheme.typography.bodySmall)
                }
            }
        } else if (error != null) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text("Failed to load ugoira: $error", color = MaterialTheme.colorScheme.error)
            }
        } else if (frames.isNotEmpty()) {
            val currentBitmap = frames.getOrNull(currentFrameIndex)
            if (currentBitmap != null) {
                Image(
                    bitmap = currentBitmap,
                    contentDescription = description,
                    modifier = Modifier
                        .matchParentSize()
                        .graphicsLayer {
                            scaleX = scale
                            scaleY = scale
                            translationX = offset.x
                            translationY = offset.y
                        },
                    contentScale = ContentScale.Fit
                )
            }

            Surface(
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .fillMaxWidth()
                    .padding(8.dp),
                color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.85f),
                shape = MaterialTheme.shapes.small
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    IconButton(onClick = { playing = !playing }) {
                        Text(if (playing) "⏸" else "▶")
                    }
                    Slider(
                        value = currentFrameIndex.toFloat(),
                        onValueChange = {
                            playing = false
                            currentFrameIndex = it.toInt().coerceIn(0, frames.lastIndex)
                        },
                        valueRange = 0f..frames.lastIndex.toFloat().coerceAtLeast(1f),
                        modifier = Modifier.weight(1f)
                    )
                    Text(
                        "${currentFrameIndex + 1}/${frames.size}",
                        style = MaterialTheme.typography.labelSmall
                    )
                }
            }
        }
    }
}
