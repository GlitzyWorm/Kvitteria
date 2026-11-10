/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import dev.jelshoj.kvitteria.core.domain.insights.PriceMonth
import kotlin.math.abs
import kotlin.math.ceil
import kotlin.math.floor
import kotlin.math.max
import kotlin.math.pow
import kotlin.math.round

/** The rules behind the price chart that need no drawing, so they can be tested. */
object PriceAxis {
    private const val MAX_TICKS = 5
    private const val MIN_TICKS = 3
    private val MULTIPLES = listOf(1.0, 2.0, 2.5, 5.0)

    /** Steps from 0.01 kr upwards, each one of {1, 2, 2.5, 5} times a power of ten. */
    private val steps: List<Double> = (-2..6).flatMap { exponent -> MULTIPLES.map { it * 10.0.pow(exponent) } }

    /**
     * Three to five rounded values that cover [low]..[high], lowest first. Equal prices are widened by one
     * step either side so a single point sits inside a labelled range rather than on a collapsed axis.
     */
    fun ticks(low: Double, high: Double): List<Double> {
        val from = minOf(low, high)
        val to = maxOf(low, high)
        if (from == to) {
            val step = steps.first { it >= max(abs(from) * 0.2, 1.0) / (MAX_TICKS - 1) }
            return at(floor(from / step) - 1, ceil(to / step) + 1, step)
        }

        val step = steps.first { count(from, to, it) <= MAX_TICKS }
        val first = floor(from / step)
        var last = ceil(to / step)
        if (last - first + 1 < MIN_TICKS) last = first + (MIN_TICKS - 1)
        return at(first, last, step)
    }

    private fun count(low: Double, high: Double, step: Double): Int =
        (ceil(high / step) - floor(low / step)).toInt() + 1

    private fun at(first: Double, last: Double, step: Double): List<Double> =
        generateSequence(first) { it + 1 }.takeWhile { it <= last }.map { round(it * step * 1e6) / 1e6 }.toList()

    /**
     * The index of the priced month nearest [index], or null when no month has a price. A tie goes to the
     * later month, because the latest price is the one the figures lead with.
     */
    fun nearestPriced(months: List<PriceMonth>, index: Int): Int? =
        months.indices
            .filter { months[it].price != null }
            .minWithOrNull(compareBy<Int> { abs(it - index) }.thenByDescending { it })
}
