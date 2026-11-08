/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.presentation

import android.graphics.BitmapFactory
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.stringResource
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvIconButton
import dev.jelshoj.kvitteria.core.designsystem.KvNote
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvSpinner
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.refreshIfForbidden
import dev.jelshoj.kvitteria.core.presentation.toUiText
import dev.jelshoj.kvitteria.feature.capture.domain.PendingCapture
import dev.jelshoj.kvitteria.feature.capture.domain.PreparedImage
import dev.jelshoj.kvitteria.feature.capture.domain.ReceiptUploader
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.koin.compose.viewmodel.koinViewModel

data class ConfirmCaptureState(
    val image: PreparedImage? = null,
    val isUploading: Boolean = false,
    /** The last failure, which keeps the image here for another try or a discard. */
    val error: UiText? = null,
)

sealed interface ConfirmCaptureAction {
    data object OnUpload : ConfirmCaptureAction
    data object OnDiscard : ConfirmCaptureAction
}

sealed interface ConfirmCaptureEvent {
    /** Back to where adding started. */
    data object Close : ConfirmCaptureEvent
}

class ConfirmCaptureViewModel(
    private val pending: PendingCapture,
    private val uploader: ReceiptUploader,
    private val notices: CaptureNotices,
    private val members: MemberRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(ConfirmCaptureState(image = pending.image.value))
    val state = _state.asStateFlow()

    private val _events = Channel<ConfirmCaptureEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    init {
        // Restored after process death, the image is gone, and there is nothing to confirm.
        if (_state.value.image == null) _events.trySend(ConfirmCaptureEvent.Close)
    }

    fun onAction(action: ConfirmCaptureAction) {
        when (action) {
            ConfirmCaptureAction.OnUpload -> upload()
            ConfirmCaptureAction.OnDiscard -> {
                pending.clear()
                _events.trySend(ConfirmCaptureEvent.Close)
            }
        }
    }

    private fun upload() {
        val image = _state.value.image ?: return
        if (_state.value.isUploading) return
        _state.update { it.copy(isUploading = true, error = null) }
        viewModelScope.launch {
            when (val result = uploader.upload(image)) {
                is Result.Success -> {
                    pending.clear()
                    notices.post(UiText.StringResource(R.string.capture_queued))
                    _state.update { it.copy(isUploading = false) }
                    _events.send(ConfirmCaptureEvent.Close)
                }
                is Result.Error -> {
                    _state.update { it.copy(isUploading = false, error = result.error.toUiText()) }
                    members.refreshIfForbidden(result.error)
                }
            }
        }
    }
}

@Composable
fun ConfirmCaptureRoot(
    onClose: () -> Unit,
    viewModel: ConfirmCaptureViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    ObserveAsEvents(viewModel.events) { event ->
        when (event) {
            ConfirmCaptureEvent.Close -> onClose()
        }
    }
    ConfirmCaptureScreen(state = state, onAction = viewModel::onAction)
}

@Composable
fun ConfirmCaptureScreen(state: ConfirmCaptureState, onAction: (ConfirmCaptureAction) -> Unit) {
    val image = state.image
    val bitmap by produceState<ImageBitmap?>(null, image) {
        value = image?.let { withContext(Dispatchers.Default) { BitmapFactory.decodeByteArray(it.bytes, 0, it.bytes.size)?.asImageBitmap() } }
    }

    Column(
        modifier = Modifier.fillMaxSize().background(KvColors.bg).statusBarsPadding().navigationBarsPadding()
            .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
        verticalArrangement = Arrangement.spacedBy(KvSpace.s3),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            KvIconButton(
                glyph = PhosphorIcons.ArrowLeft,
                contentDescription = stringResource(R.string.confirm_discard),
                onClick = { onAction(ConfirmCaptureAction.OnDiscard) },
            )
            KvHeading(stringResource(R.string.confirm_title))
        }
        KvNote(stringResource(R.string.confirm_body))
        Box(
            modifier = Modifier.weight(1f).fillMaxWidth().clip(RoundedCornerShape(KvRadius.md)).background(KvColors.surface),
            contentAlignment = Alignment.Center,
        ) {
            val shown = bitmap
            if (shown == null) {
                KvSpinner()
            } else {
                Image(
                    bitmap = shown,
                    contentDescription = stringResource(R.string.confirm_image),
                    contentScale = ContentScale.Fit,
                    modifier = Modifier.fillMaxSize(),
                )
            }
        }
        state.error?.let { KvNote(it.asString()) }
        Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            KvButton(
                text = stringResource(R.string.confirm_discard),
                onClick = { onAction(ConfirmCaptureAction.OnDiscard) },
                modifier = Modifier.weight(1f),
                style = KvButtonStyle.Ghost,
                enabled = !state.isUploading,
                fill = true,
            )
            KvButton(
                text = stringResource(R.string.confirm_upload),
                onClick = { onAction(ConfirmCaptureAction.OnUpload) },
                modifier = Modifier.weight(1f),
                style = KvButtonStyle.Primary,
                busy = state.isUploading,
                enabled = image != null,
                fill = true,
            )
        }
    }
}
