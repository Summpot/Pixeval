package io.github.pixeval.mobile

import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.toComposeImageBitmap
import org.jetbrains.skia.Image
import org.jetbrains.skia.ImageInfo
import org.jetbrains.skia.ColorType
import org.jetbrains.skia.ColorAlphaType
import uniffi.pixeval_cache.DecodedPreviewFrame

internal actual fun decodeImage(bytes: ByteArray, width: Int): ImageBitmap = Image.makeFromEncoded(bytes).toComposeImageBitmap()

internal actual fun decodePreview(frame: DecodedPreviewFrame): ImageBitmap = Image.makeRaster(
    ImageInfo(frame.width.toInt(), frame.height.toInt(), ColorType.BGRA_8888, ColorAlphaType.UNPREMUL),
    frame.bgraData, frame.width.toInt() * 4,
).toComposeImageBitmap()
