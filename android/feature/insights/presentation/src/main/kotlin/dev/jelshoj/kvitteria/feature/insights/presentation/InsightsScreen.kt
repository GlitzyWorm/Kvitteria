/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
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
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import dev.jelshoj.kvitteria.core.designsystem.KvAmount
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCallout
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvDialog
import dev.jelshoj.kvitteria.core.designsystem.KvEyebrow
import dev.jelshoj.kvitteria.core.designsystem.KvField
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvHeroCard
import dev.jelshoj.kvitteria.core.designsystem.KvIconButton
import dev.jelshoj.kvitteria.core.designsystem.KvRule
import dev.jelshoj.kvitteria.core.designsystem.KvScrollTabs
import dev.jelshoj.kvitteria.core.designsystem.KvSegmented
import dev.jelshoj.kvitteria.core.designsystem.KvSelectField
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvToastBox
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.rememberKvToastState
import dev.jelshoj.kvitteria.core.domain.insights.CategoryAmount
import dev.jelshoj.kvitteria.core.domain.insights.CategoryBreakdown
import dev.jelshoj.kvitteria.core.domain.insights.MealCoverageStats
import dev.jelshoj.kvitteria.core.domain.insights.PriceHistory
import dev.jelshoj.kvitteria.core.domain.insights.TrendMonth
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import dev.jelshoj.kvitteria.core.presentation.LoadableContent
import dev.jelshoj.kvitteria.core.presentation.MessageState
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.PickerRow
import dev.jelshoj.kvitteria.core.presentation.PickerSearch
import dev.jelshoj.kvitteria.core.presentation.PickerSheet
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import java.math.BigDecimal
import java.math.RoundingMode
import java.time.YearMonth
import org.koin.compose.viewmodel.koinViewModel

@Composable
fun InsightsRoot(viewModel: InsightsViewModel = koinViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.messages) { toast.show(it.asString(context)) }

    KvToastBox(toast) {
        InsightsScreen(state, viewModel::onAction)
    }
}

@Composable
fun InsightsScreen(state: InsightsState, onAction: (InsightsAction) -> Unit) {
    val refresh = { onAction(InsightsAction.OnRefresh) }

    Column(Modifier.fillMaxSize().statusBarsPadding()) {
        Column(
            modifier = Modifier.padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
            verticalArrangement = Arrangement.spacedBy(KvSpace.s3),
        ) {
            KvHeading(stringResource(R.string.insights_title))
            KvScrollTabs(
                options = listOf(
                    stringResource(R.string.view_categories),
                    stringResource(R.string.view_trend),
                    stringResource(R.string.view_prices),
                    stringResource(R.string.view_dinner),
                ),
                selected = state.selections.view.ordinal,
                onSelect = { onAction(InsightsAction.OnView(InsightsView.entries[it])) },
            )
        }

        when (state.selections.view) {
            InsightsView.Categories -> LoadableContent(state.breakdown, refresh) { CategoriesView(it, state, onAction) }
            InsightsView.Trend -> LoadableContent(
                state.trend,
                refresh,
                isEmpty = { months -> months.all { it.receiptCount == 0 } },
                empty = { EmptyView() },
            ) { TrendView(it, state, onAction) }
            InsightsView.Prices -> LoadableContent(
                state.priceItems,
                refresh,
                isEmpty = { it.isEmpty() },
                empty = {
                    MessageState(PhosphorIcons.TrendUp, stringResource(R.string.no_prices_title), body = stringResource(R.string.no_prices_body))
                },
            ) { PricesView(state, onAction) }
            InsightsView.Dinner -> LoadableContent(
                state.dinner,
                refresh,
                isEmpty = { it.isEmpty() },
                empty = {
                    MessageState(PhosphorIcons.TrendUp, stringResource(R.string.no_tracking_title), body = stringResource(R.string.no_tracking_body))
                },
            ) { DinnerView(it, state, onAction) }
        }
    }

    Pickers(state, onAction)
    state.selections.dialog?.let { CategoryDialogView(it, state, onAction) }
}

@Composable
private fun EmptyView() {
    MessageState(PhosphorIcons.Receipt, stringResource(R.string.no_data_title), body = stringResource(R.string.no_data_body))
}

@Composable
private fun ViewColumn(content: @Composable () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
        verticalArrangement = Arrangement.spacedBy(KvSpace.s6),
    ) { content() }
}

private val barColors: List<Color> = listOf(
    KvColors.accent500, KvColors.accent600, KvColors.accent700, KvColors.neutral600, KvColors.neutral700, KvColors.accent800,
)

private fun changeColor(change: BigDecimal?): Color = when {
    change == null -> KvColors.neutral600
    change.signum() >= 0 -> KvColors.accent300
    else -> KvColors.neutral500
}

/** A `+x %` label, and empty for a row with nothing to compare with. */
private fun changeLabel(change: BigDecimal?): String = KrFormat.change(change)

private fun fractionOf(amount: BigDecimal?, max: BigDecimal): Float {
    if (amount == null || amount.signum() <= 0 || max.signum() <= 0) return 0f
    return amount.divide(max, 4, RoundingMode.HALF_UP).toFloat().coerceAtLeast(0.02f)
}

@Composable
private fun CategoriesView(breakdown: CategoryBreakdown, state: InsightsState, onAction: (InsightsAction) -> Unit) {
    val chosen = state.selections
    val rows = CategoryComparison.sorted(breakdown.rows, chosen.sort) + listOfNotNull(breakdown.uncategorized)
    val max = rows.maxOfOrNull { row -> maxOf(row.amount, row.compareAmount ?: BigDecimal.ZERO) } ?: BigDecimal.ZERO
    val hasCompare = chosen.compareTo != null

    ViewColumn {
        Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
            KvSelectField(
                label = stringResource(R.string.month_shown),
                value = DanishTime.month(chosen.month),
                onClick = { onAction(InsightsAction.OnPicker(InsightsPicker.Month)) },
                modifier = Modifier.weight(1f),
            )
            KvSelectField(
                label = stringResource(R.string.compared_with),
                value = chosen.compareTo?.let { DanishTime.month(it) } ?: stringResource(R.string.no_compare),
                onClick = { onAction(InsightsAction.OnPicker(InsightsPicker.Compare)) },
                modifier = Modifier.weight(1f),
            )
        }
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
            KvSegmented(
                options = listOf(stringResource(R.string.sort_size), stringResource(R.string.sort_change)),
                selected = chosen.sort.ordinal,
                onSelect = { if (it == 0 || hasCompare) onAction(InsightsAction.OnSort(CategorySort.entries[it])) },
                modifier = Modifier.weight(1f),
            )
            if (state.canManage) {
                KvButton(
                    stringResource(CoreR.string.new_category),
                    { onAction(InsightsAction.OnNewCategory) },
                    style = KvButtonStyle.Primary,
                    glyph = PhosphorIcons.Plus,
                )
            }
        }
        BasicText(
            chosen.drill?.let { stringResource(R.string.inside_category, it.name.orEmpty()) }
                ?: stringResource(R.string.drill_hint),
            style = KvType.small.copy(color = KvColors.neutral400),
        )

        Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
            rows.forEachIndexed { index, row ->
                CategoryRow(row, index, max, hasCompare, chosen.drill == null, state.canManage, onAction)
            }
            if (rows.isEmpty()) BasicText(stringResource(R.string.no_data_title), style = KvType.muted)
        }

        if (chosen.drill != null) {
            KvButton(
                stringResource(R.string.all_categories),
                { onAction(InsightsAction.OnDrill(null)) },
                style = KvButtonStyle.Ghost,
                glyph = PhosphorIcons.ArrowLeft,
            )
        }
        BasicText(moversNote(breakdown.rows, hasCompare), style = KvType.label.copy(color = KvColors.neutral500))
    }
}

@Composable
private fun moversNote(rows: List<CategoryAmount>, hasCompare: Boolean): String {
    val movers = CategoryComparison.movers(rows)
    return when {
        !hasCompare -> stringResource(R.string.movers_pick_month)
        movers == null -> stringResource(R.string.movers_none)
        else -> stringResource(
            R.string.movers,
            movers.rise.name.orEmpty(),
            KrFormat.change(movers.rise.changePercent),
            movers.fall.name.orEmpty(),
            KrFormat.change(movers.fall.changePercent),
        )
    }
}

@Composable
private fun CategoryRow(
    row: CategoryAmount,
    index: Int,
    max: BigDecimal,
    hasCompare: Boolean,
    canDrill: Boolean,
    canManage: Boolean,
    onAction: (InsightsAction) -> Unit,
) {
    val isUncategorized = row.categoryId == null
    val color = if (isUncategorized) KvColors.neutral700 else barColors[index % barColors.size]
    val tap = canDrill && !isUncategorized

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(min = 48.dp)
            .then(if (tap) Modifier.clickable(role = Role.Button) { onAction(InsightsAction.OnDrill(row)) } else Modifier)
            .padding(vertical = KvSpace.s1),
        verticalArrangement = Arrangement.spacedBy(3.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
            BasicText(
                row.name ?: stringResource(R.string.uncategorized),
                style = KvType.small,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.weight(1f),
            )
            KvAmount(KrFormat.amount(row.amount), style = KvType.amountSmall, color = KvColors.text)
            if (hasCompare) {
                KvAmount(changeLabel(row.changePercent), style = KvType.amountSmall, color = changeColor(row.changePercent))
            }
            if (canManage && !isUncategorized) {
                KvIconButton(
                    PhosphorIcons.PencilSimple,
                    stringResource(CoreR.string.edit_category),
                    { onAction(InsightsAction.OnEditCategory(row.categoryId.orEmpty())) },
                    size = 30.dp,
                    tint = KvColors.accent,
                )
            }
        }
        Bar(fractionOf(row.amount, max), color, 9.dp)
        if (hasCompare) Bar(fractionOf(row.compareAmount, max), KvColors.textMuted50, 5.dp)
    }
}

@Composable
private fun Bar(fraction: Float, color: Color, height: androidx.compose.ui.unit.Dp) {
    Box(
        Modifier
            .fillMaxWidth()
            .height(height)
            .background(KvColors.textWash6, RoundedCornerShape(3.dp))
            .clearAndSetSemantics {},
    ) {
        if (fraction > 0f) Box(Modifier.fillMaxWidth(fraction).height(height).background(color, RoundedCornerShape(3.dp)))
    }
}

@Composable
private fun ColumnHeader(text: String, modifier: Modifier = Modifier, end: Boolean = true) {
    BasicText(
        text.uppercase(),
        style = KvType.columnHeader.copy(textAlign = if (end) TextAlign.End else TextAlign.Start),
        modifier = modifier,
    )
}

@Composable
private fun TrendView(months: List<TrendMonth>, state: InsightsState, onAction: (InsightsAction) -> Unit) {
    ViewColumn {
        TrendChart(months, state.selections.trendMonth) { onAction(InsightsAction.OnTrendMonth(it)) }
        Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            Row {
                ColumnHeader(stringResource(R.string.column_month), Modifier.weight(1.4f), end = false)
                ColumnHeader(stringResource(R.string.column_total), Modifier.weight(1.2f))
                ColumnHeader(stringResource(R.string.column_change), Modifier.weight(1f))
                ColumnHeader(stringResource(R.string.column_receipts), Modifier.weight(0.8f))
            }
            KvRule()
            months.reversed().forEach { month ->
                Row(verticalAlignment = Alignment.CenterVertically) {
                    BasicText(DanishTime.month(month.month), style = KvType.small, modifier = Modifier.weight(1.4f))
                    KvAmount(KrFormat.amount(month.total), style = KvType.amountSmall, color = KvColors.text, modifier = Modifier.weight(1.2f))
                    KvAmount(
                        changeLabel(month.changePercent),
                        style = KvType.amountSmall,
                        color = changeColor(month.changePercent),
                        modifier = Modifier.weight(1f),
                    )
                    KvAmount(
                        month.receiptCount.toString(),
                        style = KvType.amountSmall,
                        color = KvColors.neutral500,
                        modifier = Modifier.weight(0.8f),
                    )
                }
                KvRule(color = KvColors.textWash8)
            }
        }
    }
}

@Composable
private fun PricesView(state: InsightsState, onAction: (InsightsAction) -> Unit) {
    val items = state.priceItems.data.orEmpty()
    val price = state.selections.price
    val chosen = items.firstOrNull { it.id == price?.itemId }
    val nameLabel = chosen?.names?.firstOrNull { it.key == price?.nameKey }?.label

    ViewColumn {
        KvSelectField(
            label = stringResource(R.string.item),
            // The heading names the selection: the item, or the item and its raw name.
            value = listOfNotNull(chosen?.name, nameLabel).joinToString(" · "),
            onClick = { onAction(InsightsAction.OnPicker(InsightsPicker.PriceItem)) },
            modifier = Modifier.fillMaxWidth(),
        )
        LoadableContent(state.history, onRefresh = { onAction(InsightsAction.OnRefresh) }, modifier = Modifier.height(380.dp)) { history ->
            PriceFigures(
                history,
                state.selections.priceMonth,
                onSelectMonth = { onAction(InsightsAction.OnPriceMonth(it)) },
            )
        }
    }
}

@Composable
private fun PriceFigure(label: String, value: String, color: Color, modifier: Modifier) {
    Column(modifier) {
        BasicText(label.uppercase(), style = KvType.columnHeader.copy(color = KvColors.neutral500))
        KvAmount(value, style = KvType.amountMedium, color = color, textAlign = TextAlign.Start)
    }
}

@Composable
private fun PriceFigures(history: PriceHistory, selected: YearMonth?, onSelectMonth: (YearMonth) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s6)) {
        Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
            PriceFigure(stringResource(R.string.price_now), KrFormat.amount(history.latestPrice), KvColors.text, Modifier.weight(1f))
            PriceFigure(
                stringResource(R.string.column_change),
                KrFormat.change(history.changePercent),
                changeColor(history.changePercent),
                Modifier.weight(1f),
            )
            PriceFigure(stringResource(R.string.price_lowest), KrFormat.amount(history.lowestPrice), KvColors.text, Modifier.weight(1f))
        }
        PriceChart(history.months, selected, onSelectMonth)
        BasicText(stringResource(R.string.price_note), style = KvType.label.copy(color = KvColors.neutral500))
    }
}

@Composable
private fun DinnerView(stats: List<MealCoverageStats>, state: InsightsState, onAction: (InsightsAction) -> Unit) {
    val meal = stats.firstOrNull { it.mealId == state.selections.mealId } ?: stats.first()
    val latest = meal.months.lastOrNull()

    ViewColumn {
        if (stats.size > 1) {
            KvSegmented(
                options = stats.map { it.name },
                selected = stats.indexOf(meal),
                onSelect = { onAction(InsightsAction.OnMeal(stats[it].mealId)) },
                modifier = Modifier.fillMaxWidth(),
            )
        }
        KvHeroCard(Modifier.fillMaxWidth()) {
            KvEyebrow(stringResource(R.string.per_day))
            KvAmount(KrFormat.amount(latest?.costPerDay), style = KvType.amountHero, color = KvColors.text, textAlign = TextAlign.Start)
            Row(Modifier.padding(top = KvSpace.s6), horizontalArrangement = Arrangement.spacedBy(KvSpace.s8)) {
                PriceFigure(stringResource(R.string.days_covered), (latest?.daysCovered ?: 0).toString(), KvColors.text, Modifier.weight(1f))
                PriceFigure(meal.name, KrFormat.amount(latest?.spend), KvColors.text, Modifier.weight(1f))
            }
        }
        Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            Row {
                ColumnHeader(stringResource(R.string.column_month), Modifier.weight(1.4f), end = false)
                ColumnHeader(meal.name, Modifier.weight(1.2f))
                ColumnHeader(stringResource(R.string.days_short), Modifier.weight(0.6f))
                ColumnHeader(stringResource(R.string.per_day), Modifier.weight(1f))
            }
            KvRule()
            meal.months.reversed().forEach { month ->
                Row(verticalAlignment = Alignment.CenterVertically) {
                    BasicText(DanishTime.month(month.month), style = KvType.small, modifier = Modifier.weight(1.4f))
                    KvAmount(KrFormat.amount(month.spend), style = KvType.amountSmall, color = KvColors.text, modifier = Modifier.weight(1.2f))
                    KvAmount(month.daysCovered.toString(), style = KvType.amountSmall, color = KvColors.text, modifier = Modifier.weight(0.6f))
                    KvAmount(KrFormat.amount(month.costPerDay), style = KvType.amountSmall, color = KvColors.accent300, modifier = Modifier.weight(1f))
                }
                KvRule(color = KvColors.textWash8)
            }
        }
    }
}

@Composable
private fun monthRows(months: List<YearMonth>): List<PickerRow<YearMonth?>> =
    months.map { PickerRow.Option(it, DanishTime.month(it)) }

@Composable
private fun Pickers(state: InsightsState, onAction: (InsightsAction) -> Unit) {
    val dismiss = { onAction(InsightsAction.OnPicker(null)) }
    when (state.selections.picker) {
        InsightsPicker.Month -> PickerSheet(
            title = stringResource(R.string.month_shown),
            rows = monthRows(state.months),
            selected = state.selections.month,
            onPick = { month -> month?.let { onAction(InsightsAction.OnMonth(it)) } },
            onDismiss = dismiss,
        )
        InsightsPicker.Compare -> PickerSheet(
            title = stringResource(R.string.compared_with),
            rows = listOf<PickerRow<YearMonth?>>(PickerRow.Option(null, stringResource(R.string.no_compare))) +
                monthRows(state.months.filter { it != state.selections.month }),
            selected = state.selections.compareTo,
            onPick = { onAction(InsightsAction.OnCompare(it)) },
            onDismiss = dismiss,
        )
        InsightsPicker.PriceItem -> PickerSheet(
            title = stringResource(R.string.item),
            rows = PriceItemRows.rows(state.priceItems.data.orEmpty(), state.selections.priceSearch),
            selected = state.selections.price,
            onPick = { onAction(InsightsAction.OnPrice(it)) },
            onDismiss = dismiss,
            search = PickerSearch(
                query = state.selections.priceSearch,
                onQueryChange = { onAction(InsightsAction.OnPriceSearch(it)) },
                label = stringResource(R.string.search_prices),
                emptyLabel = stringResource(R.string.no_matching_prices),
            ),
        )
        InsightsPicker.Parent -> {
            val dialog = state.selections.dialog
            PickerSheet(
                title = stringResource(CoreR.string.category_parent),
                rows = listOf<PickerRow<String?>>(PickerRow.Option(null, stringResource(CoreR.string.category_top_level))) +
                    state.categories.filter { it.id != dialog?.id }.map { PickerRow.Option(it.id, it.name) },
                selected = dialog?.parentId,
                onPick = {
                    onAction(InsightsAction.OnDialogChange(dialog?.copy(parentId = it)))
                    dismiss()
                },
                onDismiss = dismiss,
            )
        }
        null -> Unit
    }
}

@Composable
private fun CategoryDialogView(dialog: CategoryDialog, state: InsightsState, onAction: (InsightsAction) -> Unit) {
    val parent = state.categories.firstOrNull { it.id == dialog.parentId }?.name
    KvDialog(
        title = stringResource(if (dialog.id == null) CoreR.string.new_category else CoreR.string.edit_category),
        onDismiss = { onAction(InsightsAction.OnDialogChange(null)) },
        actions = {
            KvButton(
                stringResource(CoreR.string.cancel),
                { onAction(InsightsAction.OnDialogChange(null)) },
                style = KvButtonStyle.Ghost,
            )
            KvButton(
                stringResource(CoreR.string.save),
                { onAction(InsightsAction.OnSaveDialog) },
                style = KvButtonStyle.Primary,
                enabled = dialog.name.isNotBlank(),
                busy = dialog.isBusy,
            )
        },
    ) {
        KvField(
            label = stringResource(CoreR.string.category_name),
            value = dialog.name,
            onValueChange = { onAction(InsightsAction.OnDialogChange(dialog.copy(name = it, error = null))) },
            placeholder = stringResource(CoreR.string.category_name_placeholder),
            modifier = Modifier.fillMaxWidth(),
        )
        KvSelectField(
            label = stringResource(CoreR.string.category_parent),
            value = parent ?: stringResource(CoreR.string.category_top_level),
            onClick = { onAction(InsightsAction.OnPicker(InsightsPicker.Parent)) },
            enabled = !dialog.parentLocked,
            hint = stringResource(R.string.parent_locked).takeIf { dialog.parentLocked },
            modifier = Modifier.fillMaxWidth(),
        )
        dialog.error?.let { KvCallout(it.asString()) }
    }
}
