/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.receipt

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import java.math.BigDecimal
import java.time.Instant

enum class ReceiptStatus { Queued, Extracting, Extracted, Completed, Failed, Unknown }

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
    val reconciled: Boolean,
    val status: ReceiptStatus,
    val lines: List<ReceiptLine>,
    val coverage: List<MealCoverage>,
    /** The server's stored note on why this receipt didn't reconcile. */
    val warning: String?,
    val memberId: String?,
    val memberName: String?,
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

    /** The endpoint replaces the receipt's whole coverage, so [days] is the full set. */
    suspend fun setCoverage(receiptId: String, days: Map<String, Int>): Result<List<MealCoverage>, ApiError>
}
