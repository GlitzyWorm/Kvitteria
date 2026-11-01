/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import androidx.navigation3.runtime.EntryProviderScope
import androidx.navigation3.runtime.NavKey
import kotlinx.serialization.Serializable
import org.koin.core.module.dsl.viewModelOf
import org.koin.dsl.module

@Serializable data object InsightsRoute : NavKey

fun EntryProviderScope<NavKey>.insightsEntries() {
    entry<InsightsRoute> { InsightsRoot() }
}

val insightsPresentationModule = module {
    viewModelOf(::InsightsViewModel)
}
