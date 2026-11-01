/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.insights.CategoryAmount
import java.math.BigDecimal
import org.junit.jupiter.api.Test

class CategoryComparisonTest {
    private fun row(name: String, amount: String = "100", change: String? = null) =
        CategoryAmount(name, name, BigDecimal(amount), null, change?.let(::BigDecimal))

    @Test
    fun `movers name the biggest rise and fall`() {
        val movers = CategoryComparison.movers(listOf(row("a", change = "4.2"), row("b", change = "-8"), row("c", change = "12")))
        assertThat(movers?.rise?.name).isEqualTo("c")
        assertThat(movers?.fall?.name).isEqualTo("b")
    }

    @Test
    fun `fewer than two rows moving by 1 percent gives no note`() {
        assertThat(CategoryComparison.movers(listOf(row("a", change = "12"), row("b", change = "0.5")))).isNull()
    }

    @Test
    fun `exactly 1 percent counts`() {
        val movers = CategoryComparison.movers(listOf(row("a", change = "1"), row("b", change = "-1")))
        assertThat(movers?.rise?.name).isEqualTo("a")
    }

    @Test
    fun `a null change never counts as a mover`() {
        assertThat(CategoryComparison.movers(listOf(row("a", change = "12"), row("b", change = null)))).isNull()
    }

    @Test
    fun `sorting by size is largest first`() {
        val sorted = CategoryComparison.sorted(listOf(row("a", "1"), row("b", "3"), row("c", "2")), CategorySort.Size)
        assertThat(sorted.map { it.name }).containsExactly("b", "c", "a")
    }

    @Test
    fun `sorting by change puts null changes last`() {
        val sorted = CategoryComparison.sorted(
            listOf(row("a", change = null), row("b", change = "-3"), row("c", change = "8")),
            CategorySort.Change,
        )
        assertThat(sorted.map { it.name }).containsExactly("c", "b", "a")
    }
}
