/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.presentation

import android.app.Activity
import android.content.Context
import androidx.activity.compose.LocalActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.IntentSenderRequest
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.google.android.gms.common.ConnectionResult
import com.google.android.gms.common.GoogleApiAvailability
import com.google.mlkit.vision.documentscanner.GmsDocumentScannerOptions
import com.google.mlkit.vision.documentscanner.GmsDocumentScanning
import com.google.mlkit.vision.documentscanner.GmsDocumentScanningResult
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvIcon
import dev.jelshoj.kvitteria.core.designsystem.KvIconButton
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvSpinner
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.KvGlyph
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.feature.capture.domain.ImagePreparer
import dev.jelshoj.kvitteria.feature.capture.domain.PendingCapture
import dev.jelshoj.kvitteria.feature.capture.domain.PrepareError
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel

/**
 * Messages about captures for whoever is showing toasts: the shell. Adding starts on one screen and is
 * confirmed on another, so neither owns the outcome.
 */
class CaptureNotices {
    private val channel = Channel<UiText>(Channel.BUFFERED)
    val messages = channel.receiveAsFlow()

    fun post(message: UiText) {
        channel.trySend(message)
    }
}

fun PrepareError.toUiText(): UiText = UiText.StringResource(
    when (this) {
        PrepareError.Unreadable -> R.string.capture_unreadable
        PrepareError.TooLarge -> R.string.capture_too_large
    },
)

data class AddReceiptState(
    val canAdd: Boolean = false,
    val isSheetOpen: Boolean = false,
    val canScan: Boolean = true,
    val isPreparing: Boolean = false,
)

sealed interface AddReceiptAction {
    data object OnOpen : AddReceiptAction
    data object OnDismiss : AddReceiptAction
    data class OnScanSupport(val available: Boolean) : AddReceiptAction
    data object OnScannerFailed : AddReceiptAction
    data class OnPicked(val uri: String) : AddReceiptAction
}

sealed interface AddReceiptEvent {
    data object OpenConfirm : AddReceiptEvent
}

class AddReceiptViewModel(
    private val preparer: ImagePreparer,
    private val pending: PendingCapture,
    private val notices: CaptureNotices,
    sessions: SessionManager,
) : ViewModel() {
    private val _state = MutableStateFlow(AddReceiptState())
    val state = _state.asStateFlow()

    private val _events = Channel<AddReceiptEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    init {
        viewModelScope.launch {
            sessions.member.collect { member -> _state.update { it.copy(canAdd = member?.can(Permission.AddReceipts) == true) } }
        }
    }

    fun onAction(action: AddReceiptAction) {
        when (action) {
            AddReceiptAction.OnOpen -> _state.update { it.copy(isSheetOpen = true) }
            AddReceiptAction.OnDismiss -> _state.update { it.copy(isSheetOpen = false) }
            is AddReceiptAction.OnScanSupport -> _state.update { it.copy(canScan = action.available) }
            AddReceiptAction.OnScannerFailed -> {
                // From now on only choosing an image is offered.
                _state.update { it.copy(canScan = false) }
                notices.post(UiText.StringResource(R.string.capture_scanner_unavailable))
            }
            is AddReceiptAction.OnPicked -> prepare(action.uri)
        }
    }

    private fun prepare(uri: String) {
        if (_state.value.isPreparing) return
        _state.update { it.copy(isPreparing = true) }
        viewModelScope.launch {
            when (val result = preparer.prepare(uri)) {
                is Result.Success -> {
                    pending.set(result.data)
                    _state.update { it.copy(isPreparing = false, isSheetOpen = false) }
                    _events.send(AddReceiptEvent.OpenConfirm)
                }
                is Result.Error -> {
                    _state.update { it.copy(isPreparing = false) }
                    notices.post(result.error.toUiText())
                }
            }
        }
    }
}

/** Whether this device can run the ML Kit scanner at all, which needs Google Play services. */
private fun Context.hasScanner(): Boolean =
    GoogleApiAvailability.getInstance().isGooglePlayServicesAvailable(this) == ConnectionResult.SUCCESS

/**
 * The plus action and its sheet. Draws nothing for a member without [Permission.AddReceipts]. A prepared
 * image goes to [PendingCapture], and [onOpenConfirm] opens the confirmation.
 */
@Composable
fun AddReceiptButton(
    onOpenConfirm: () -> Unit,
    modifier: Modifier = Modifier,
    viewModel: AddReceiptViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    if (!state.canAdd) return

    val context = LocalContext.current
    val activity = LocalActivity.current
    LaunchedEffect(Unit) { viewModel.onAction(AddReceiptAction.OnScanSupport(context.hasScanner())) }
    ObserveAsEvents(viewModel.events) { event ->
        when (event) {
            AddReceiptEvent.OpenConfirm -> onOpenConfirm()
        }
    }

    // Backing out of either leaves nothing to do: the sheet stays as it was.
    val scanLauncher = rememberLauncherForActivityResult(ActivityResultContracts.StartIntentSenderForResult()) { result ->
        if (result.resultCode == Activity.RESULT_OK) {
            GmsDocumentScanningResult.fromActivityResultIntent(result.data)
                ?.pages?.firstOrNull()?.imageUri
                ?.let { viewModel.onAction(AddReceiptAction.OnPicked(it.toString())) }
        }
    }
    val pickLauncher = rememberLauncherForActivityResult(ActivityResultContracts.PickVisualMedia()) { uri ->
        uri?.let { viewModel.onAction(AddReceiptAction.OnPicked(it.toString())) }
    }
    val scanner = remember {
        GmsDocumentScanning.getClient(
            GmsDocumentScannerOptions.Builder()
                .setGalleryImportAllowed(false)
                .setPageLimit(1)
                .setResultFormats(GmsDocumentScannerOptions.RESULT_FORMAT_JPEG)
                .setScannerMode(GmsDocumentScannerOptions.SCANNER_MODE_FULL)
                .build(),
        )
    }

    KvIconButton(
        glyph = PhosphorIcons.Plus,
        contentDescription = stringResource(R.string.add_receipt),
        onClick = { viewModel.onAction(AddReceiptAction.OnOpen) },
        modifier = modifier,
        outlined = true,
    )

    if (state.isSheetOpen) {
        AddReceiptSheet(
            state = state,
            onScan = {
                if (activity == null) {
                    viewModel.onAction(AddReceiptAction.OnScannerFailed)
                } else {
                    scanner.getStartScanIntent(activity)
                        .addOnSuccessListener { sender -> scanLauncher.launch(IntentSenderRequest.Builder(sender).build()) }
                        .addOnFailureListener { viewModel.onAction(AddReceiptAction.OnScannerFailed) }
                }
            },
            onChoose = { pickLauncher.launch(PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly)) },
            onDismiss = { viewModel.onAction(AddReceiptAction.OnDismiss) },
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AddReceiptSheet(
    state: AddReceiptState,
    onScan: () -> Unit,
    onChoose: () -> Unit,
    onDismiss: () -> Unit,
) {
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true),
        containerColor = KvColors.bg,
    ) {
        Column(
            modifier = Modifier.navigationBarsPadding().padding(horizontal = KvSpace.gutter).padding(bottom = KvSpace.s6),
            verticalArrangement = Arrangement.spacedBy(KvSpace.s2),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
                KvHeading(stringResource(R.string.add_receipt), Modifier.weight(1f).padding(vertical = KvSpace.s3))
                if (state.isPreparing) KvSpinner()
            }
            if (state.canScan) {
                SheetOption(
                    PhosphorIcons.Scan,
                    stringResource(R.string.add_receipt_scan),
                    stringResource(R.string.add_receipt_scan_body),
                    enabled = !state.isPreparing,
                    onClick = onScan,
                )
            }
            SheetOption(
                PhosphorIcons.Image,
                stringResource(R.string.add_receipt_choose),
                stringResource(R.string.add_receipt_choose_body),
                enabled = !state.isPreparing,
                onClick = onChoose,
            )
        }
    }
}

@Composable
private fun SheetOption(glyph: KvGlyph, title: String, body: String, enabled: Boolean, onClick: () -> Unit) {
    val shape = RoundedCornerShape(KvRadius.md)
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(KvColors.surface, shape)
            .clickable(enabled = enabled, role = Role.Button, onClick = onClick)
            .padding(KvSpace.s4),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(KvSpace.s3),
    ) {
        KvIcon(glyph, contentDescription = null, size = KvSpace.s6, tint = KvColors.accent)
        Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s1)) {
            androidx.compose.foundation.text.BasicText(title, style = KvType.body.copy(color = KvColors.text))
            androidx.compose.foundation.text.BasicText(body, style = KvType.small.copy(color = KvColors.textMuted70))
        }
    }
}
