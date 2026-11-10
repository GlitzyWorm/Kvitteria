/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.isBetween
import assertk.assertions.isEqualTo
import assertk.assertions.isGreaterThan
import assertk.assertions.isLessThan
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.insights.PriceMonth
import java.math.BigDecimal
import java.time.YearMonth
import org.junit.jupiter.api.Test

class PriceAxisTest {
    private fun month(index: Int, price: String?) =
        PriceMonth(YearMonth.of(2026, 1).plusMonths(index.toLong()), price?.let(::BigDecimal), if (price == null) 0 else 1)

    @Test
    fun `a narrow range gets rounded ticks that include both ends`() {
        assertThat(PriceAxis.ticks(14.45, 14.95)).containsExactly(14.4, 14.6, 14.8, 15.0)
    }

    @Test
    fun `a wide range stays within five ticks and covers both ends`() {
        val ticks = PriceAxis.ticks(0.99, 1249.0)

        assertThat(ticks.size).isBetween(3, 5)
        assertThat(ticks.first()).isLessThan(0.99 + 1e-9)
        assertThat(ticks.last()).isGreaterThan(1249.0 - 1e-9)
    }

    @Test
    fun `one price is widened by a step either side`() {
        val ticks = PriceAxis.ticks(14.95, 14.95)

        assertThat(ticks.size).isBetween(3, 5)
        assertThat(ticks.first()).isLessThan(14.95)
        assertThat(ticks.last()).isGreaterThan(14.95)
    }

    @Test
    fun `a range of exactly one step still gets at least three ticks`() {
        assertThat(PriceAxis.ticks(10.0, 20.0).size).isBetween(3, 5)
    }

    @Test
    fun `the nearest priced month to a gap is the closest one`() {
        val months = listOf(month(0, "10"), month(1, null), month(2, null), month(3, null), month(4, "12"))

        assertThat(PriceAxis.nearestPriced(months, 1)).isEqualTo(0)
        assertThat(PriceAxis.nearestPriced(months, 3)).isEqualTo(4)
    }

    @Test
    fun `a tie between two priced months goes to the later one`() {
        assertThat(PriceAxis.nearestPriced(listOf(month(0, "10"), month(1, null), month(2, "12")), 1)).isEqualTo(2)
    }

    @Test
    fun `a priced month is its own nearest`() {
        assertThat(PriceAxis.nearestPriced(listOf(month(0, "10"), month(1, "11")), 0)).isEqualTo(0)
    }

    @Test
    fun `no priced month has no nearest`() {
        assertThat(PriceAxis.nearestPriced(listOf(month(0, null)), 0)).isNull()
    }
}
