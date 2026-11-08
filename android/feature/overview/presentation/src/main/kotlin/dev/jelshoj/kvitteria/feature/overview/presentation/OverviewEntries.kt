/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.overview.presentation

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
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
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation3.runtime.EntryProviderScope
import androidx.navigation3.runtime.NavKey
import dev.jelshoj.kvitteria.core.designsystem.KvAmount
import dev.jelshoj.kvitteria.core.designsystem.KvAvatar
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCard
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvEyebrow
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvHeroCard
import dev.jelshoj.kvitteria.core.designsystem.KvIconButton
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvTag
import dev.jelshoj.kvitteria.core.designsystem.KvTagStyle
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.KvitteriaTheme
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.domain.insights.CategoryAmount
import dev.jelshoj.kvitteria.core.domain.member.Initials
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import dev.jelshoj.kvitteria.core.presentation.Loadable
import dev.jelshoj.kvitteria.core.presentation.LoadableContent
import dev.jelshoj.kvitteria.core.presentation.MessageState
import dev.jelshoj.kvitteria.core.presentation.OnStart
import java.math.BigDecimal
import java.math.RoundingMode
import java.time.YearMonth
import kotlinx.serialization.Serializable
import org.koin.compose.viewmodel.koinViewModel
import org.koin.core.module.dsl.viewModelOf
import org.koin.dsl.module

@Serializable data object OverviewRoute : NavKey

fun EntryProviderScope<NavKey>.overviewEntries(
    onOpenSettings: () -> Unit,
    onOpenSort: () -> Unit,
    onOpenInsights: () -> Unit,
    onOpenReceipt: (String) -> Unit,
    addReceipt: @Composable () -> Unit = {},
) {
    entry<OverviewRoute> { OverviewRoot(onOpenSettings, onOpenSort, onOpenInsights, onOpenReceipt, addReceipt) }
}

val overviewPresentationModule = module {
    viewModelOf(::OverviewViewModel)
}

@Composable
fun OverviewRoot(
    onOpenSettings: () -> Unit,
    onOpenSort: () -> Unit,
    onOpenInsights: () -> Unit,
    onOpenReceipt: (String) -> Unit,
    addReceipt: @Composable () -> Unit = {},
    viewModel: OverviewViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()

    OnStart(viewModel::refresh)

    OverviewScreen(
        state = state,
        onRefresh = viewModel::refresh,
        onPreviousMonth = viewModel::previousMonth,
        onNextMonth = viewModel::nextMonth,
        onOpenSettings = onOpenSettings,
        onOpenSort = onOpenSort,
        onOpenInsights = onOpenInsights,
        onOpenReceipt = onOpenReceipt,
        addReceipt = addReceipt,
    )
}

/** [addReceipt] is a slot the app fills, because adding a receipt belongs to another feature. */
@Composable
private fun ProfileBar(
    state: OverviewState,
    memberCount: Int?,
    onOpenSettings: () -> Unit,
    addReceipt: @Composable () -> Unit,
) {
    val part = stringResource(
        when (state.greeting) {
            Greeting.Morning -> R.string.greeting_morning
            Greeting.Afternoon -> R.string.greeting_afternoon
            Greeting.Evening -> R.string.greeting_evening
        },
    )
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
        KvAvatar(Initials.from(state.memberName))
        Column(Modifier.weight(1f)) {
            BasicText(
                "$part, ${state.memberName}",
                style = KvType.h4.copy(fontSize = KvType.cardTitle.fontSize),
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            if (memberCount != null) {
                BasicText(pluralStringResource(R.plurals.household_members, memberCount, memberCount), style = KvType.hint)
            }
        }
        addReceipt()
        KvIconButton(PhosphorIcons.Gear, stringResource(R.string.settings), onOpenSettings, tint = KvColors.neutral300)
    }
}

@Composable
private fun Hero(data: OverviewData, month: YearMonth, canGoForward: Boolean, onPrevious: () -> Unit, onNext: () -> Unit) {
    KvHeroCard(Modifier.fillMaxWidth()) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            KvEyebrow(DanishTime.month(month), Modifier.weight(1f))
            KvIconButton(PhosphorIcons.CaretLeft, stringResource(R.string.previous_month), onPrevious, tint = KvColors.accent)
            if (canGoForward) {
                KvIconButton(PhosphorIcons.CaretRight, stringResource(R.string.next_month), onNext, tint = KvColors.accent)
            }
        }
        Spacer(Modifier.height(KvSpace.s1))
        KvAmount(
            KrFormat.amount(data.summary.total),
            style = KvType.amountHero,
            color = KvColors.text,
            textAlign = TextAlign.Start,
        )
        Spacer(Modifier.height(12.dp))
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            data.summary.changePercent?.let { KvTag(KrFormat.change(it), style = KvTagStyle.Accent) }
            BasicText(
                listOfNotNull(
                    stringResource(R.string.vs_previous).takeIf { data.summary.changePercent != null },
                    pluralStringResource(R.plurals.receipt_count, data.summary.receiptCount, data.summary.receiptCount),
                ).joinToString(" · "),
                style = KvType.small.copy(color = KvColors.neutral400),
            )
        }
    }
}

@Composable
private fun CategoryBar(row: CategoryAmount, index: Int, data: OverviewData, max: BigDecimal) {
    val isUncategorized = row.categoryId == null
    val color = if (isUncategorized) KvColors.neutral700 else barColors[index % barColors.size]
    val fraction = if (max.signum() > 0 && row.amount.signum() > 0) {
        row.amount.divide(max, 4, RoundingMode.HALF_UP).toFloat().coerceAtLeast(0.02f)
    } else {
        0f
    }
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(9.dp)) {
            Box(Modifier.size(8.dp).background(color, RoundedCornerShape(2.dp)))
            Row(
                modifier = Modifier.weight(1f),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(9.dp),
            ) {
                BasicText(
                    row.name ?: stringResource(R.string.uncategorized),
                    style = KvType.control,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.weight(1f, fill = false),
                )
                BasicText(KrFormat.share(shareOf(row.amount, data.summary.linesTotal)), style = KvType.hint)
            }
            KvAmount(KrFormat.amount(row.amount), style = KvType.amountMedium.copy(fontSize = KvType.h4.fontSize), color = KvColors.text)
        }
        Box(
            Modifier
                .fillMaxWidth()
                .height(14.dp)
                .background(KvColors.textWash6, RoundedCornerShape(KvRadius.sm))
                .clearAndSetSemantics {},
        ) {
            if (fraction > 0f) {
                Box(Modifier.fillMaxWidth(fraction).height(14.dp).background(color, RoundedCornerShape(KvRadius.sm)))
            }
        }
    }
}

private val barColors: List<Color> = listOf(
    KvColors.accent500, KvColors.accent600, KvColors.accent700, KvColors.neutral600, KvColors.neutral700, KvColors.accent800,
)

@Composable
private fun WorkCard(
    kicker: String,
    title: String,
    body: String?,
    accented: Boolean = false,
    action: @Composable (() -> Unit)? = null,
) {
    val shape = RoundedCornerShape(KvRadius.md)
    KvCard(
        Modifier
            .fillMaxWidth()
            .then(if (accented) Modifier.border(1.dp, KvColors.accentTint34, shape) else Modifier),
    ) {
        BasicText(kicker.uppercase(), style = KvType.kicker)
        KvAmount(title, style = KvType.cardTitle.copy(fontFeatureSettings = "tnum"), color = KvColors.text, textAlign = TextAlign.Start)
        if (body != null) BasicText(body, style = KvType.small.copy(color = KvColors.textMuted70))
        action?.invoke()
    }
}

@Composable
private fun WorkCards(
    data: OverviewData,
    unassigned: Int,
    onOpenSort: () -> Unit,
    onOpenReceipt: (String) -> Unit,
) {
    Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        WorkCard(
            kicker = stringResource(R.string.work_waiting),
            title = pluralStringResource(R.plurals.line_count, unassigned, unassigned),
            body = stringResource(R.string.queue_blurb),
            accented = true,
        ) {
            KvButton(
                stringResource(R.string.start_sorting),
                onOpenSort,
                style = KvButtonStyle.Primary,
                glyph = PhosphorIcons.Lightning,
                fill = true,
            )
        }
        WorkCard(
            kicker = stringResource(R.string.uncategorized),
            title = KrFormat.share(shareOf(data.summary.uncategorizedTotal, data.summary.linesTotal)),
            body = stringResource(R.string.uncategorized_blurb, KrFormat.amount(data.summary.uncategorizedTotal)),
        )
        data.dinner?.let { dinner ->
            WorkCard(
                kicker = dinner.name,
                title = KrFormat.amount(dinner.month?.costPerDay),
                body = stringResource(
                    R.string.dinner_blurb,
                    dinner.month?.daysCovered ?: 0,
                    KrFormat.amount(dinner.month?.spend ?: BigDecimal.ZERO),
                ),
            )
        }
        data.latest?.let { latest ->
            WorkCard(
                kicker = stringResource(R.string.last_shop),
                title = latest.merchant,
                body = listOfNotNull(
                    DanishTime.date(latest.purchaseDate),
                    KrFormat.amount(latest.grandTotal),
                    latest.memberName,
                ).joinToString(" · "),
            ) {
                KvButton(stringResource(R.string.open_receipt), { onOpenReceipt(latest.id) }, fill = true)
            }
        }
    }
}

@Composable
fun OverviewScreen(
    state: OverviewState,
    onRefresh: () -> Unit,
    onPreviousMonth: () -> Unit,
    onNextMonth: () -> Unit,
    onOpenSettings: () -> Unit,
    onOpenSort: () -> Unit,
    onOpenInsights: () -> Unit,
    onOpenReceipt: (String) -> Unit,
    addReceipt: @Composable () -> Unit = {},
) {
    Column(Modifier.fillMaxSize().statusBarsPadding()) {
        Box(Modifier.padding(horizontal = KvSpace.gutter, vertical = KvSpace.s2)) {
            ProfileBar(state, state.data.data?.memberCount, onOpenSettings, addReceipt)
        }
        LoadableContent(
            state = state.data,
            onRefresh = onRefresh,
            isEmpty = { it.isEmpty },
            empty = {
                MessageState(
                    glyph = PhosphorIcons.Receipt,
                    title = stringResource(R.string.empty_title),
                    body = stringResource(R.string.empty_body),
                )
            },
        ) { data ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
                verticalArrangement = Arrangement.spacedBy(26.dp),
            ) {
                Hero(data, state.month, !state.isCurrentMonth, onPreviousMonth, onNextMonth)
                Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        KvHeading(stringResource(R.string.by_category), Modifier.weight(1f), style = KvType.h5)
                        KvButton(
                            stringResource(R.string.all_insights),
                            onOpenInsights,
                            style = KvButtonStyle.Ghost,
                            glyph = PhosphorIcons.ArrowRight,
                        )
                    }
                    val bars = data.bars
                    val max = bars.maxOfOrNull { it.amount } ?: BigDecimal.ZERO
                    bars.forEachIndexed { index, row -> CategoryBar(row, index, data, max) }
                }
                WorkCards(data, state.unassigned, onOpenSort, onOpenReceipt)
            }
        }
    }
}

@Preview
@Composable
private fun OverviewEmptyPreview() {
    KvitteriaTheme {
        OverviewScreen(
            OverviewState(YearMonth.of(2026, 8), true, Loadable(), "Sofie"),
            {}, {}, {}, {}, {}, {}, {},
        )
    }
}
