/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.domain

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Error
import dev.jelshoj.kvitteria.core.domain.Result
import java.time.Instant
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * An image ready to upload: upright, within [PixelBudget], and re-encoded as JPEG, which also leaves its
 * metadata behind. [capturedAt] is when it was taken, when the original said.
 */
class PreparedImage(
    val bytes: ByteArray,
    val width: Int,
    val height: Int,
    val capturedAt: Instant?,
)

/** Why an image couldn't be prepared. */
enum class PrepareError : Error { Unreadable, TooLarge }

/** Reads an image from a content URI and prepares it. Takes the URI as text to keep this module plain Kotlin. */
interface ImagePreparer {
    suspend fun prepare(uri: String): Result<PreparedImage, PrepareError>
}

/** Sends a prepared image to the server, answering with the intake it was recorded as. */
interface ReceiptUploader {
    suspend fun upload(image: PreparedImage): Result<String, ApiError>
}

/**
 * The one image waiting for confirmation, held in memory for the app's session. Routes carry IDs only, so
 * the image itself never travels in one, and a share that arrives before sign-in waits here.
 */
interface PendingCapture {
    val image: StateFlow<PreparedImage?>

    /** True while an image shared from another app waits for the signed-in shell to open its confirmation. */
    val awaitingShell: StateFlow<Boolean>

    fun set(image: PreparedImage, shared: Boolean = false)

    /** The shell has opened the confirmation for a shared image. */
    fun opened()

    fun clear()
}

class InMemoryPendingCapture : PendingCapture {
    private val _image = MutableStateFlow<PreparedImage?>(null)
    override val image: StateFlow<PreparedImage?> = _image.asStateFlow()

    private val _awaitingShell = MutableStateFlow(false)
    override val awaitingShell: StateFlow<Boolean> = _awaitingShell.asStateFlow()

    override fun set(image: PreparedImage, shared: Boolean) {
        _image.value = image
        _awaitingShell.value = shared
    }

    override fun opened() {
        _awaitingShell.value = false
    }

    override fun clear() {
        _image.value = null
        _awaitingShell.value = false
    }
}

/**
 * The pixel arithmetic of preparing an image, apart from Android so it can be tested. The numbers are the
 * server's: it refuses anything larger, because a bigger image runs the home server out of memory.
 */
object PixelBudget {
    const val MAX_PIXELS = 1_000_000L
    const val MAX_BYTES = 2 * 1024 * 1024
    const val JPEG_QUALITY = 85

    data class Size(val width: Int, val height: Int) {
        val pixels: Long get() = width.toLong() * height
    }

    /** The size after an EXIF rotation: a quarter turn swaps the sides. */
    fun rotated(width: Int, height: Int, degrees: Int): Size =
        if (degrees % 180 == 0) Size(width, height) else Size(height, width)

    /**
     * The largest power-of-two decode subsampling that still leaves at least the budget, so the full image
     * never has to sit in memory and the final scale still has detail to work with.
     */
    fun sampleSize(width: Int, height: Int): Int {
        var sample = 1
        while ((width.toLong() / (sample * 2)) * (height.toLong() / (sample * 2)) >= MAX_PIXELS) sample *= 2
        return sample
    }

    /** The size within the budget with the same aspect ratio. Never larger than [width] by [height]. */
    fun target(width: Int, height: Int): Size {
        val pixels = width.toLong() * height
        if (pixels <= MAX_PIXELS) return Size(width, height)
        val scale = kotlin.math.sqrt(MAX_PIXELS.toDouble() / pixels)
        var w = (width * scale).toInt().coerceAtLeast(1)
        var h = (height * scale).toInt().coerceAtLeast(1)
        // Rounding can land a pixel row over; trim the longer side until it fits.
        while (w.toLong() * h > MAX_PIXELS) if (w >= h) w-- else h--
        return Size(w, h)
    }
}
