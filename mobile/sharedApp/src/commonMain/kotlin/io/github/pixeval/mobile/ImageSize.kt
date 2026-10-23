package io.github.pixeval.mobile

/** Bound decoding before allocation, including very tall manga pages. */
internal fun decodeSampleSize(sourceWidth: Int, sourceHeight: Int, requestedWidth: Int): Int {
    require(sourceWidth > 0 && sourceHeight > 0) { "Unsupported image dimensions" }
    val target = requestedWidth.coerceIn(1, 4096)
    var sample = 1
    while (sample <= Int.MAX_VALUE / 2) {
        val width = (sourceWidth.toLong() + sample - 1) / sample
        val height = (sourceHeight.toLong() + sample - 1) / sample
        if (sourceWidth.toLong() / (sample.toLong() * 2) < target && width <= 8192 && height <= 8192 && width * height <= 8L * 1024 * 1024) break
        sample *= 2
    }
    return sample
}
