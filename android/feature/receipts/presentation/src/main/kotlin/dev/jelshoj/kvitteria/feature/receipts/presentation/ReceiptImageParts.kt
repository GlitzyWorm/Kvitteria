/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.receipts.presentation

import android.graphics.BitmapFactory
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.rememberTransformableState
import androidx.compose.foundation.gestures.transformable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvDialog
import dev.jelshoj.kvitteria.core.designsystem.KvIcon
import dev.jelshoj.kvitteria.core.designsystem.KvIconButton
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvSpinner
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

private const val MAX_ZOOM = 6f

/** Decodes off the main thread; null while decoding and for bytes that aren't an image. */
@Composable
private fun rememberDecoded(bytes: ByteArray): ImageBitmap? {
    val decoded by produceState<ImageBitmap?>(null, bytes) {
        value = withContext(Dispatchers.Default) { BitmapFactory.decodeByteArray(bytes, 0, bytes.size)?.asImageBitmap() }
    }
    return decoded
}

/** The image a receipt was read from, as a thumbnail that opens it full screen, or a retry when it failed. */
@Composable
fun ReceiptThumbnail(image: ReceiptImage, onOpen: () -> Unit, onRetry: () -> Unit) {
    if (image is ReceiptImage.None) return
    val shape = RoundedCornerShape(KvRadius.md)
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(180.dp)
            .clip(shape)
            .background(KvColors.surface)
            .border(1.dp, KvColors.divider, shape)
            .clickable(enabled = image is ReceiptImage.Loaded, role = Role.Image, onClick = onOpen),
        contentAlignment = Alignment.Center,
    ) {
        when (image) {
            ReceiptImage.None -> Unit
            ReceiptImage.Loading -> KvSpinner()
            ReceiptImage.Failed -> Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(KvSpace.s2),
            ) {
                BasicText(stringResource(R.string.image_failed), style = KvType.small.copy(color = KvColors.neutral400))
                KvButton(stringResource(CoreR.string.retry), onRetry, glyph = PhosphorIcons.ArrowClockwise)
            }
            is ReceiptImage.Loaded -> {
                val bitmap = rememberDecoded(image.bytes)
                if (bitmap == null) {
                    KvSpinner()
                } else {
                    Image(
                        bitmap = bitmap,
                        contentDescription = stringResource(R.string.receipt_image),
                        contentScale = ContentScale.Crop,
                        alignment = Alignment.TopCenter,
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }
        }
    }
}

/** Full screen, with pinch to zoom and drag to pan. */
@Composable
fun ReceiptImageViewer(bytes: ByteArray, onClose: () -> Unit) {
    Dialog(
        onDismissRequest = onClose,
        properties = DialogProperties(usePlatformDefaultWidth = false, decorFitsSystemWindows = false),
    ) {
        var scale by remember { mutableFloatStateOf(1f) }
        var offset by remember { mutableStateOf(Offset.Zero) }
        val transform = rememberTransformableState { zoom, pan, _ ->
            scale = (scale * zoom).coerceIn(1f, MAX_ZOOM)
            offset = if (scale == 1f) Offset.Zero else offset + pan
        }
        Box(Modifier.fillMaxSize().background(KvColors.bg)) {
            rememberDecoded(bytes)?.let { bitmap ->
                Image(
                    bitmap = bitmap,
                    contentDescription = stringResource(R.string.receipt_image),
                    contentScale = ContentScale.Fit,
                    modifier = Modifier
                        .fillMaxSize()
                        .transformable(transform)
                        .graphicsLayer {
                            scaleX = scale
                            scaleY = scale
                            translationX = offset.x
                            translationY = offset.y
                        },
                )
            }
            KvIconButton(
                glyph = PhosphorIcons.X,
                contentDescription = stringResource(CoreR.string.close),
                onClick = onClose,
                modifier = Modifier.align(Alignment.TopEnd).statusBarsPadding().padding(KvSpace.s2),
                outlined = true,
            )
        }
    }
}

/** The mark on a receipt that matches a stored one on merchant, day and total. Opens the match. */
@Composable
fun PossibleDuplicateMark(onOpen: () -> Unit) {
    val shape = RoundedCornerShape(KvRadius.md)
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(KvColors.accentTint10, shape)
            .border(1.dp, KvColors.accentTint34, shape)
            .clickable(role = Role.Button, onClick = onOpen)
            .padding(KvSpace.s3),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(KvSpace.s3),
    ) {
        KvIcon(PhosphorIcons.Copy, contentDescription = null, tint = KvColors.accent)
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            BasicText(stringResource(R.string.possible_duplicate), style = KvType.label.copy(color = KvColors.text))
            BasicText(stringResource(R.string.possible_duplicate_body), style = KvType.hint)
        }
        KvIcon(PhosphorIcons.CaretRight, contentDescription = null)
    }
}

@Composable
fun DeleteReceiptDialog(isDeleting: Boolean, onConfirm: () -> Unit, onDismiss: () -> Unit) {
    KvDialog(
        title = stringResource(R.string.delete_receipt_title),
        onDismiss = onDismiss,
        actions = {
            KvButton(stringResource(CoreR.string.cancel), onDismiss, style = KvButtonStyle.Ghost, enabled = !isDeleting)
            KvButton(
                stringResource(R.string.delete_receipt),
                onConfirm,
                style = KvButtonStyle.Primary,
                glyph = PhosphorIcons.Trash,
                busy = isDeleting,
            )
        },
    ) {
        BasicText(stringResource(R.string.delete_receipt_body), style = KvType.body.copy(color = KvColors.neutral300))
    }
}
