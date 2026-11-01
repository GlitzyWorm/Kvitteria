/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.serialization

import assertk.assertFailure
import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isInstanceOf
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptStatus
import java.math.BigDecimal
import java.time.Instant
import java.time.YearMonth
import kotlinx.serialization.SerializationException
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource
import org.junit.jupiter.params.provider.ValueSource

class SerializersTest {
    private fun money(json: String) = KvitteriaJson.decodeFromString(MoneySerializer, json)

    @ParameterizedTest
    @ValueSource(strings = ["12345.67", "284.30", "0.1", "-12.50", "99999999999999.99"])
    fun `amounts keep their exact text`(text: String) {
        assertThat(money(text)).isEqualTo(BigDecimal(text))
    }

    @Test
    fun `decimal can also arrive as a string`() {
        assertThat(money("\"12345.67\"")).isEqualTo(BigDecimal("12345.67"))
    }

    @Test
    fun `an amount written back is the same text`() {
        assertThat(KvitteriaJson.encodeToString(MoneySerializer, BigDecimal("284.30"))).isEqualTo("284.30")
    }

    @Test
    fun `something that is not an amount fails to decode`() {
        assertFailure { money("\"many\"") }.isInstanceOf(SerializationException::class)
    }

    private fun instant(text: String) = KvitteriaJson.decodeFromString(InstantSerializer, "\"$text\"")

    @Test
    fun `an instant with an offset keeps it`() {
        assertThat(instant("2026-03-01T00:30:00+01:00")).isEqualTo(Instant.parse("2026-02-28T23:30:00Z"))
    }

    @Test
    fun `an instant with Z is read as UTC`() {
        assertThat(instant("2026-03-01T10:15:30.1234567Z")).isEqualTo(Instant.parse("2026-03-01T10:15:30.1234567Z"))
    }

    @Test
    fun `a missing offset is read as UTC`() {
        assertThat(instant("2026-03-01T10:15:30")).isEqualTo(Instant.parse("2026-03-01T10:15:30Z"))
    }

    @Test
    fun `something that is not an instant fails to decode`() {
        assertFailure { instant("yesterday") }.isInstanceOf(SerializationException::class)
    }

    @Test
    fun `a month round-trips in the wire form`() {
        assertThat(KvitteriaJson.decodeFromString(YearMonthSerializer, "\"2026-03\"")).isEqualTo(YearMonth.of(2026, 3))
        assertThat(KvitteriaJson.encodeToString(YearMonthSerializer, YearMonth.of(2026, 3))).isEqualTo("\"2026-03\"")
    }

    @ParameterizedTest
    @CsvSource("0,Queued", "1,Extracting", "2,Extracted", "3,Completed", "4,Failed", "5,Unknown", "-1,Unknown")
    fun `status is a number`(wire: String, expected: ReceiptStatus) {
        assertThat(KvitteriaJson.decodeFromString(ReceiptStatusSerializer, wire)).isEqualTo(expected)
    }

    @Test
    fun `other enums are names, and an unknown name is Unknown`() {
        assertThat(KvitteriaJson.decodeFromString(RoleSerializer, "\"Admin\"")).isEqualTo(Role.Admin)
        assertThat(KvitteriaJson.decodeFromString(RoleSerializer, "\"Owner\"")).isEqualTo(Role.Unknown)
        assertThat(KvitteriaJson.decodeFromString(MemberStatusSerializer, "\"Disabled\"")).isEqualTo(MemberStatus.Disabled)
        assertThat(KvitteriaJson.decodeFromString(MemberStatusSerializer, "\"Paused\"")).isEqualTo(MemberStatus.Unknown)
    }
}
