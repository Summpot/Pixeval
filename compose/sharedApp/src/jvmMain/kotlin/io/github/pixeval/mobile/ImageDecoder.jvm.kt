package io.github.pixeval.mobile

import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.toComposeImageBitmap
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import javax.imageio.ImageIO
import org.jetbrains.skia.Image
import org.jetbrains.skia.ImageInfo
import org.jetbrains.skia.ColorType
import org.jetbrains.skia.ColorAlphaType
import uniffi.pixeval_cache.DecodedPreviewFrame

internal actual fun decodeImage(bytes: ByteArray, width: Int): ImageBitmap {
    ImageIO.createImageInputStream(ByteArrayInputStream(bytes)).use { input ->
        val readers = ImageIO.getImageReaders(input)
        if (!readers.hasNext()) return Image.makeFromEncoded(bytes).toComposeImageBitmap()
        val reader = readers.next()
        try {
            reader.input = input
            val sample = decodeSampleSize(reader.getWidth(0), reader.getHeight(0), width)
            val params = reader.defaultReadParam
            params.setSourceSubsampling(sample, sample, 0, 0)
            val image = reader.read(0, params)
            val output = ByteArrayOutputStream()
            ImageIO.write(image, "png", output)
            return Image.makeFromEncoded(output.toByteArray()).toComposeImageBitmap()
        } finally { reader.dispose() }
    }
}

internal actual fun decodePreview(frame: DecodedPreviewFrame): ImageBitmap = Image.makeRaster(
    ImageInfo(frame.width.toInt(), frame.height.toInt(), ColorType.BGRA_8888, ColorAlphaType.UNPREMUL),
    frame.bgraData, frame.width.toInt() * 4,
).toComposeImageBitmap()
