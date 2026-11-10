/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import dev.jelshoj.kvitteria.core.domain.insights.PricedItem
import dev.jelshoj.kvitteria.core.domain.insights.PricedName
import dev.jelshoj.kvitteria.core.presentation.PickerRow
import java.util.Locale

/** What Priser shows: a whole item, or one raw name ([nameKey]) of it. */
data class PriceSelection(val itemId: String, val nameKey: String? = null)

/** The item sheet's rows: each item with its raw names indented beneath it, and the search over both. */
object PriceItemRows {
    fun rows(items: List<PricedItem>, query: String): List<PickerRow<PriceSelection>> =
        filter(items, query).flatMap { (item, names) ->
            if (names.isEmpty()) {
                listOf(PickerRow.Option(PriceSelection(item.id), item.name))
            } else {
                buildList {
                    add(PickerRow.Section(item.name))
                    add(PickerRow.Option(PriceSelection(item.id), item.name))
                    names.forEach { add(PickerRow.Option(PriceSelection(item.id, it.key), it.label, indented = true)) }
                }
            }
        }

    /**
     * Keeps the items and names containing [query], with the names to show beneath each item. A matching
     * item keeps all its names, a matching name keeps its item, and an item with one name shows no name
     * row: it would only repeat the item.
     */
    fun filter(items: List<PricedItem>, query: String): List<Pair<PricedItem, List<PricedName>>> {
        val needle = query.trim().lowercase(Locale.ROOT)
        return items.mapNotNull { item ->
            val itemMatches = needle.isEmpty() || item.name.lowercase(Locale.ROOT).contains(needle)
            val matching = if (itemMatches) {
                item.names
            } else {
                item.names.filter { it.label.lowercase(Locale.ROOT).contains(needle) || it.key.contains(needle) }
            }
            when {
                !itemMatches && matching.isEmpty() -> null
                item.names.size < 2 -> item to emptyList()
                else -> item to matching
            }
        }
    }
}
