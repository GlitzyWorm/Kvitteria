/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.receipts.presentation

import androidx.compose.runtime.Composable
import androidx.navigation3.runtime.EntryProviderScope
import androidx.navigation3.runtime.NavKey
import kotlinx.serialization.Serializable
import org.koin.core.module.dsl.viewModel
import org.koin.core.module.dsl.viewModelOf
import org.koin.dsl.module

@Serializable data object ReceiptsRoute : NavKey

@Serializable data class ReceiptDetailRoute(val id: String) : NavKey

fun EntryProviderScope<NavKey>.receiptsEntries(
    onOpenReceipt: (String) -> Unit,
    onBack: () -> Unit,
    addReceipt: @Composable () -> Unit = {},
) {
    entry<ReceiptsRoute> { ReceiptsRoot(onOpenReceipt = onOpenReceipt, addReceipt = addReceipt) }
    entry<ReceiptDetailRoute> { route -> ReceiptDetailRoot(id = route.id, onBack = onBack, onOpenReceipt = onOpenReceipt) }
}

val receiptsPresentationModule = module {
    viewModelOf(::ReceiptsViewModel)
    viewModel { params -> ReceiptDetailViewModel(params.get(), get(), get(), get(), get(), get()) }
}
