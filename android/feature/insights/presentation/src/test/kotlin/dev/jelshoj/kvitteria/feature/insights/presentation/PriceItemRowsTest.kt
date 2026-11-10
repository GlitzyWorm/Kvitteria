/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.isEmpty
import assertk.assertions.isEqualTo
import dev.jelshoj.kvitteria.core.domain.insights.PricedItem
import dev.jelshoj.kvitteria.core.domain.insights.PricedName
import dev.jelshoj.kvitteria.core.presentation.PickerRow
import org.junit.jupiter.api.Test

class PriceItemRowsTest {
    private val milk = PricedItem(
        "milk", "Mælk", 5,
        listOf(PricedName("arla letmælk 1l", "ARLA LETMÆLK 1L", 3), PricedName("minimælk", "Minimælk", 2)),
    )
    private val butter = PricedItem("butter", "Smør", 2, listOf(PricedName("kærgården", "KÆRGÅRDEN", 2)))
    private val items = listOf(milk, butter)

    private fun labels(rows: List<PickerRow<PriceSelection>>) = rows.map {
        when (it) {
            is PickerRow.Section -> "# ${it.label}"
            is PickerRow.Option -> (if (it.indented) "  " else "") + it.label
        }
    }

    @Test
    fun `an item with several names lists them indented beneath it`() {
        assertThat(labels(PriceItemRows.rows(listOf(milk), ""))).containsExactly(
            "# Mælk", "Mælk", "  ARLA LETMÆLK 1L", "  Minimælk",
        )
    }

    @Test
    fun `an item with one name is listed alone`() {
        assertThat(labels(PriceItemRows.rows(listOf(butter), ""))).containsExactly("Smør")
    }

    @Test
    fun `picking a name carries its key and picking the item carries none`() {
        val options = PriceItemRows.rows(listOf(milk), "").filterIsInstance<PickerRow.Option<PriceSelection>>()

        assertThat(options.map { it.value }).containsExactly(
            PriceSelection("milk"),
            PriceSelection("milk", "arla letmælk 1l"),
            PriceSelection("milk", "minimælk"),
        )
    }

    @Test
    fun `a matching name keeps its item and hides the other names`() {
        assertThat(labels(PriceItemRows.rows(items, "arla"))).containsExactly("# Mælk", "Mælk", "  ARLA LETMÆLK 1L")
    }

    @Test
    fun `a matching item keeps all its names`() {
        assertThat(labels(PriceItemRows.rows(items, "mælk"))).containsExactly(
            "# Mælk", "Mælk", "  ARLA LETMÆLK 1L", "  Minimælk",
        )
    }

    @Test
    fun `a name of a single-name item keeps the item without the name row`() {
        assertThat(labels(PriceItemRows.rows(items, "kærgården"))).containsExactly("Smør")
    }

    @Test
    fun `nothing matching gives no rows`() {
        assertThat(PriceItemRows.rows(items, "xyz")).isEmpty()
    }

    @Test
    fun `case is ignored and Danish letters stay distinct`() {
        assertThat(PriceItemRows.rows(items, "MÆLK")).isEqualTo(PriceItemRows.rows(items, "mælk"))
        assertThat(PriceItemRows.rows(items, "maelk")).isEmpty()
    }
}
