/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import java.math.BigDecimal
import java.math.RoundingMode

/**
 * Amounts in the Danish form in every language, by hand rather than through `NumberFormat`, so the true
 * minus sign and the placement of `kr` are exact. A missing amount is a dash, never a zero.
 */
object KrFormat {
    const val MISSING = "—"
    private const val MINUS = '−'

    private fun grouped(value: BigDecimal, decimals: Int): String {
        val plain = value.abs().setScale(decimals, RoundingMode.HALF_UP).toPlainString()
        val whole = plain.substringBefore('.')
        val fraction = plain.substringAfter('.', "")
        val thousands = whole.reversed().chunked(3).joinToString(".").reversed()
        return if (fraction.isEmpty()) thousands else "$thousands,$fraction"
    }

    private fun isNegative(value: BigDecimal, decimals: Int) =
        value.setScale(decimals, RoundingMode.HALF_UP).signum() < 0

    fun amount(value: BigDecimal?, decimals: Int = 2): String {
        if (value == null) return MISSING
        val sign = if (isNegative(value, decimals)) MINUS.toString() else ""
        return "$sign${grouped(value, decimals)} kr"
    }

    /** A change with its direction, as `+4,2 %` or `−3,0 %`. */
    fun change(percent: BigDecimal?): String {
        if (percent == null) return MISSING
        val sign = if (isNegative(percent, 1)) MINUS else '+'
        return "$sign${grouped(percent, 1)} %"
    }

    /** A share, as `12,5 %`. */
    fun share(percent: BigDecimal?): String {
        if (percent == null) return MISSING
        return "${grouped(percent, 1)} %"
    }

    /** A quantity with only the digits it needs, as `2` or `0,75`. */
    fun quantity(value: BigDecimal): String {
        val plain = value.stripTrailingZeros().toPlainString()
        return plain.replace('.', ',').replace('-', MINUS)
    }

    /** The plain figure for an input field, with a decimal comma and no grouping. */
    fun editable(value: BigDecimal): String = value.toPlainString().replace('.', ',')

    /**
     * What a person typed, accepting a decimal comma or point, and null when it isn't a number. A point
     * followed by exactly three digits reads as thousands, because that's how the app writes them.
     */
    fun parse(text: String): BigDecimal? {
        val trimmed = text.trim().replace(MINUS, '-').removeSuffix("kr").trim()
        if (trimmed.isEmpty()) return null
        val normalized = if (',' in trimmed) {
            trimmed.replace(".", "").replace(',', '.')
        } else if (Regex("""-?\d{1,3}(\.\d{3})+""").matches(trimmed)) {
            trimmed.replace(".", "")
        } else {
            trimmed
        }
        return normalized.toBigDecimalOrNull()
    }
}
