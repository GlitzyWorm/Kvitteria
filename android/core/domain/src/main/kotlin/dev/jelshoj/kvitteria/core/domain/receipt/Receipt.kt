/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.receipt

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import java.math.BigDecimal
import java.time.Instant
import kotlinx.coroutines.flow.Flow

enum class ReceiptStatus { Queued, Extracting, Extracted, Completed, Failed, Unknown }

/**
 * Derived by the server from the stored figures on every read. [Accepted] is an unreconciled receipt whose
 * difference a member has accepted; only [Unreconciled] needs attention.
 */
enum class ReconciliationState { Reconciled, Accepted, Unreconciled, Unknown }

/** A member's acceptance of the receipt's current difference. */
data class Acceptance(
    val difference: BigDecimal,
    val memberId: String?,
    val memberName: String?,
    val acceptedAt: Instant,
    val note: String?,
)

data class ReceiptLine(
    val id: String,
    val rawName: String,
    val quantity: BigDecimal,
    val unitPrice: BigDecimal,
    val lineTotal: BigDecimal,
    val canonicalItemId: String?,
    val canonicalItemName: String?,
    val mealId: String?,
    val mealName: String?,
    val categoryId: String?,
    val categoryName: String?,
)

data class MealCoverage(
    val mealId: String,
    val mealName: String,
    val daysCovered: Int,
)

data class Receipt(
    val id: String,
    val merchant: String,
    val purchaseDate: Instant,
    val grandTotal: BigDecimal,
    val pantTotal: BigDecimal,
    val reconciliation: ReconciliationState,
    /** Set only while [reconciliation] is [ReconciliationState.Accepted]. */
    val acceptance: Acceptance?,
    val status: ReceiptStatus,
    val lines: List<ReceiptLine>,
    val coverage: List<MealCoverage>,
    /** What extraction reported about this receipt. History, not the current state: see [reconciliation]. */
    val warning: String?,
    val memberId: String?,
    val memberName: String?,
    /** Whether it was read from an uploaded image, which [ReceiptRepository.image] then fetches. */
    val hasImage: Boolean = false,
    /** The stored receipt with the same merchant, day and total, when there was one. */
    val possibleDuplicateOfId: String? = null,
) {
    /** What reconciliation compares with zero: the total less the lines and pant. */
    val difference: BigDecimal
        get() = grandTotal - lines.fold(BigDecimal.ZERO) { sum, line -> sum + line.lineTotal } - pantTotal
}

data class Page<T>(
    val items: List<T>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
) {
    val hasMore: Boolean get() = page * pageSize < totalCount
}

data class LineCorrection(
    val rawName: String,
    val quantity: BigDecimal,
    val unitPrice: BigDecimal,
    val lineTotal: BigDecimal,
)

data class NewLine(
    val rawName: String,
    val quantity: BigDecimal,
    val unitPrice: BigDecimal,
    val lineTotal: BigDecimal,
)

interface ReceiptRepository {
    suspend fun receipts(
        page: Int,
        pageSize: Int,
        search: String? = null,
        unreconciledOnly: Boolean = false,
    ): Result<Page<Receipt>, ApiError>

    suspend fun receipt(id: String): Result<Receipt, ApiError>

    /** The last copy seen, so detail can paint before its refresh answers. */
    fun peek(id: String): Receipt?

    suspend fun setLineCategory(lineId: String, categoryId: String?): Result<ReceiptLine, ApiError>

    suspend fun setLineMeal(lineId: String, mealId: String?): Result<ReceiptLine, ApiError>

    suspend fun correctLine(lineId: String, correction: LineCorrection): Result<ReceiptLine, ApiError>

    /** Corrects the grand total and/or pant. Answers the receipt as it now reads. */
    suspend fun correctTotals(
        receiptId: String,
        grandTotal: BigDecimal?,
        pantTotal: BigDecimal?,
    ): Result<Receipt, ApiError>

    suspend fun addLine(receiptId: String, line: NewLine): Result<ReceiptLine, ApiError>

    suspend fun deleteLine(lineId: String): EmptyResult<ApiError>

    /** Accepts [difference], the figure the member saw; the server refuses it when the figures moved since. */
    suspend fun acceptDifference(receiptId: String, difference: BigDecimal, note: String?): Result<Receipt, ApiError>

    suspend fun withdrawAcceptance(receiptId: String): EmptyResult<ApiError>

    /** The endpoint replaces the receipt's whole coverage, so [days] is the full set. */
    suspend fun setCoverage(receiptId: String, days: Map<String, Int>): Result<List<MealCoverage>, ApiError>

    /**
     * Deletes the receipt for good, and forgets it here so no list paints it again. A 404 forgets it too:
     * it is gone either way.
     */
    suspend fun delete(id: String): EmptyResult<ApiError>

    /** IDs of receipts deleted or found gone, so a list that still shows one can drop it. */
    val deleted: Flow<String>

    /** The JPEG the receipt was read from. */
    suspend fun image(id: String): Result<ByteArray, ApiError>
}
