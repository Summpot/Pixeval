package io.github.pixeval.mobile

import androidx.compose.ui.graphics.toPixelMap
import uniffi.pixeval_cache.DecodedPreviewFrame
import kotlin.test.*

class ImageDecoderTest {
    @Test fun decodingBoundsTallAndWideImagesBeforeAllocation() {
        listOf(4000 to 120000, 120000 to 4000, 30000 to 30000).forEach { (width, height) ->
            val sample = decodeSampleSize(width, height, 1440)
            val decodedWidth = (width.toLong() + sample - 1) / sample
            val decodedHeight = (height.toLong() + sample - 1) / sample
            assertTrue(decodedWidth <= 8192 && decodedHeight <= 8192)
            assertTrue(decodedWidth * decodedHeight <= 8L * 1024 * 1024)
        }
        assertFailsWith<IllegalArgumentException> { decodeSampleSize(-1, -1, 480) }
    }
    @Test fun progressiveBgraPreservesChannelsAndTransparency() {
        val frame = DecodedPreviewFrame(2u, 1u, byteArrayOf(0, 0, -1, -1, -1, 0, 0, -128))
        val pixels = decodePreview(frame).toPixelMap()
        assertEquals(1f, pixels[0, 0].red)
        assertEquals(0f, pixels[0, 0].blue)
        assertTrue(pixels[1, 0].blue > .98f)
        assertTrue(pixels[1, 0].alpha in .49f.. .51f)
    }
}
