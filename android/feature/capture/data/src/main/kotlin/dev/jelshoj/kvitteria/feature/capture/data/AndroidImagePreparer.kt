/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.data

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import androidx.core.graphics.scale
import androidx.core.net.toUri
import androidx.exifinterface.media.ExifInterface
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.feature.capture.domain.ImagePreparer
import dev.jelshoj.kvitteria.feature.capture.domain.PixelBudget
import dev.jelshoj.kvitteria.feature.capture.domain.PrepareError
import dev.jelshoj.kvitteria.feature.capture.domain.PreparedImage
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.time.Instant
import java.time.LocalDateTime
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import kotlin.coroutines.cancellation.CancellationException
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Turns whatever was scanned, picked or shared into what the server accepts. The URI is read once, since a
 * shared URI's read grant may not outlive the activity. Bounds are decoded first, so a 12-megapixel photo is
 * subsampled while decoding instead of sitting in memory whole. [Bitmap.compress] writes no EXIF, which is
 * what leaves location and every other tag behind.
 */
class AndroidImagePreparer(
    private val context: Context,
    private val io: CoroutineDispatcher = Dispatchers.IO,
) : ImagePreparer {
    override suspend fun prepare(uri: String): Result<PreparedImage, PrepareError> = withContext(io) {
        try {
            val source = context.contentResolver.openInputStream(uri.toUri())?.use { it.readBytes() }
                ?: return@withContext Result.Error(PrepareError.Unreadable)
            if (source.size > MAX_SOURCE_BYTES) return@withContext Result.Error(PrepareError.TooLarge)
            prepare(source)
        } catch (e: CancellationException) {
            throw e
        } catch (_: Exception) {
            Result.Error(PrepareError.Unreadable)
        }
    }

    private fun prepare(source: ByteArray): Result<PreparedImage, PrepareError> {
        val exif = runCatching { ExifInterface(ByteArrayInputStream(source)) }.getOrNull()
        val degrees = exif?.rotationDegrees ?: 0
        val capturedAt = exif?.capturedAt()

        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        BitmapFactory.decodeByteArray(source, 0, source.size, bounds)
        if (bounds.outWidth <= 0 || bounds.outHeight <= 0) return Result.Error(PrepareError.Unreadable)

        val decoded = BitmapFactory.decodeByteArray(
            source,
            0,
            source.size,
            BitmapFactory.Options().apply { inSampleSize = PixelBudget.sampleSize(bounds.outWidth, bounds.outHeight) },
        ) ?: return Result.Error(PrepareError.Unreadable)

        val upright = PixelBudget.rotated(decoded.width, decoded.height, degrees)
        val target = PixelBudget.target(upright.width, upright.height)

        val turned = if (degrees == 0) {
            decoded
        } else {
            val rotation = Matrix().apply { postRotate(degrees.toFloat()) }
            Bitmap.createBitmap(decoded, 0, 0, decoded.width, decoded.height, rotation, true)
        }
        val scaled = if (turned.width == target.width && turned.height == target.height) {
            turned
        } else {
            turned.scale(target.width, target.height)
        }

        var quality = PixelBudget.JPEG_QUALITY
        var bytes = scaled.toJpeg(quality)
        while (bytes.size > PixelBudget.MAX_BYTES && quality > MIN_QUALITY) {
            quality -= QUALITY_STEP
            bytes = scaled.toJpeg(quality)
        }
        if (bytes.size > PixelBudget.MAX_BYTES) return Result.Error(PrepareError.TooLarge)

        return Result.Success(PreparedImage(bytes, scaled.width, scaled.height, capturedAt))
    }

    /**
     * When the photo was taken, from `DateTimeOriginal` and, when the camera wrote one, its offset. Without
     * an offset the time is the phone's local time, which is where the photo was almost certainly taken.
     * Read through `getAttribute` because the library's own parsed getter is restricted API.
     */
    private fun ExifInterface.capturedAt(): Instant? {
        val stamp = getAttribute(ExifInterface.TAG_DATETIME_ORIGINAL)?.trim() ?: return null
        val local = runCatching { LocalDateTime.parse(stamp, EXIF_TIME) }.getOrNull() ?: return null
        val offset = getAttribute(ExifInterface.TAG_OFFSET_TIME_ORIGINAL)
            ?.let { runCatching { ZoneOffset.of(it.trim()) }.getOrNull() }
        return if (offset != null) local.toInstant(offset) else local.atZone(ZoneId.systemDefault()).toInstant()
    }

    private fun Bitmap.toJpeg(quality: Int): ByteArray =
        ByteArrayOutputStream().also { compress(Bitmap.CompressFormat.JPEG, quality, it) }.toByteArray()

    private companion object {
        /** Far above any phone photo; only there so a stray huge file can't exhaust memory. */
        const val MAX_SOURCE_BYTES = 64 * 1024 * 1024
        const val MIN_QUALITY = 55
        const val QUALITY_STEP = 10
        val EXIF_TIME: DateTimeFormatter = DateTimeFormatter.ofPattern("yyyy:MM:dd HH:mm:ss")
    }
}
