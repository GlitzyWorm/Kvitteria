/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

@file:OptIn(ExperimentalLayoutApi::class, ExperimentalMaterial3Api::class)

package dev.jelshoj.kvitteria.feature.sort.presentation

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.verticalScroll
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
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import dev.jelshoj.kvitteria.core.designsystem.KvAmount
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCallout
import dev.jelshoj.kvitteria.core.designsystem.KvCard
import dev.jelshoj.kvitteria.core.designsystem.KvCheckbox
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvDialog
import dev.jelshoj.kvitteria.core.designsystem.KvElevation
import dev.jelshoj.kvitteria.core.designsystem.KvField
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvIcon
import dev.jelshoj.kvitteria.core.designsystem.KvNote
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvRule
import dev.jelshoj.kvitteria.core.designsystem.KvSelectField
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvToastBox
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.KvitteriaTheme
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.rememberKvToastState
import dev.jelshoj.kvitteria.core.domain.taxonomy.CanonicalItem
import dev.jelshoj.kvitteria.core.presentation.CategoryPickerSheet
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.ErrorState
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import dev.jelshoj.kvitteria.core.presentation.LoadingState
import dev.jelshoj.kvitteria.core.presentation.MealPickerSheet
import dev.jelshoj.kvitteria.core.presentation.MessageState
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.PickerRow
import dev.jelshoj.kvitteria.core.presentation.PickerSheet
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import dev.jelshoj.kvitteria.feature.sort.domain.UnassignedLine
import java.math.BigDecimal
import java.time.Instant
import org.koin.compose.viewmodel.koinViewModel

@Composable
fun SortRoot(
    onOpenReceipts: () -> Unit,
    viewModel: SortViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.messages) { toast.show(it.asString(context)) }

    KvToastBox(toast) {
        SortScreen(state = state, onAction = viewModel::onAction, onOpenReceipts = onOpenReceipts)
    }
}

@Composable
fun SortScreen(
    state: SortState,
    onAction: (SortAction) -> Unit,
    onOpenReceipts: () -> Unit,
) {
    Column(Modifier.fillMaxSize().statusBarsPadding().imePadding()) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            KvHeading(stringResource(R.string.sort_title))
            if (state.total > 0) {
                KvAmount("${state.doneCount} / ${state.total}", style = KvType.amountSmall, color = KvColors.neutral500)
                Box(Modifier.weight(1f).height(3.dp).background(KvColors.textWash8, RoundedCornerShape(2.dp))) {
                    Box(
                        Modifier
                            .fillMaxWidth(state.doneCount.toFloat() / state.total)
                            .fillMaxHeight()
                            .background(KvColors.accent, RoundedCornerShape(2.dp)),
                    )
                }
            }
        }

        PullToRefreshBox(
            isRefreshing = false,
            onRefresh = { onAction(SortAction.OnRefresh) },
            modifier = Modifier.weight(1f),
        ) {
            val line = state.current
            when {
                state.isLoading -> LoadingState()
                state.error != null -> ErrorState(state.error.asString(), { onAction(SortAction.OnRefresh) })
                line != null -> Queue(line, state, onAction)
                state.skippedCount > 0 -> MessageState(
                    glyph = PhosphorIcons.ArrowClockwise,
                    title = stringResource(R.string.all_skipped_title),
                    body = stringResource(R.string.all_skipped_body),
                    actionLabel = stringResource(R.string.show_skipped),
                    onAction = { onAction(SortAction.OnShowSkipped) },
                )
                else -> MessageState(
                    glyph = PhosphorIcons.Confetti,
                    title = stringResource(R.string.queue_done_title),
                    body = stringResource(R.string.queue_done_body),
                    actionLabel = stringResource(R.string.go_receipts),
                    onAction = onOpenReceipts,
                )
            }
        }
    }

    Sheets(state, onAction)
}

@Composable
private fun Queue(line: UnassignedLine, state: SortState, onAction: (SortAction) -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
        verticalArrangement = Arrangement.spacedBy(KvSpace.s6),
    ) {
        if (!state.canCategorize) KvNote(stringResource(R.string.no_categorize_permission))

        KvCard(Modifier.fillMaxWidth(), elevation = KvElevation.Medium, padding = 16.dp) {
            LineHeader(line)
            KvRule()
            if (state.canCategorize) Suggestions(state, onAction)
            state.draft?.let { Draft(it, state, onAction) }
            if (state.canCategorize) {
                Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
                    KvButton(
                        stringResource(CoreR.string.save),
                        { onAction(SortAction.OnSave) },
                        style = KvButtonStyle.Primary,
                        glyph = PhosphorIcons.Check,
                        enabled = state.draft != null && state.draft.name.isNotBlank(),
                        busy = state.isSaving,
                    )
                    KvButton(stringResource(R.string.skip), { onAction(SortAction.OnSkip) })
                }
            }
        }

        state.learned?.let { count ->
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .border(1.dp, KvColors.accentTint30, RoundedCornerShape(KvRadius.md))
                    .padding(horizontal = 14.dp, vertical = 12.dp),
                horizontalArrangement = Arrangement.spacedBy(10.dp),
            ) {
                KvIcon(PhosphorIcons.GraduationCap, contentDescription = null, tint = KvColors.accent)
                BasicText(pluralStringResource(R.plurals.learned_lines, count, count), style = KvType.small)
            }
        }

        if (state.upNext.isNotEmpty()) {
            Column {
                BasicText(stringResource(R.string.up_next), style = KvType.label.copy(color = KvColors.neutral500))
                state.upNext.forEach { next ->
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(vertical = KvSpace.s3),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        BasicText(
                            next.rawName,
                            style = KvType.mono.copy(color = KvColors.neutral300),
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                            modifier = Modifier.weight(1f),
                        )
                        BasicText(next.merchant, style = KvType.hint.copy(color = KvColors.neutral600))
                        KvAmount(KrFormat.amount(next.lineTotal), style = KvType.amountSmall, color = KvColors.neutral400)
                    }
                    KvRule(color = KvColors.textWash7)
                }
            }
        }
    }
}

@Composable
private fun LineHeader(line: UnassignedLine) {
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        BasicText(stringResource(R.string.raw_line).uppercase(), style = KvType.kicker)
        BasicText(line.rawName, style = KvType.mono.copy(fontSize = 23.sp, lineHeight = 28.sp, color = KvColors.text))
        BasicText(
            listOfNotNull(line.merchant, DanishTime.date(line.purchaseDate), line.memberName).joinToString(" · "),
            style = KvType.label.copy(color = KvColors.neutral400),
        )
        Row(verticalAlignment = Alignment.Bottom, horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
            KvAmount(KrFormat.amount(line.lineTotal), style = KvType.amountMedium, color = KvColors.text)
            BasicText(
                "${KrFormat.quantity(line.quantity)} × ${KrFormat.amount(line.unitPrice)}",
                style = KvType.label.copy(color = KvColors.neutral500),
            )
        }
    }
}

@Composable
private fun Chip(
    label: String,
    selected: Boolean,
    onClick: () -> Unit,
    enabled: Boolean = true,
    trailing: @Composable (() -> Unit)? = null,
) {
    val shape = RoundedCornerShape(KvRadius.md)
    Row(
        modifier = Modifier
            .heightIn(min = 48.dp)
            .padding(vertical = 4.dp)
            .border(1.dp, if (selected) KvColors.accent else KvColors.divider, shape)
            .background(if (selected) KvColors.accentTint10 else KvColors.bg, shape)
            .clickable(enabled = enabled, role = Role.Button, onClick = onClick)
            .padding(horizontal = 10.dp, vertical = KvSpace.s2),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(KvSpace.s1),
    ) {
        BasicText(
            label,
            style = KvType.small.copy(color = if (enabled) KvColors.text else KvColors.textMuted50),
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
        )
        trailing?.invoke()
    }
}

@Composable
private fun Suggestions(state: SortState, onAction: (SortAction) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
        BasicText(stringResource(R.string.suggestions), style = KvType.label.copy(color = KvColors.neutral400))
        if (state.suggestions.isEmpty()) {
            BasicText(stringResource(R.string.no_suggestions), style = KvType.label.copy(color = KvColors.neutral500))
        }
        FlowRow(horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            state.suggestions.forEach { suggestion ->
                Chip(
                    suggestion.item.name,
                    selected = state.draft?.itemId == suggestion.item.id,
                    onClick = { onAction(SortAction.OnPickItem(suggestion.item)) },
                    trailing = if (suggestion.confident) {
                        { KvIcon(PhosphorIcons.Sparkle, contentDescription = null, size = 13.dp, tint = KvColors.accent) }
                    } else {
                        null
                    },
                )
            }
            Chip(
                stringResource(R.string.create_item),
                selected = state.draft?.isNew == true,
                onClick = { onAction(SortAction.OnCreateItem) },
                enabled = state.canManageTaxonomy,
            )
        }
        if (!state.canManageTaxonomy) {
            BasicText(stringResource(R.string.no_taxonomy_permission), style = KvType.hint)
        }
        KvField(
            label = stringResource(R.string.search_items),
            value = state.itemSearch,
            onValueChange = { onAction(SortAction.OnSearchChange(it)) },
            modifier = Modifier.fillMaxWidth(),
        )
        state.searchHits.forEach { hit: CanonicalItem ->
            Chip(hit.name, selected = state.draft?.itemId == hit.id, onClick = { onAction(SortAction.OnPickItem(hit)) })
        }
    }
}

@Composable
private fun Draft(draft: SortDraft, state: SortState, onAction: (SortAction) -> Unit) {
    val shape = RoundedCornerShape(KvRadius.md)
    val category = state.categories.flatMap { listOf(it) + it.children }.firstOrNull { it.id == draft.categoryId }?.name
    val meal = state.meals.firstOrNull { it.id == draft.mealId }?.name
    val none = stringResource(CoreR.string.none)

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(KvColors.accentTint10, shape)
            .border(1.dp, KvColors.accentTint30, shape)
            .padding(14.dp),
        verticalArrangement = Arrangement.spacedBy(KvSpace.s3),
    ) {
        if (draft.isNew) {
            KvField(
                label = stringResource(R.string.new_item_name),
                value = draft.name,
                onValueChange = { onAction(SortAction.OnNewNameChange(it)) },
                modifier = Modifier.fillMaxWidth(),
            )
        } else {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                KvIcon(PhosphorIcons.CheckCircle, contentDescription = null, tint = KvColors.accent)
                BasicText(draft.name, style = KvType.small)
            }
        }
        KvSelectField(
            label = stringResource(CoreR.string.category),
            value = category ?: none,
            onClick = { onAction(SortAction.OnOpenPicker(SortPicker.Category)) },
            modifier = Modifier.fillMaxWidth(),
        )
        KvSelectField(
            label = stringResource(CoreR.string.meal),
            value = meal ?: none,
            onClick = { onAction(SortAction.OnOpenPicker(SortPicker.Meal)) },
            modifier = Modifier.fillMaxWidth(),
        )
        KvCheckbox(draft.rememberAlias, { onAction(SortAction.OnRememberAlias(it)) }) {
            BasicText(stringResource(R.string.remember_alias), style = KvType.small)
        }
        KvCheckbox(
            draft.setDefaults,
            { onAction(SortAction.OnSetDefaults(it)) },
            enabled = state.canManageTaxonomy,
        ) {
            Column {
                BasicText(stringResource(R.string.set_defaults), style = KvType.small)
                BasicText(
                    stringResource(if (state.canManageTaxonomy) R.string.set_defaults_hint else R.string.no_taxonomy_permission),
                    style = KvType.hint,
                )
            }
        }
    }
}

@Composable
private fun Sheets(state: SortState, onAction: (SortAction) -> Unit) {
    when (state.picking) {
        SortPicker.Category -> CategoryPickerSheet(
            categories = state.categories,
            selected = state.draft?.categoryId,
            onPick = { onAction(SortAction.OnPickCategory(it)) },
            onDismiss = { onAction(SortAction.OnOpenPicker(null)) },
            onNewCategory = { onAction(SortAction.OnNewCategory) }.takeIf { state.canManageTaxonomy },
        )
        SortPicker.Meal -> MealPickerSheet(
            meals = state.meals,
            selected = state.draft?.mealId,
            onPick = { onAction(SortAction.OnPickMeal(it)) },
            onDismiss = { onAction(SortAction.OnOpenPicker(null)) },
        )
        null -> Unit
    }

    state.newCategory?.let { draft ->
        val parent = state.categories.firstOrNull { it.id == draft.parentId }?.name
        KvDialog(
            title = stringResource(CoreR.string.new_category),
            onDismiss = { onAction(SortAction.OnNewCategoryChange(null)) },
            actions = {
                KvButton(
                    stringResource(CoreR.string.cancel),
                    { onAction(SortAction.OnNewCategoryChange(null)) },
                    style = KvButtonStyle.Ghost,
                )
                KvButton(
                    stringResource(CoreR.string.save),
                    { onAction(SortAction.OnSaveNewCategory) },
                    style = KvButtonStyle.Primary,
                    enabled = draft.name.isNotBlank(),
                    busy = draft.isBusy,
                )
            },
        ) {
            KvField(
                label = stringResource(CoreR.string.category_name),
                value = draft.name,
                onValueChange = { onAction(SortAction.OnNewCategoryChange(draft.copy(name = it, error = null))) },
                placeholder = stringResource(CoreR.string.category_name_placeholder),
                modifier = Modifier.fillMaxWidth(),
            )
            KvSelectField(
                label = stringResource(CoreR.string.category_parent),
                value = parent ?: stringResource(CoreR.string.category_top_level),
                onClick = { onAction(SortAction.OnNewCategoryChange(draft.copy(pickingParent = true))) },
                modifier = Modifier.fillMaxWidth(),
            )
            draft.error?.let { KvCallout(it.asString()) }
        }

        if (draft.pickingParent) {
            val top = stringResource(CoreR.string.category_top_level)
            PickerSheet(
                title = stringResource(CoreR.string.category_parent),
                rows = listOf<PickerRow<String?>>(PickerRow.Option(null, top)) +
                    state.categories.map { PickerRow.Option(it.id, it.name) },
                selected = draft.parentId,
                onPick = {
                    onAction(SortAction.OnNewCategoryChange(draft.copy(parentId = it, pickingParent = false)))
                },
                onDismiss = { onAction(SortAction.OnNewCategoryChange(draft.copy(pickingParent = false))) },
            )
        }
    }
}

@Preview
@Composable
private fun SortScreenPreview() {
    val line = UnassignedLine(
        "l", "HAKKET OKSEKØD 8-12%", BigDecimal.ONE, BigDecimal("38.95"), BigDecimal("38.95"), "r", "Netto",
        Instant.parse("2026-08-18T10:00:00Z"), "Sofie",
    )
    KvitteriaTheme {
        SortScreen(
            SortState(isLoading = false, queue = listOf(line), canCategorize = true, canManageTaxonomy = true),
            onAction = {},
            onOpenReceipts = {},
        )
    }
}
