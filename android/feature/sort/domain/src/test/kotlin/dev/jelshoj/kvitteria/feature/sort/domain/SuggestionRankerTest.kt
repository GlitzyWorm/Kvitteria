/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.sort.domain

import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.hasSize
import assertk.assertions.isEmpty
import assertk.assertions.isEqualTo
import assertk.assertions.isFalse
import assertk.assertions.isTrue
import dev.jelshoj.kvitteria.core.domain.taxonomy.CanonicalItem
import java.util.Locale
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource

class SuggestionRankerTest {
    private fun item(name: String) = CanonicalItem(name, name, null, null)

    private val items = listOf(
        item("Skummetmælk 1 L"),
        item("Hakket oksekød 8-12%"),
        item("Hakkede tomater"),
        item("Æg, 10 stk."),
        item("Rødvinseddike"),
    )

    @Test
    fun `an exact word scores its length plus four`() {
        assertThat(SuggestionRanker.score(listOf("oksekød"), listOf("oksekød"))).isEqualTo(11)
    }

    @Test
    fun `four shared letters on long words score three`() {
        assertThat(SuggestionRanker.score(listOf("hakket"), listOf("hakkede"))).isEqualTo(3)
    }

    @Test
    fun `short words never match on a prefix`() {
        assertThat(SuggestionRanker.score(listOf("abc"), listOf("abcd"))).isEqualTo(0)
    }

    @Test
    fun `a confident match comes first and is marked`() {
        val ranked = SuggestionRanker.rank("HAKKET OKSEKØD 8-12%", items)
        assertThat(ranked.first().item.name).isEqualTo("Hakket oksekød 8-12%")
        assertThat(ranked.first().confident).isTrue()
        assertThat(ranked.drop(1).none { it.confident }).isTrue()
    }

    @Test
    fun `a score of six isn't confident and seven is`() {
        val six = SuggestionRanker.rank("hakk tomat", listOf(item("Hakket tomater")))
        assertThat(six.single().confident).isFalse()

        val seven = SuggestionRanker.rank("sky", listOf(item("sky")))
        assertThat(seven.single().confident).isTrue()
    }

    @Test
    fun `no match gives no suggestions`() {
        assertThat(SuggestionRanker.rank("SOKKER", items)).isEmpty()
    }

    @Test
    fun `at most four are suggested`() {
        val many = (1..6).map { item("Mælk $it") }
        assertThat(SuggestionRanker.rank("MÆLK", many)).hasSize(4)
    }

    @ParameterizedTest
    @CsvSource("RØDVINSEDDIKE,rødvinseddike", "ÆBLE,æble", "BLÅBÆR,blåbær")
    fun `Danish letters stay inside one word`(raw: String, expected: String) {
        assertThat(SuggestionRanker.tokenize(raw)).containsExactly(expected)
    }

    @Test
    fun `tokenizing is the same on a Turkish device`() {
        val original = Locale.getDefault()
        try {
            Locale.setDefault(Locale.forLanguageTag("tr-TR"))
            assertThat(SuggestionRanker.tokenize("MILK")).containsExactly("milk")
        } finally {
            Locale.setDefault(original)
        }
    }

    @Test
    fun `search matches from two characters ignoring case, and shows up to eight`() {
        assertThat(SuggestionRanker.search("o", items)).isEmpty()
        assertThat(SuggestionRanker.search("OK", items).map { it.name }).containsExactly("Hakket oksekød 8-12%")
        assertThat(SuggestionRanker.search("mæ", items).map { it.name }).containsExactly("Skummetmælk 1 L")
        assertThat(SuggestionRanker.search("ost", (1..12).map { item("Ost $it") })).hasSize(8)
    }

    @ParameterizedTest
    @CsvSource("RØDVINSEDDIKE 500ML,Rødvinseddike 500ml", "'  ØL  PILSNER ',Øl Pilsner", "ÆG,Æg")
    fun `a printed name is tidied for a new item`(raw: String, expected: String) {
        assertThat(NameTidier.tidy(raw)).isEqualTo(expected)
    }
}
