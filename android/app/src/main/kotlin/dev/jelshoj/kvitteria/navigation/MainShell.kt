/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.navigation

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.navigation3.runtime.NavKey
import androidx.navigation3.runtime.entryProvider
import androidx.navigation3.ui.NavDisplay
import dev.jelshoj.kvitteria.R
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvTab
import dev.jelshoj.kvitteria.core.designsystem.KvTabBar
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.domain.WorkCounts
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.designsystem.KvToastBox
import dev.jelshoj.kvitteria.core.designsystem.rememberKvToastState
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.feature.capture.domain.PendingCapture
import dev.jelshoj.kvitteria.feature.capture.presentation.AddReceiptButton
import dev.jelshoj.kvitteria.feature.capture.presentation.CaptureNotices
import dev.jelshoj.kvitteria.feature.capture.presentation.ConfirmCaptureRoute
import dev.jelshoj.kvitteria.feature.capture.presentation.captureEntries
import dev.jelshoj.kvitteria.feature.capture.presentation.R as CaptureR
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.platform.LocalContext
import dev.jelshoj.kvitteria.feature.insights.presentation.InsightsRoute
import dev.jelshoj.kvitteria.feature.insights.presentation.insightsEntries
import dev.jelshoj.kvitteria.feature.overview.presentation.OverviewRoute
import dev.jelshoj.kvitteria.feature.overview.presentation.overviewEntries
import dev.jelshoj.kvitteria.feature.pipeline.presentation.PipelineRoute
import dev.jelshoj.kvitteria.feature.pipeline.presentation.pipelineEntries
import dev.jelshoj.kvitteria.feature.receipts.presentation.ReceiptDetailRoute
import dev.jelshoj.kvitteria.feature.receipts.presentation.ReceiptsRoute
import dev.jelshoj.kvitteria.feature.receipts.presentation.receiptsEntries
import dev.jelshoj.kvitteria.feature.sort.presentation.SortRoute
import dev.jelshoj.kvitteria.feature.sort.presentation.sortEntries
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel

private val tabs: List<NavKey> = listOf(OverviewRoute, SortRoute, ReceiptsRoute, InsightsRoute, PipelineRoute)

/** Foreground refresh of the badges, for as long as the shell lives. */
class MainShellViewModel(
    val counts: WorkCounts,
    val pending: PendingCapture,
    val notices: CaptureNotices,
    private val sessions: SessionManager,
) : ViewModel() {
    fun refresh() {
        viewModelScope.launch { counts.refresh() }
    }

    /**
     * Whether to open the confirmation of an image shared from another app. Runs once the shell exists,
     * which is after sign-in, so a share that arrived signed out is shown here. Without [Permission.AddReceipts]
     * the image is discarded with a message rather than offered.
     */
    fun takeShared(): Boolean {
        if (!pending.awaitingShell.value) return false
        val member = sessions.member.value ?: return false
        pending.opened()
        if (member.can(Permission.AddReceipts)) return true
        pending.clear()
        notices.post(UiText.StringResource(CaptureR.string.capture_no_permission))
        return false
    }
}

@Composable
fun MainShell(
    onOpenSettings: () -> Unit,
    viewModel: MainShellViewModel = koinViewModel(),
) {
    val navigation = rememberTabNavigation(tabs)
    val unassigned by viewModel.counts.unassignedLines.collectAsStateWithLifecycle()
    val failed by viewModel.counts.failedMail.collectAsStateWithLifecycle()
    val sharedWaiting by viewModel.pending.awaitingShell.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.notices.messages) { toast.show(it.asString(context)) }
    LaunchedEffect(sharedWaiting) {
        if (sharedWaiting && viewModel.takeShared()) navigation.push(ConfirmCaptureRoute)
    }

    val addReceipt: @Composable () -> Unit = { AddReceiptButton(onOpenConfirm = { navigation.push(ConfirmCaptureRoute) }) }

    val openReceipt: (String) -> Unit = { id -> navigation.pushOn(ReceiptsRoute, ReceiptDetailRoute(id)) }
    val entries = entryProvider<NavKey> {
        overviewEntries(
            onOpenSettings = onOpenSettings,
            onOpenSort = { navigation.selectTab(SortRoute) },
            onOpenInsights = { navigation.selectTab(InsightsRoute) },
            onOpenReceipt = openReceipt,
            addReceipt = addReceipt,
        )
        sortEntries(onOpenReceipts = { navigation.selectTab(ReceiptsRoute) })
        receiptsEntries(
            onOpenReceipt = { navigation.push(ReceiptDetailRoute(it)) },
            onBack = navigation::back,
            addReceipt = addReceipt,
        )
        captureEntries(onClose = navigation::back)
        insightsEntries()
        pipelineEntries(onOpenReceipt = openReceipt)
    }

    Column(Modifier.fillMaxSize().background(KvColors.bg)) {
        Box(Modifier.weight(1f).fillMaxWidth()) {
            KvToastBox(toast) {
                NavDisplay(entries = navigation.decoratedEntries(entries), onBack = navigation::back)
            }
        }
        KvTabBar(
            tabs = listOf(
                KvTab(stringResource(R.string.tab_overview), PhosphorIcons.ChartPieSlice),
                KvTab(
                    stringResource(R.string.tab_sort),
                    PhosphorIcons.Lightning,
                    badge = unassigned,
                    badgeDescription = stringResource(R.string.badge_unassigned, unassigned).takeIf { unassigned > 0 },
                ),
                KvTab(stringResource(R.string.tab_receipts), PhosphorIcons.Receipt),
                KvTab(stringResource(R.string.tab_insights), PhosphorIcons.TrendUp),
                KvTab(
                    stringResource(R.string.tab_pipeline),
                    PhosphorIcons.FlowArrow,
                    badge = failed,
                    badgeDescription = stringResource(R.string.badge_failed, failed).takeIf { failed > 0 },
                ),
            ),
            selected = tabs.indexOf(navigation.current),
            onSelect = { navigation.selectTab(tabs[it]) },
        )
    }
}
