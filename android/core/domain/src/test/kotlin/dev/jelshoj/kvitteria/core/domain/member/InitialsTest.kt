/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.member

import assertk.assertThat
import assertk.assertions.isEqualTo
import java.util.Locale
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource
import org.junit.jupiter.params.provider.NullSource
import org.junit.jupiter.params.provider.ValueSource

class InitialsTest {
    @ParameterizedTest
    @CsvSource("Sofie Jelshøj,SJ", "mathias bruun,MB", "Anne Marie Holm,AH")
    fun `takes the first and last word`(name: String, expected: String) {
        assertThat(Initials.from(name)).isEqualTo(expected)
    }

    @ParameterizedTest
    @CsvSource("Sofie,S", "øre,Ø")
    fun `takes one letter from one word`(name: String, expected: String) {
        assertThat(Initials.from(name)).isEqualTo(expected)
    }

    @ParameterizedTest
    @NullSource
    @ValueSource(strings = ["", "   ", "\t\n"])
    fun `falls back when there is no name`(name: String?) {
        assertThat(Initials.from(name)).isEqualTo(Initials.UNKNOWN)
    }

    @Test
    fun `ignores extra whitespace between words`() {
        assertThat(Initials.from("  Sofie   Jelshøj  ")).isEqualTo("SJ")
    }

    @ParameterizedTest
    @CsvSource("Æble Ørsted,ÆØ", "åse Ågesen,ÅÅ")
    fun `handles Danish letters`(name: String, expected: String) {
        assertThat(Initials.from(name)).isEqualTo(expected)
    }

    @Test
    fun `uppercases independently of the current locale`() {
        val original = Locale.getDefault()
        try {
            Locale.setDefault(Locale.forLanguageTag("tr-TR"))
            assertThat(Initials.from("ida ingemann")).isEqualTo("II")
        } finally {
            Locale.setDefault(original)
        }
    }
}
