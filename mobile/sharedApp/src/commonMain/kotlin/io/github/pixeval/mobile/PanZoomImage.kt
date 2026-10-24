package io.github.pixeval.mobile

import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.gestures.detectTransformGestures
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.unit.dp

@Composable
internal fun PanZoomImage(
    repository: ImageRepository,
    url: String?,
    description: String,
    modifier: Modifier = Modifier.fillMaxWidth().height(500.dp),
    width: Int = 1440
) {
    var scale by remember(url) { mutableFloatStateOf(1f) }
    var offset by remember(url) { mutableStateOf(Offset.Zero) }

    Box(
        modifier = modifier
            .pointerInput(url) {
                detectTapGestures(
                    onDoubleTap = {
                        if (scale > 1.05f) {
                            scale = 1f
                            offset = Offset.Zero
                        } else {
                            scale = 2.5f
                            offset = Offset.Zero
                        }
                    }
                )
            }
            .pointerInput(url) {
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
            .graphicsLayer {
                scaleX = scale
                scaleY = scale
                translationX = offset.x
                translationY = offset.y
            }
    ) {
        RustImage(repository, url, description, Modifier.matchParentSize(), width = width)
    }
}
