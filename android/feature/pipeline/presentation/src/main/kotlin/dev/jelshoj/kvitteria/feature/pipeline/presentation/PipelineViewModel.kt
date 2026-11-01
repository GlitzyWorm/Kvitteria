/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.pipeline.presentation

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.WorkCounts
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptRepository
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.presentation.Loadable
import dev.jelshoj.kvitteria.core.presentation.Loader
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.detailOrUiText
import dev.jelshoj.kvitteria.core.presentation.refreshIfForbidden
import dev.jelshoj.kvitteria.feature.pipeline.domain.MailboxHealth
import dev.jelshoj.kvitteria.feature.pipeline.domain.PipelineRepository
import dev.jelshoj.kvitteria.feature.pipeline.domain.PipelineStatus
import dev.jelshoj.kvitteria.feature.pipeline.domain.ReceiptFailure
import java.time.Clock
import java.time.Instant
import kotlinx.coroutines.async
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

data class PipelineData(
    val status: PipelineStatus,
    val failures: List<ReceiptFailure>,
    val unreconciled: List<Receipt>,
    val health: Map<String, MailboxHealth>,
)

data class PipelineState(
    val data: Loadable<PipelineData> = Loadable(),
    val canEdit: Boolean = false,
    val busyId: String? = null,
    val dismissing: ReceiptFailure? = null,
    val now: Instant = Instant.EPOCH,
)

sealed interface PipelineAction {
    data object OnRefresh : PipelineAction
    data class OnRetry(val failure: ReceiptFailure) : PipelineAction
    data class OnAskDismiss(val failure: ReceiptFailure?) : PipelineAction
    data object OnDismiss : PipelineAction
}

private const val UNRECONCILED_SHOWN = 50

class PipelineViewModel(
    private val pipeline: PipelineRepository,
    private val receipts: ReceiptRepository,
    private val counts: WorkCounts,
    private val members: MemberRepository,
    private val clock: Clock,
    sessions: SessionManager,
) : ViewModel() {
    private val loader = Loader(viewModelScope, ::load)
    private val busyId = MutableStateFlow<String?>(null)
    private val dismissing = MutableStateFlow<ReceiptFailure?>(null)

    val state = combine(loader.state, sessions.member, busyId, dismissing) { data, member, busy, toDismiss ->
        PipelineState(data, member?.can(Permission.EditReceipts) == true, busy, toDismiss, clock.instant())
    }.stateIn(viewModelScope, SharingStarted.Eagerly, PipelineState())

    private val notices = Channel<UiText>(Channel.BUFFERED)
    val messages = merge(loader.failures, notices.receiveAsFlow())

    private suspend fun load(): Result<PipelineData, ApiError> = coroutineScope {
        val status = async { pipeline.status() }
        val failures = async { pipeline.failures() }
        val unreconciled = async { receipts.receipts(1, UNRECONCILED_SHOWN, unreconciledOnly = true) }
        val health = async { pipeline.mailboxHealth() }

        val first = status.await()
        val second = failures.await()
        val third = unreconciled.await()
        when {
            first is Result.Error -> first
            second is Result.Error -> second
            third is Result.Error -> third
            else -> {
                val loaded = PipelineData(
                    status = (first as Result.Success).data,
                    failures = (second as Result.Success).data,
                    unreconciled = (third as Result.Success).data.items,
                    // No list is no reason to fail the tab: rows just can't say more than the status does.
                    health = (health.await() as? Result.Success)?.data.orEmpty().associateBy { it.mailboxId },
                )
                counts.setFailedMail(loaded.status.failed)
                Result.Success(loaded)
            }
        }
    }

    fun refresh() = loader.refresh()

    fun onAction(action: PipelineAction) {
        when (action) {
            PipelineAction.OnRefresh -> refresh()
            is PipelineAction.OnRetry -> retry(action.failure)
            is PipelineAction.OnAskDismiss -> dismissing.value = action.failure
            PipelineAction.OnDismiss -> dismiss()
        }
    }

    private fun removeFailure(id: String, queuedDelta: Int) {
        loader.update { data ->
            val status = data.status
            data.copy(
                failures = data.failures.filter { it.id != id },
                status = status.copy(
                    failed = (status.failed - 1).coerceAtLeast(0),
                    queued = status.queued + queuedDelta,
                ),
            )
        }
        counts.setFailedMail(loader.state.value.data?.status?.failed ?: 0)
    }

    private suspend fun onFailed(result: EmptyResult<ApiError>): Boolean {
        val error = (result as? Result.Error)?.error ?: return false
        notices.send(error.detailOrUiText())
        members.refreshIfForbidden(error)
        // Handled by someone else, so what's listed is out of date. A full queue changes nothing.
        if (error.kind == DataError.Network.NOT_FOUND || error.kind == DataError.Network.CONFLICT) refresh()
        return true
    }

    private fun retry(failure: ReceiptFailure) {
        if (busyId.value != null) return
        busyId.value = failure.id
        viewModelScope.launch {
            val result = pipeline.retry(failure.id)
            busyId.value = null
            if (!onFailed(result)) removeFailure(failure.id, queuedDelta = 1)
        }
    }

    private fun dismiss() {
        val failure = dismissing.value ?: return
        dismissing.value = null
        busyId.value = failure.id
        viewModelScope.launch {
            val result = pipeline.dismiss(failure.id)
            busyId.value = null
            if (!onFailed(result)) removeFailure(failure.id, queuedDelta = 0)
        }
    }
}
