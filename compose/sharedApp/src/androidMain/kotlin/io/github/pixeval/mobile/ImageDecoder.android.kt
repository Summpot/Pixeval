package io.github.pixeval.mobile

import android.graphics.BitmapFactory
import android.graphics.Bitmap
import uniffi.pixeval_cache.DecodedPreviewFrame
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap

internal actual fun decodeImage(bytes: ByteArray, width: Int): ImageBitmap {
    val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
    BitmapFactory.decodeByteArray(bytes, 0, bytes.size, bounds)
    val options = BitmapFactory.Options().apply {
        inSampleSize = decodeSampleSize(bounds.outWidth, bounds.outHeight, width)
    }
    return (BitmapFactory.decodeByteArray(bytes, 0, bytes.size, options) ?: error("Unsupported image")).asImageBitmap()
}

internal actual fun decodePreview(frame: DecodedPreviewFrame): ImageBitmap {
    val pixels = IntArray(frame.bgraData.size / 4) { index ->
        val offset = index * 4
        fun byte(channel: Int) = frame.bgraData[offset + channel].toInt() and 255
        (byte(3) shl 24) or (byte(2) shl 16) or (byte(1) shl 8) or byte(0)
    }
    return Bitmap.createBitmap(pixels, frame.width.toInt(), frame.height.toInt(), Bitmap.Config.ARGB_8888).asImageBitmap()
}
