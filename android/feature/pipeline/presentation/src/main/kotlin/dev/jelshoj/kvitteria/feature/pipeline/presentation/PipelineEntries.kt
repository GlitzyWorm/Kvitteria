/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

@file:OptIn(ExperimentalLayoutApi::class)

package dev.jelshoj.kvitteria.feature.pipeline.presentation

import dev.jelshoj.kvitteria.core.designsystem.KvIcon
import java.util.Locale
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.feature.pipeline.domain.FailureSource
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation3.runtime.EntryProviderScope
import androidx.navigation3.runtime.NavKey
import dev.jelshoj.kvitteria.core.designsystem.KvAmount
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCallout
import dev.jelshoj.kvitteria.core.designsystem.KvCard
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvDialog
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvRule
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvTag
import dev.jelshoj.kvitteria.core.designsystem.KvTagStyle
import dev.jelshoj.kvitteria.core.designsystem.KvToastBox
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.rememberKvToastState
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.LoadableContent
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import dev.jelshoj.kvitteria.core.presentation.RelativeTime
import dev.jelshoj.kvitteria.feature.pipeline.domain.MailboxActivity
import dev.jelshoj.kvitteria.feature.pipeline.domain.MailboxState
import dev.jelshoj.kvitteria.feature.pipeline.domain.ReceiptFailure
import dev.jelshoj.kvitteria.feature.pipeline.domain.toState
import kotlinx.serialization.Serializable
import org.koin.compose.viewmodel.koinViewModel
import org.koin.core.module.dsl.viewModelOf
import org.koin.dsl.module

@Serializable data object PipelineRoute : NavKey

fun EntryProviderScope<NavKey>.pipelineEntries(onOpenReceipt: (String) -> Unit) {
    entry<PipelineRoute> { PipelineRoot(onOpenReceipt = onOpenReceipt) }
}

val pipelinePresentationModule = module {
    viewModelOf(::PipelineViewModel)
}

@Composable
fun PipelineRoot(
    onOpenReceipt: (String) -> Unit,
    viewModel: PipelineViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.messages) { toast.show(it.asString(context)) }

    KvToastBox(toast) {
        PipelineScreen(state, viewModel::onAction, onOpenReceipt)
    }
}

@Composable
fun PipelineScreen(
    state: PipelineState,
    onAction: (PipelineAction) -> Unit,
    onOpenReceipt: (String) -> Unit,
) {
    Column(Modifier.fillMaxSize().statusBarsPadding()) {
        LoadableContent(state.data, onRefresh = { onAction(PipelineAction.OnRefresh) }) { data ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
                verticalArrangement = Arrangement.spacedBy(20.dp),
            ) {
                Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s1)) {
                    KvHeading(stringResource(R.string.pipeline_title))
                    val polled = data.status.lastPolledAt
                    BasicText(
                        if (polled == null) {
                            stringResource(R.string.never_polled)
                        } else {
                            stringResource(
                                R.string.last_poll,
                                RelativeTime.describe(polled, state.now).asString(),
                                data.status.pollIntervalSeconds,
                            )
                        },
                        style = KvType.small.copy(color = KvColors.neutral500),
                    )
                }

                FlowRow(
                    maxItemsInEachRow = 2,
                    horizontalArrangement = Arrangement.spacedBy(12.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    val hasFailed = data.status.failed > 0
                    StageCard(stringResource(R.string.stage_queued), data.status.queued, KvColors.accent)
                    StageCard(stringResource(R.string.stage_extracting), data.status.extracting, KvColors.accent)
                    StageCard(stringResource(R.string.stage_persisting), data.status.persisting, KvColors.neutral500)
                    StageCard(
                        stringResource(R.string.stage_failed),
                        data.status.failed,
                        if (hasFailed) KvColors.accent300 else KvColors.neutral500,
                    )
                }

                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    KvHeading(stringResource(R.string.problems), style = KvType.h5)
                    data.failures.forEach { FailureCard(it, state, onAction) }
                    data.unreconciled.forEach { UnreconciledRow(it, onOpenReceipt) }
                    if (data.failures.isEmpty() && data.unreconciled.isEmpty()) {
                        BasicText(stringResource(R.string.no_problems), style = KvType.muted)
                    }
                }

                Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
                    KvHeading(stringResource(R.string.mailboxes), style = KvType.h5)
                    if (data.status.mailboxes.isEmpty()) {
                        BasicText(stringResource(R.string.no_mailboxes), style = KvType.muted)
                    }
                    data.status.mailboxes.forEach { activity ->
                        MailboxRow(activity, data.health[activity.mailboxId]?.lastError, data.health[activity.mailboxId].toState())
                    }
                }
            }
        }
    }

    state.dismissing?.let { failure ->
        KvDialog(
            title = stringResource(R.string.dismiss_title),
            onDismiss = { onAction(PipelineAction.OnAskDismiss(null)) },
            actions = {
                KvButton(
                    stringResource(CoreR.string.cancel),
                    { onAction(PipelineAction.OnAskDismiss(null)) },
                    style = KvButtonStyle.Ghost,
                )
                KvButton(
                    stringResource(R.string.dismiss),
                    { onAction(PipelineAction.OnDismiss) },
                    style = KvButtonStyle.Primary,
                )
            },
        ) {
            BasicText(failure.summary().title.asString(), style = KvType.small)
            BasicText(stringResource(R.string.dismiss_message), style = KvType.small.copy(color = KvColors.neutral400))
        }
    }
}

@Composable
private fun StageCard(label: String, count: Int, tone: Color) {
    KvCard(Modifier.fillMaxWidth(0.48f)) {
        BasicText(label.uppercase(), style = KvType.kicker.copy(color = tone))
        KvAmount(count.toString(), style = KvType.amountLarge, color = KvColors.text, textAlign = androidx.compose.ui.text.style.TextAlign.Start)
    }
}

/**
 * What a failure is called and what is said under it. Mail goes by subject and sender; an upload has
 * neither, so it says it was an uploaded image, with who sent it and when.
 */
data class FailureSummary(val title: UiText, val meta: List<String>)

fun ReceiptFailure.summary(locale: Locale = Locale.getDefault()): FailureSummary = when (source) {
    FailureSource.Upload -> FailureSummary(
        UiText.StringResource(R.string.failure_uploaded_image),
        listOf(memberName, DanishTime.dateTime(receivedAt, locale)),
    )
    FailureSource.Mail, FailureSource.Unknown -> FailureSummary(
        UiText.DynamicString(subject),
        listOf(from, memberName, DanishTime.date(mailDate, locale)).filter { it.isNotBlank() },
    )
}

@Composable
private fun FailureCard(failure: ReceiptFailure, state: PipelineState, onAction: (PipelineAction) -> Unit) {
    val summary = failure.summary()
    KvCard(Modifier.fillMaxWidth(), edge = KvColors.accent300) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            if (failure.source == FailureSource.Upload) KvIcon(PhosphorIcons.Image, contentDescription = null)
            BasicText(summary.title.asString(), style = KvType.control)
        }
        BasicText(
            (summary.meta + stringResource(R.string.attempts, failure.attempts)).joinToString(" · "),
            style = KvType.hint,
        )
        BasicText(
            failure.error,
            style = KvType.mono,
            modifier = Modifier
                .fillMaxWidth()
                .background(KvColors.textWash6, RoundedCornerShape(KvRadius.tag))
                .padding(horizontal = 11.dp, vertical = 9.dp),
        )
        if (state.canEdit) {
            Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
                KvButton(
                    stringResource(CoreR.string.retry),
                    { onAction(PipelineAction.OnRetry(failure)) },
                    glyph = PhosphorIcons.ArrowClockwise,
                    busy = state.busyId == failure.id,
                )
                KvButton(
                    stringResource(R.string.dismiss),
                    { onAction(PipelineAction.OnAskDismiss(failure)) },
                    style = KvButtonStyle.Ghost,
                )
            }
        }
    }
}

@Composable
private fun UnreconciledRow(receipt: Receipt, onOpenReceipt: (String) -> Unit) {
    KvCard(
        Modifier
            .fillMaxWidth()
            .heightIn(min = 48.dp)
            .clickable(role = Role.Button) { onOpenReceipt(receipt.id) },
        edge = KvColors.accent,
    ) {
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.CenterVertically) {
            KvTag(stringResource(R.string.unreconciled), style = KvTagStyle.Outline)
            BasicText(receipt.merchant, style = KvType.control, modifier = Modifier.weight(1f))
            BasicText(DanishTime.date(receipt.purchaseDate), style = KvType.hint)
        }
        receipt.warning?.let { BasicText(it, style = KvType.hint) }
    }
}

@Composable
private fun MailboxRow(activity: MailboxActivity, lastError: String?, state: MailboxState) {
    Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s1)) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
            Column(Modifier.weight(1f)) {
                BasicText(activity.memberName, style = KvType.control)
                BasicText(activity.folder, style = KvType.mono)
            }
            when (state) {
                MailboxState.Connected -> KvTag(stringResource(R.string.mailbox_connected), style = KvTagStyle.Accent)
                MailboxState.Failing -> KvTag(stringResource(R.string.mailbox_failing), style = KvTagStyle.Outline)
                MailboxState.Paused -> KvTag(stringResource(R.string.mailbox_paused), style = KvTagStyle.Neutral)
            }
        }
        BasicText(
            listOfNotNull(
                activity.lastMessageAt?.let { stringResource(R.string.last_mail, DanishTime.dateTime(it)) },
                stringResource(R.string.this_month, activity.receiptsThisMonth),
            ).joinToString(" · "),
            style = KvType.hint,
        )
        if (state == MailboxState.Failing && lastError != null) KvCallout(lastError)
        KvRule(color = KvColors.textWash7, modifier = Modifier.padding(top = KvSpace.s2))
    }
}
