package io.github.pixeval.mobile

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.media.*
import android.os.SystemClock
import java.io.File
import java.nio.ByteBuffer

/** Uses image input planes, including the codec's row and pixel strides. */
class AndroidVideoEncoder : VideoEncoder {
    override fun encode(framePaths: List<String>, delaysMs: List<Long>, destination: String): String = try {
        require(framePaths.isNotEmpty() && framePaths.size == delaysMs.size) { "Frame count does not match delays" }
        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        BitmapFactory.decodeFile(framePaths.first(), bounds)
        require(bounds.outWidth > 0 && bounds.outHeight > 0) { "Cannot decode first frame" }
        val width = (bounds.outWidth + 1) and -2
        val height = (bounds.outHeight + 1) and -2
        val output = File(destination)
        output.parentFile?.mkdirs()
        val temp = File("$destination.encoding.mp4")
        try {
            encodeFrames(framePaths, delaysMs, width, height, temp)
            check(temp.length() > 0) { "The video encoder produced an empty file" }
            check(temp.renameTo(output)) { "Cannot save the encoded video" }
        } finally { temp.delete() }
        ""
    } catch(error: Exception) { error.message ?: error.toString() }

    private fun encodeFrames(paths: List<String>, delays: List<Long>, width: Int, height: Int, output: File) {
        val codec = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC)
        var muxer: MediaMuxer? = null
        var started = false
        var muxing = false
        try {
            val format = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, width, height).apply {
                setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible)
                setInteger(MediaFormat.KEY_BIT_RATE, (width.toLong() * height * 4).coerceIn(256_000, 32_000_000).toInt())
                setInteger(MediaFormat.KEY_FRAME_RATE, 30)
                setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1)
            }
            codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE)
            codec.start(); started = true
            val writer = MediaMuxer(output.absolutePath, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4)
            muxer = writer
            val info = MediaCodec.BufferInfo()
            var frame = 0
            var pts = 0L
            var inputDone = false
            var outputDone = false
            var track = -1
            var deadline = SystemClock.elapsedRealtime() + 60_000
            while(!outputDone) {
                check(SystemClock.elapsedRealtime() < deadline) { "Video encoder timed out" }
                if(!inputDone) {
                    val index = codec.dequeueInputBuffer(10_000)
                    if(index >= 0) {
                        if(frame == paths.size) {
                            codec.queueInputBuffer(index, 0, 0, pts, MediaCodec.BUFFER_FLAG_END_OF_STREAM)
                            inputDone = true
                        } else {
                            val decoded = BitmapFactory.decodeFile(paths[frame]) ?: error("Cannot decode frame ${frame + 1}")
                            val bitmap = if(decoded.width == width && decoded.height == height) decoded else Bitmap.createScaledBitmap(decoded, width, height, true)
                            try {
                                val image = codec.getInputImage(index) ?: error("Encoder has no YUV image input")
                                try { fillYuv(bitmap, image) } finally { image.close() }
                                codec.queueInputBuffer(index, 0, width * height * 3 / 2, pts, 0)
                            } finally { if(bitmap !== decoded) bitmap.recycle(); decoded.recycle() }
                            pts += delays[frame].coerceAtLeast(1) * 1000
                            frame++
                            deadline = SystemClock.elapsedRealtime() + 60_000
                        }
                    }
                }
                when(val index = codec.dequeueOutputBuffer(info, 10_000)) {
                    MediaCodec.INFO_OUTPUT_FORMAT_CHANGED -> {
                        check(!muxing) { "Video format changed twice" }
                        track = writer.addTrack(codec.outputFormat)
                        writer.start(); muxing = true
                    }
                    MediaCodec.INFO_TRY_AGAIN_LATER -> Unit
                    else -> if(index >= 0) {
                        try {
                            if(info.size > 0 && info.flags and MediaCodec.BUFFER_FLAG_CODEC_CONFIG == 0) {
                                check(muxing) { "Video output arrived before its format" }
                                val buffer = codec.getOutputBuffer(index) ?: error("Missing output buffer")
                                buffer.position(info.offset); buffer.limit(info.offset + info.size)
                                writer.writeSampleData(track, buffer, info)
                            }
                            outputDone = info.flags and MediaCodec.BUFFER_FLAG_END_OF_STREAM != 0
                            if (outputDone) {
                                check(muxing) { "Video encoder produced no samples" }
                                // MediaMuxer uses this timestamp to preserve the
                                // final frame's delay rather than guessing it.
                                val end = MediaCodec.BufferInfo().apply {
                                    set(0, 0, pts, MediaCodec.BUFFER_FLAG_END_OF_STREAM)
                                }
                                writer.writeSampleData(track, ByteBuffer.allocate(0), end)
                            }
                        } finally { codec.releaseOutputBuffer(index, false) }
                    }
                }
            }
            writer.stop(); muxing = false
        } finally {
            if(muxing) runCatching { muxer?.stop() }
            muxer?.release()
            if(started) runCatching { codec.stop() }
            codec.release()
        }
    }

    private fun fillYuv(bitmap: Bitmap, image: Image) {
        val width = bitmap.width
        val height = bitmap.height
        val pixels = IntArray(width * height)
        bitmap.getPixels(pixels, 0, width, 0, 0, width, height)
        fun channel(pixel: Int, shift: Int) = (pixel shr shift) and 255
        fun write(plane: Image.Plane, x: Int, y: Int, value: Int) {
            plane.buffer.put(y * plane.rowStride + x * plane.pixelStride, value.coerceIn(0, 255).toByte())
        }
        for(y in 0 until height) for(x in 0 until width) {
            val pixel = pixels[y * width + x]
            val r = channel(pixel, 16); val g = channel(pixel, 8); val b = channel(pixel, 0)
            write(image.planes[0], x, y, ((66 * r + 129 * g + 25 * b + 128) shr 8) + 16)
            if(x % 2 == 0 && y % 2 == 0) {
                val p1 = pixels[y * width + x + 1]
                val p2 = pixels[(y + 1) * width + x]
                val p3 = pixels[(y + 1) * width + x + 1]
                fun average(shift: Int) = (channel(pixel, shift) + channel(p1, shift) + channel(p2, shift) + channel(p3, shift)) / 4
                val red = average(16)
                val green = average(8)
                val blue = average(0)
                write(image.planes[1], x / 2, y / 2, ((-38 * red - 74 * green + 112 * blue + 128) shr 8) + 128)
                write(image.planes[2], x / 2, y / 2, ((112 * red - 94 * green - 18 * blue + 128) shr 8) + 128)
            }
        }
    }
}
