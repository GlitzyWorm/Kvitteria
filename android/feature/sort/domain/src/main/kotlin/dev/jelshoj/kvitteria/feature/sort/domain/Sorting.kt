/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.sort.domain

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.taxonomy.CanonicalItem
import java.math.BigDecimal
import java.time.Instant
import java.util.Locale

data class UnassignedLine(
    val id: String,
    val rawName: String,
    val quantity: BigDecimal,
    val unitPrice: BigDecimal,
    val lineTotal: BigDecimal,
    val receiptId: String,
    val merchant: String,
    val purchaseDate: Instant,
    val memberName: String?,
)

/** One atomic save: the item, its category and meal, and the two choices about what's learned. */
data class SortRequest(
    val canonicalItemId: String?,
    val newItemName: String?,
    val categoryId: String?,
    val mealId: String?,
    val rememberAlias: Boolean,
    val setAsDefaults: Boolean,
)

data class SortResult(
    val otherLinesAssigned: Int,
    val remainingCount: Int,
)

interface SortRepository {
    /** Every waiting line, paged through. The queue is small, because that's what sorting keeps it. */
    suspend fun queue(): Result<List<UnassignedLine>, ApiError>

    suspend fun sort(lineId: String, request: SortRequest): Result<SortResult, ApiError>
}

/** Lines moved past for the rest of the app session. Nothing about a skip reaches the server. */
interface SkippedLines {
    val ids: Set<String>

    fun skip(id: String)

    fun clear()
}

data class Suggestion(val item: CanonicalItem, val confident: Boolean)

/** Suggests items for a printed line, by comparing words locally. */
object SuggestionRanker {
    const val MAX_SUGGESTIONS = 4
    const val MAX_SEARCH_RESULTS = 8
    const val CONFIDENT_SCORE = 7
    private const val PREFIX = 4
    private const val EXACT_BONUS = 4
    private const val PREFIX_SCORE = 3
    private const val MIN_WORD = 3
    private const val MIN_SEARCH = 2

    private val separator = Regex("[^a-zæøå0-9]+")

    /** Lower-cased with the root locale, so `Æ`, `Ø` and `Å` stay part of a word on any device. */
    fun tokenize(text: String): List<String> =
        text.lowercase(Locale.ROOT).split(separator).filter { it.length >= MIN_WORD }

    fun score(rawTokens: List<String>, nameTokens: List<String>): Int =
        rawTokens.sumOf { raw ->
            nameTokens.sumOf { name ->
                when {
                    raw == name -> raw.length + EXACT_BONUS
                    raw.length >= PREFIX && name.length >= PREFIX && raw.take(PREFIX) == name.take(PREFIX) -> PREFIX_SCORE
                    else -> 0
                }
            }
        }

    fun rank(rawName: String, items: List<CanonicalItem>): List<Suggestion> {
        val rawTokens = tokenize(rawName)
        return items
            .map { it to score(rawTokens, tokenize(it.name)) }
            .filter { (_, score) -> score > 0 }
            .sortedByDescending { (_, score) -> score }
            .take(MAX_SUGGESTIONS)
            .mapIndexed { index, (item, score) -> Suggestion(item, index == 0 && score >= CONFIDENT_SCORE) }
    }

    fun search(term: String, items: List<CanonicalItem>): List<CanonicalItem> {
        val query = term.trim().lowercase(Locale.ROOT)
        if (query.length < MIN_SEARCH) return emptyList()
        return items.filter { query in it.name.lowercase(Locale.ROOT) }.take(MAX_SEARCH_RESULTS)
    }
}

object NameTidier {
    /** The printed name as a starting point for a new item: `RØDVINSEDDIKE 500ML` is `Rødvinseddike 500ml`. */
    fun tidy(rawName: String): String =
        rawName.trim().lowercase(Locale.ROOT).split(Regex("""\s+"""))
            .joinToString(" ") { word -> word.replaceFirstChar { it.uppercase(Locale.ROOT) } }
}
