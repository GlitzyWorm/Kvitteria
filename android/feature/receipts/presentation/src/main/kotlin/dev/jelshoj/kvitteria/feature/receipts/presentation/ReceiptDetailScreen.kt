/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

@file:OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)

package dev.jelshoj.kvitteria.feature.receipts.presentation

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import dev.jelshoj.kvitteria.core.designsystem.KvAmount
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCallout
import dev.jelshoj.kvitteria.core.designsystem.KvCard
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvDialog
import dev.jelshoj.kvitteria.core.designsystem.KvField
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvIcon
import dev.jelshoj.kvitteria.core.designsystem.KvIconButton
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
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptLine
import dev.jelshoj.kvitteria.core.domain.receipt.ReconciliationState
import dev.jelshoj.kvitteria.core.domain.taxonomy.Meal
import dev.jelshoj.kvitteria.core.presentation.CategoryPickerSheet
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.ErrorState
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import dev.jelshoj.kvitteria.core.presentation.LoadingState
import dev.jelshoj.kvitteria.core.presentation.MealPickerSheet
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import java.math.BigDecimal
import java.math.RoundingMode
import org.koin.compose.viewmodel.koinViewModel
import org.koin.core.parameter.parametersOf

@Composable
fun ReceiptDetailRoot(
    id: String,
    onBack: () -> Unit,
    onOpenReceipt: (String) -> Unit = {},
    viewModel: ReceiptDetailViewModel = koinViewModel(key = id) { parametersOf(id) },
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.messages) { toast.show(it.asString(context)) }
    ObserveAsEvents(viewModel.events) { event ->
        when (event) {
            ReceiptDetailEvent.Close -> onBack()
            is ReceiptDetailEvent.OpenReceipt -> onOpenReceipt(event.id)
        }
    }

    KvToastBox(toast) {
        ReceiptDetailScreen(state = state, onAction = viewModel::onAction, onBack = onBack)
    }
}

@Composable
fun ReceiptDetailScreen(
    state: ReceiptDetailState,
    onAction: (ReceiptDetailAction) -> Unit,
    onBack: () -> Unit,
) {
    val receipt = state.receipt
    Column(Modifier.fillMaxSize().statusBarsPadding()) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            KvIconButton(PhosphorIcons.ArrowLeft, stringResource(CoreR.string.back), onBack, tint = KvColors.accent)
            BasicText(
                stringResource(R.string.receipts_title),
                style = KvType.small.copy(color = KvColors.accent),
                modifier = Modifier.weight(1f),
            )
            if (state.canDelete && receipt != null) {
                KvIconButton(
                    PhosphorIcons.Trash,
                    stringResource(R.string.delete_receipt),
                    { onAction(ReceiptDetailAction.OnAskDelete) },
                    tint = KvColors.neutral300,
                )
            }
        }
        PullToRefreshBox(
            isRefreshing = state.isRefreshing,
            onRefresh = { onAction(ReceiptDetailAction.OnRefresh) },
            modifier = Modifier.weight(1f),
        ) {
            when {
                receipt != null -> DetailContent(receipt, state, onAction)
                state.error != null -> ErrorState(state.error.asString(), { onAction(ReceiptDetailAction.OnRefresh) })
                else -> LoadingState()
            }
        }
    }

    state.picking?.let { (lineId, picker) ->
        val line = receipt?.lines?.firstOrNull { it.id == lineId } ?: return@let
        when (picker) {
            LinePicker.Category -> CategoryPickerSheet(
                categories = state.categories,
                selected = line.categoryId,
                onPick = { onAction(ReceiptDetailAction.OnPickCategory(it)) },
                onDismiss = { onAction(ReceiptDetailAction.OnDismissPicker) },
            )
            LinePicker.Meal -> MealPickerSheet(
                meals = state.meals,
                selected = line.mealId,
                onPick = { onAction(ReceiptDetailAction.OnPickMeal(it)) },
                onDismiss = { onAction(ReceiptDetailAction.OnDismissPicker) },
            )
        }
    }

    state.editor?.let { LineEditorDialog(it, state.canEdit, onAction) }
    state.totalsEditor?.let { TotalsEditorDialog(it, onAction) }
    state.acceptDialog?.let { AcceptDifferenceDialog(it, onAction) }
    if (state.confirmingDeleteLine != null) {
        DeleteLineDialog(
            isDeleting = state.isDeletingLine,
            onConfirm = { onAction(ReceiptDetailAction.OnConfirmDeleteLine) },
            onDismiss = { onAction(ReceiptDetailAction.OnDismissDeleteLine) },
        )
    }

    val image = state.image
    if (state.isViewerOpen && image is ReceiptImage.Loaded) {
        ReceiptImageViewer(image.bytes, onClose = { onAction(ReceiptDetailAction.OnCloseImage) })
    }
    if (state.isConfirmingDelete) {
        DeleteReceiptDialog(
            isDeleting = state.isDeleting,
            onConfirm = { onAction(ReceiptDetailAction.OnConfirmDelete) },
            onDismiss = { onAction(ReceiptDetailAction.OnDismissDelete) },
        )
    }
}

@Composable
private fun DetailContent(
    receipt: Receipt,
    state: ReceiptDetailState,
    onAction: (ReceiptDetailAction) -> Unit,
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        item { Header(receipt, state.canEdit, state.isWithdrawing, onAction) }
        if (receipt.possibleDuplicateOfId != null) {
            item { PossibleDuplicateMark(onOpen = { onAction(ReceiptDetailAction.OnOpenDuplicate) }) }
        }
        if (receipt.hasImage) {
            item {
                ReceiptThumbnail(
                    image = state.image,
                    onOpen = { onAction(ReceiptDetailAction.OnOpenImage) },
                    onRetry = { onAction(ReceiptDetailAction.OnRetryImage) },
                )
            }
        }
        items(receipt.lines, key = { it.id }) { line -> LineCard(line, state, onAction) }
        if (state.canEdit) {
            item {
                KvButton(
                    stringResource(R.string.add_line),
                    { onAction(ReceiptDetailAction.OnAddLine) },
                    glyph = PhosphorIcons.Plus,
                    modifier = Modifier.fillMaxWidth(),
                )
            }
        }
        item { CoverageCard(state, onAction) }
    }
}

@Composable
private fun Header(
    receipt: Receipt,
    canEdit: Boolean,
    isWithdrawing: Boolean,
    onAction: (ReceiptDetailAction) -> Unit,
) {
    Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s3), modifier = Modifier.padding(bottom = KvSpace.s3)) {
        BasicText(DanishTime.date(receipt.purchaseDate).uppercase(), style = KvType.kicker.copy(fontSize = KvType.meta.fontSize))
        KvHeading(receipt.merchant, style = KvType.h3)
        val lines = pluralStringResource(R.plurals.line_count, receipt.lines.size, receipt.lines.size)
        BasicText(
            listOfNotNull(receipt.memberName, lines).joinToString(" · "),
            style = KvType.small.copy(color = KvColors.neutral400),
        )
        Row(verticalAlignment = Alignment.CenterVertically) {
            KvAmount(KrFormat.amount(receipt.grandTotal), style = KvType.amountLarge, modifier = Modifier.weight(1f))
            if (canEdit) {
                KvIconButton(
                    PhosphorIcons.PencilSimple,
                    stringResource(R.string.edit_totals),
                    { onAction(ReceiptDetailAction.OnOpenTotals) },
                    tint = KvColors.neutral400,
                )
            }
        }
        KvAmount(
            stringResource(R.string.pant_amount, KrFormat.amount(receipt.pantTotal)),
            style = KvType.hint,
            modifier = Modifier.fillMaxWidth(),
        )
        FlowRow(horizontalArrangement = Arrangement.spacedBy(KvSpace.s3), verticalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            when (receipt.reconciliation) {
                ReconciliationState.Reconciled -> KvTag(stringResource(R.string.reconciled), style = KvTagStyle.Accent)
                ReconciliationState.Accepted -> KvTag(stringResource(R.string.accepted), style = KvTagStyle.Accent)
                ReconciliationState.Unreconciled, ReconciliationState.Unknown -> {
                    KvTag(stringResource(R.string.unreconciled), style = KvTagStyle.Outline)
                    BasicText(
                        stringResource(R.string.difference, KrFormat.amount(receipt.difference)),
                        style = KvType.label.copy(color = KvColors.neutral400),
                    )
                }
            }
        }
        receipt.acceptance?.let { acceptance ->
            val who = acceptance.memberName ?: stringResource(R.string.unknown_member)
            BasicText(
                stringResource(
                    R.string.accepted_by,
                    KrFormat.amount(acceptance.difference),
                    who,
                    DanishTime.dateTime(acceptance.acceptedAt),
                ),
                style = KvType.label.copy(color = KvColors.neutral400),
            )
            if (!acceptance.note.isNullOrBlank()) KvCallout(acceptance.note.orEmpty())
            if (canEdit) {
                KvButton(
                    stringResource(R.string.withdraw_acceptance),
                    { onAction(ReceiptDetailAction.OnWithdrawAcceptance) },
                    busy = isWithdrawing,
                )
            }
        }
        if (receipt.reconciliation == ReconciliationState.Unreconciled) {
            if (!receipt.warning.isNullOrBlank()) {
                BasicText(
                    stringResource(R.string.extraction_reported),
                    style = KvType.hint.copy(color = KvColors.neutral500),
                )
                KvCallout(receipt.warning.orEmpty())
            }
            if (canEdit) {
                KvButton(stringResource(R.string.accept_difference), { onAction(ReceiptDetailAction.OnAskAccept) })
            }
        }
    }
}

@Composable
private fun LineValue(label: String, value: String?, enabled: Boolean, onClick: () -> Unit, modifier: Modifier) {
    val shape = RoundedCornerShape(KvRadius.md)
    Row(
        modifier = modifier
            .heightIn(min = 48.dp)
            .background(KvColors.bg, shape)
            .border(1.dp, KvColors.divider, shape)
            .clickable(enabled = enabled, role = Role.DropdownList, onClick = onClick)
            .padding(horizontal = KvSpace.s3),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(KvSpace.s1),
    ) {
        BasicText(
            value ?: label,
            style = KvType.label.copy(color = if (value == null) KvColors.textMuted50 else KvColors.text),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.weight(1f),
        )
        if (enabled) KvIcon(PhosphorIcons.CaretDown, contentDescription = null, size = 13.dp)
    }
}

@Composable
private fun LineCard(line: ReceiptLine, state: ReceiptDetailState, onAction: (ReceiptDetailAction) -> Unit) {
    val shape = RoundedCornerShape(KvRadius.md)
    val assigned = line.canonicalItemId != null
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(KvColors.surface, shape)
            .border(1.dp, if (assigned) KvColors.divider else KvColors.accentTint34, shape)
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.Top) {
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                BasicText(line.rawName, style = KvType.mono.copy(fontSize = KvType.small.fontSize, color = KvColors.text))
                val quantity = "${KrFormat.quantity(line.quantity)} × ${KrFormat.amount(line.unitPrice)}"
                BasicText(
                    "${line.canonicalItemName ?: stringResource(R.string.no_item)} · $quantity",
                    style = KvType.hint.copy(color = if (assigned) KvColors.neutral500 else KvColors.accent300),
                )
            }
            KvAmount(KrFormat.amount(line.lineTotal))
        }
        Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s2), verticalAlignment = Alignment.CenterVertically) {
            LineValue(
                label = stringResource(CoreR.string.category),
                value = line.categoryName,
                enabled = state.canCategorize,
                onClick = { onAction(ReceiptDetailAction.OnOpenPicker(line.id, LinePicker.Category)) },
                modifier = Modifier.weight(1f),
            )
            LineValue(
                label = stringResource(CoreR.string.meal),
                value = line.mealName,
                enabled = state.canCategorize,
                onClick = { onAction(ReceiptDetailAction.OnOpenPicker(line.id, LinePicker.Meal)) },
                modifier = Modifier.weight(1f),
            )
            if (state.canEdit) {
                KvIconButton(
                    PhosphorIcons.PencilSimple,
                    stringResource(R.string.edit_line),
                    { onAction(ReceiptDetailAction.OnOpenEditor(line)) },
                    tint = KvColors.neutral400,
                )
            }
        }
    }
}

@Composable
private fun CoverageRow(meal: Meal, days: Int, canEdit: Boolean, onStep: (Int) -> Unit) {
    KvRule(color = KvColors.textWash8)
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
        BasicText(meal.name, style = KvType.control, modifier = Modifier.weight(1f))
        if (canEdit) {
            KvIconButton(
                PhosphorIcons.Minus,
                stringResource(R.string.fewer_days, meal.name),
                { onStep(-1) },
                enabled = days > 0,
                size = 30.dp,
                outlined = true,
            )
        }
        KvAmount(days.toString(), style = KvType.amount, modifier = Modifier.padding(horizontal = KvSpace.s1))
        if (canEdit) {
            KvIconButton(
                PhosphorIcons.Plus,
                stringResource(R.string.more_days, meal.name),
                { onStep(1) },
                enabled = days < 31,
                size = 30.dp,
                outlined = true,
            )
        }
    }
}

@Composable
private fun CoverageCard(state: ReceiptDetailState, onAction: (ReceiptDetailAction) -> Unit) {
    if (state.meals.none { it.tracksDayCoverage }) return
    KvCard(Modifier.fillMaxWidth().padding(top = KvSpace.s3), padding = 16.dp) {
        BasicText(stringResource(R.string.coverage).uppercase(), style = KvType.kicker)
        BasicText(stringResource(R.string.coverage_blurb), style = KvType.small.copy(color = KvColors.textMuted70))
        state.coverageMeals.forEach { meal ->
            val days = state.coverage[meal.id] ?: 0
            CoverageRow(meal, days, state.canCategorize) { onAction(ReceiptDetailAction.OnStepCoverage(meal.id, it)) }
            if (days > 0) {
                val perDay = state.spendOn(meal.id).divide(BigDecimal(days), 2, RoundingMode.HALF_UP)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    BasicText(stringResource(R.string.per_day), style = KvType.hint, modifier = Modifier.weight(1f))
                    KvAmount(KrFormat.amount(perDay), color = KvColors.accent300)
                }
            }
        }
        state.blockedMeals.forEach { meal ->
            KvRule(color = KvColors.textWash8)
            BasicText(stringResource(R.string.coverage_blocked, meal.name), style = KvType.hint)
        }
    }
}

@Composable
private fun LineEditorDialog(editor: LineEditor, canEdit: Boolean, onAction: (ReceiptDetailAction) -> Unit) {
    val change: (LineEditor) -> Unit = { onAction(ReceiptDetailAction.OnEditorChange(it)) }
    val decimal = KeyboardOptions(keyboardType = KeyboardType.Decimal)
    val invalid = stringResource(R.string.invalid_number)

    KvDialog(
        title = stringResource(if (editor.lineId == null) R.string.add_line else R.string.edit_line),
        onDismiss = { onAction(ReceiptDetailAction.OnDismissEditor) },
        actions = {
            if (editor.lineId != null && canEdit) {
                KvIconButton(
                    PhosphorIcons.Trash,
                    stringResource(R.string.delete_line),
                    { onAction(ReceiptDetailAction.OnAskDeleteLine(editor.lineId)) },
                    tint = KvColors.neutral300,
                )
            }
            KvButton(
                stringResource(CoreR.string.cancel),
                { onAction(ReceiptDetailAction.OnDismissEditor) },
                style = KvButtonStyle.Ghost,
            )
            KvButton(
                stringResource(CoreR.string.save),
                { onAction(ReceiptDetailAction.OnSaveEditor) },
                style = KvButtonStyle.Primary,
                busy = editor.isBusy,
            )
        },
    ) {
        KvField(
            label = stringResource(R.string.raw_name),
            value = editor.rawName,
            onValueChange = { change(editor.copy(rawName = it, rawNameInvalid = false)) },
            error = stringResource(R.string.name_required).takeIf { editor.rawNameInvalid },
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.quantity),
            value = editor.quantity,
            onValueChange = { change(editor.copy(quantity = it, quantityInvalid = false)) },
            error = invalid.takeIf { editor.quantityInvalid },
            keyboardOptions = decimal,
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.unit_price),
            value = editor.unitPrice,
            onValueChange = { change(editor.copy(unitPrice = it, unitPriceInvalid = false)) },
            error = invalid.takeIf { editor.unitPriceInvalid },
            keyboardOptions = decimal,
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.line_total),
            value = editor.lineTotal,
            onValueChange = { change(editor.copy(lineTotal = it, lineTotalInvalid = false)) },
            error = invalid.takeIf { editor.lineTotalInvalid },
            keyboardOptions = decimal,
            modifier = Modifier.fillMaxWidth(),
        )
        editor.error?.let { KvCallout(it.asString()) }
    }
}

@Composable
private fun TotalsEditorDialog(editor: TotalsEditor, onAction: (ReceiptDetailAction) -> Unit) {
    val change: (TotalsEditor) -> Unit = { onAction(ReceiptDetailAction.OnTotalsChange(it)) }
    val decimal = KeyboardOptions(keyboardType = KeyboardType.Decimal)
    val invalid = stringResource(R.string.invalid_number)

    KvDialog(
        title = stringResource(R.string.edit_totals),
        onDismiss = { onAction(ReceiptDetailAction.OnDismissTotals) },
        actions = {
            KvButton(
                stringResource(CoreR.string.cancel),
                { onAction(ReceiptDetailAction.OnDismissTotals) },
                style = KvButtonStyle.Ghost,
            )
            KvButton(
                stringResource(CoreR.string.save),
                { onAction(ReceiptDetailAction.OnSaveTotals) },
                style = KvButtonStyle.Primary,
                busy = editor.isBusy,
            )
        },
    ) {
        KvField(
            label = stringResource(R.string.grand_total),
            value = editor.grandTotal,
            onValueChange = { change(editor.copy(grandTotal = it, grandTotalInvalid = false)) },
            error = invalid.takeIf { editor.grandTotalInvalid },
            keyboardOptions = decimal,
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.pant_total),
            value = editor.pantTotal,
            onValueChange = { change(editor.copy(pantTotal = it, pantTotalInvalid = false)) },
            error = invalid.takeIf { editor.pantTotalInvalid },
            keyboardOptions = decimal,
            modifier = Modifier.fillMaxWidth(),
        )
        editor.error?.let { KvCallout(it.asString()) }
    }
}

@Composable
private fun AcceptDifferenceDialog(dialog: AcceptDialog, onAction: (ReceiptDetailAction) -> Unit) {
    KvDialog(
        title = stringResource(R.string.accept_title, KrFormat.amount(dialog.difference)),
        onDismiss = { onAction(ReceiptDetailAction.OnDismissAccept) },
        actions = {
            KvButton(
                stringResource(CoreR.string.cancel),
                { onAction(ReceiptDetailAction.OnDismissAccept) },
                style = KvButtonStyle.Ghost,
                enabled = !dialog.isBusy,
            )
            KvButton(
                stringResource(R.string.accept),
                { onAction(ReceiptDetailAction.OnConfirmAccept) },
                style = KvButtonStyle.Primary,
                busy = dialog.isBusy,
            )
        },
    ) {
        BasicText(stringResource(R.string.accept_body), style = KvType.body.copy(color = KvColors.neutral300))
        KvField(
            label = stringResource(R.string.accept_note),
            value = dialog.note,
            onValueChange = { onAction(ReceiptDetailAction.OnAcceptNoteChange(it)) },
            modifier = Modifier.fillMaxWidth(),
        )
        dialog.error?.let { KvCallout(it.asString()) }
    }
}

@Composable
private fun DeleteLineDialog(isDeleting: Boolean, onConfirm: () -> Unit, onDismiss: () -> Unit) {
    KvDialog(
        title = stringResource(R.string.delete_line_title),
        onDismiss = onDismiss,
        actions = {
            KvButton(stringResource(CoreR.string.cancel), onDismiss, style = KvButtonStyle.Ghost, enabled = !isDeleting)
            KvButton(
                stringResource(R.string.delete_line),
                onConfirm,
                style = KvButtonStyle.Primary,
                glyph = PhosphorIcons.Trash,
                busy = isDeleting,
            )
        },
    ) {
        BasicText(stringResource(R.string.delete_line_body), style = KvType.body.copy(color = KvColors.neutral300))
    }
}
