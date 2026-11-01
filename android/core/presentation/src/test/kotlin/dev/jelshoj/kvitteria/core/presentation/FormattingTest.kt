/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import java.math.BigDecimal
import java.time.Clock
import java.time.Instant
import java.time.YearMonth
import java.time.ZoneId
import java.util.Locale
import java.util.TimeZone
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource

class FormattingTest {
    private val danish = Locale.forLanguageTag("da")

    @ParameterizedTest
    @CsvSource(
        "2438.27,'2.438,27 kr'",
        "-12.5,'−12,50 kr'",
        "284.30,'284,30 kr'",
        "0,'0,00 kr'",
        "1234567.8,'1.234.567,80 kr'",
        "999.995,'1.000,00 kr'",
        "-0.001,'0,00 kr'",
    )
    fun `amounts use the Danish form`(value: String, expected: String) {
        assertThat(KrFormat.amount(BigDecimal(value))).isEqualTo(expected)
    }

    @Test
    fun `a missing amount is a dash`() {
        assertThat(KrFormat.amount(null)).isEqualTo("—")
    }

    @Test
    fun `an amount keeps its exact value`() {
        assertThat(KrFormat.amount(BigDecimal("0.1") + BigDecimal("0.2"))).isEqualTo("0,30 kr")
    }

    @ParameterizedTest
    @CsvSource("4.2,'+4,2 %'", "-3,'−3,0 %'", "0,'+0,0 %'")
    fun `a change carries its direction`(value: String, expected: String) {
        assertThat(KrFormat.change(BigDecimal(value))).isEqualTo(expected)
    }

    @Test
    fun `a null change is a dash`() {
        assertThat(KrFormat.change(null)).isEqualTo("—")
    }

    @ParameterizedTest
    @CsvSource("2.000,2", "0.750,'0,75'", "1000,1000")
    fun `a quantity shows the digits it needs`(value: String, expected: String) {
        assertThat(KrFormat.quantity(BigDecimal(value))).isEqualTo(expected)
    }

    @ParameterizedTest
    @CsvSource("'12,50',12.50", "12.50,12.50", "'1.234,50',1234.50", "1.234,1234", "' 7 ',7", "'−3,5',-3.5", "'12,50 kr',12.50")
    fun `typed numbers accept a decimal comma`(text: String, expected: String) {
        assertThat(KrFormat.parse(text)).isEqualTo(BigDecimal(expected))
    }

    @ParameterizedTest
    @CsvSource("abc", "''", "'1,2,3'")
    fun `something else isn't a number`(text: String) {
        assertThat(KrFormat.parse(text)).isNull()
    }

    @Test
    fun `a typed figure round-trips through the field`() {
        assertThat(KrFormat.parse(KrFormat.editable(BigDecimal("1234.50")))).isEqualTo(BigDecimal("1234.50"))
    }

    @Test
    fun `dates read in Danish time on a device in New York`() {
        val original = TimeZone.getDefault()
        TimeZone.setDefault(TimeZone.getTimeZone("America/New_York"))
        try {
            // 00:30 on the first in Copenhagen is still the 31st in UTC and in New York.
            val instant = Instant.parse("2026-07-31T22:30:00Z")
            assertThat(DanishTime.date(instant, danish)).isEqualTo("1. aug 2026")
            assertThat(DanishTime.monthOf(instant)).isEqualTo(YearMonth.of(2026, 8))
            assertThat(DanishTime.dateTime(instant, danish)).isEqualTo("1. aug 2026 00:30")
        } finally {
            TimeZone.setDefault(original)
        }
    }

    @Test
    fun `the current month is the Danish one`() {
        val clock = Clock.fixed(Instant.parse("2026-07-31T22:30:00Z"), ZoneId.of("America/New_York"))
        assertThat(DanishTime.currentMonth(clock)).isEqualTo(YearMonth.of(2026, 8))
    }

    @Test
    fun `English dates and months`() {
        val instant = Instant.parse("2026-08-18T10:00:00Z")
        assertThat(DanishTime.date(instant, Locale.ENGLISH)).isEqualTo("18 Aug 2026")
        assertThat(DanishTime.month(YearMonth.of(2026, 8), Locale.ENGLISH)).isEqualTo("August 2026")
        assertThat(DanishTime.month(YearMonth.of(2026, 3), danish)).isEqualTo("Marts 2026")
        assertThat(DanishTime.shortMonth(YearMonth.of(2026, 3), danish)).isEqualTo("mar")
    }

    @Test
    fun `a 502 detail is shown verbatim and a 403 is localized`() {
        assertThat(ApiError(DataError.Network.BAD_GATEWAY, "Folders: INBOX").toUiText())
            .isEqualTo(UiText.DynamicString("Folders: INBOX"))
        assertThat(ApiError(DataError.Network.FORBIDDEN, "Forbidden").toUiText())
            .isEqualTo(UiText.StringResource(R.string.error_forbidden))
    }

    @Test
    fun `a 409 without a detail falls back to a localized message`() {
        assertThat(ApiError(DataError.Network.CONFLICT).toUiText())
            .isEqualTo(UiText.StringResource(R.string.error_unknown))
    }

    @Test
    fun `no connection is localized`() {
        assertThat(ApiError(DataError.Network.NO_INTERNET).toUiText())
            .isEqualTo(UiText.StringResource(R.string.error_no_connection))
    }
}
