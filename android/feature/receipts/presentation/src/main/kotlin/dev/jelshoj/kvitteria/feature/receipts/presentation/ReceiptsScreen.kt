/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

@file:OptIn(ExperimentalMaterial3Api::class)

package dev.jelshoj.kvitteria.feature.receipts.presentation

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.text.BasicText
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import dev.jelshoj.kvitteria.core.designsystem.KvAmount
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvField
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvRule
import dev.jelshoj.kvitteria.core.designsystem.KvSegmented
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvSpinner
import dev.jelshoj.kvitteria.core.designsystem.KvTag
import dev.jelshoj.kvitteria.core.designsystem.KvTagStyle
import dev.jelshoj.kvitteria.core.designsystem.KvToastBox
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.KvitteriaTheme
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.rememberKvToastState
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptStatus
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.ErrorState
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import dev.jelshoj.kvitteria.core.presentation.LoadingState
import dev.jelshoj.kvitteria.core.presentation.MessageState
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.OnStart
import java.math.BigDecimal
import java.time.Instant
import org.koin.compose.viewmodel.koinViewModel

@Composable
fun ReceiptsRoot(
    onOpenReceipt: (String) -> Unit,
    viewModel: ReceiptsViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.messages) { toast.show(it.asString(context)) }

    KvToastBox(toast) {
        ReceiptsScreen(state = state, onAction = viewModel::onAction, onOpenReceipt = onOpenReceipt)
    }
}

@Composable
private fun ReceiptRow(receipt: Receipt, onOpen: () -> Unit) {
    Column {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 56.dp)
                .clickable(role = Role.Button, onClick = onOpen)
                .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(KvSpace.s3),
        ) {
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                BasicText(receipt.merchant, style = KvType.control, maxLines = 1, overflow = TextOverflow.Ellipsis)
                BasicText(DanishTime.date(receipt.purchaseDate), style = KvType.hint)
            }
            if (!receipt.reconciled) KvTag(stringResource(R.string.unreconciled), style = KvTagStyle.Outline)
            KvAmount(KrFormat.amount(receipt.grandTotal))
        }
        KvRule(color = KvColors.textWash8)
    }
}

@Composable
fun ReceiptsScreen(
    state: ReceiptsState,
    onAction: (ReceiptsAction) -> Unit,
    onOpenReceipt: (String) -> Unit,
) {
    val list = rememberLazyListState()
    val nearEnd by remember {
        derivedStateOf {
            val last = list.layoutInfo.visibleItemsInfo.lastOrNull()?.index ?: 0
            last >= list.layoutInfo.totalItemsCount - 5
        }
    }
    LaunchedEffect(nearEnd, state.receipts.size) {
        if (nearEnd) onAction(ReceiptsAction.OnLoadMore)
    }

    Column(Modifier.fillMaxSize().statusBarsPadding()) {
        Column(
            modifier = Modifier.padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
            verticalArrangement = Arrangement.spacedBy(KvSpace.s3),
        ) {
            KvHeading(stringResource(R.string.receipts_title))
            KvField(
                label = stringResource(R.string.search),
                value = state.search,
                onValueChange = { onAction(ReceiptsAction.OnSearchChange(it)) },
                placeholder = stringResource(R.string.search_placeholder),
                modifier = Modifier.fillMaxWidth(),
            )
            KvSegmented(
                options = listOf(stringResource(R.string.filter_all), stringResource(R.string.unreconciled)),
                selected = if (state.unreconciledOnly) 1 else 0,
                onSelect = { onAction(ReceiptsAction.OnFilterChange(it == 1)) },
                modifier = Modifier.fillMaxWidth(),
            )
        }

        PullToRefreshBox(
            isRefreshing = state.isRefreshing,
            onRefresh = { onAction(ReceiptsAction.OnRefresh) },
            modifier = Modifier.weight(1f),
        ) {
            when {
                state.receipts.isNotEmpty() -> LazyColumn(
                    state = list,
                    modifier = Modifier.fillMaxSize(),
                    contentPadding = PaddingValues(bottom = KvSpace.s8),
                ) {
                    items(state.receipts, key = { it.id }) { receipt ->
                        ReceiptRow(receipt) { onOpenReceipt(receipt.id) }
                    }
                    if (state.isLoadingMore) {
                        item {
                            Box(Modifier.fillMaxWidth().padding(KvSpace.s4), contentAlignment = Alignment.Center) {
                                KvSpinner()
                            }
                        }
                    }
                }
                state.error != null -> ErrorState(state.error.asString(), { onAction(ReceiptsAction.OnRefresh) })
                state.isLoading -> LoadingState()
                state.search.isNotBlank() || state.unreconciledOnly -> MessageState(
                    glyph = PhosphorIcons.MagnifyingGlass,
                    title = stringResource(R.string.no_results),
                )
                else -> MessageState(
                    glyph = PhosphorIcons.Receipt,
                    title = stringResource(R.string.no_receipts_title),
                    body = stringResource(R.string.no_receipts_body),
                )
            }
        }
    }
}

@Preview
@Composable
private fun ReceiptsScreenPreview() {
    val receipt = Receipt(
        "r1", "Netto", Instant.parse("2026-08-18T10:00:00Z"), BigDecimal("284.30"), BigDecimal("3.00"), false,
        ReceiptStatus.Completed, emptyList(), emptyList(), null, null, null,
    )
    KvitteriaTheme {
        ReceiptsScreen(ReceiptsState(receipts = listOf(receipt), isLoading = false), onAction = {}, onOpenReceipt = {})
    }
}
