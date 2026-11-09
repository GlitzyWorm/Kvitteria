/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.receipt

import dev.jelshoj.kvitteria.core.data.serialization.InstantSerializer
import dev.jelshoj.kvitteria.core.data.serialization.MoneySerializer
import dev.jelshoj.kvitteria.core.data.serialization.ReceiptStatusSerializer
import dev.jelshoj.kvitteria.core.data.serialization.ReconciliationStateSerializer
import dev.jelshoj.kvitteria.core.domain.receipt.Acceptance
import dev.jelshoj.kvitteria.core.domain.receipt.MealCoverage
import dev.jelshoj.kvitteria.core.domain.receipt.Page
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptLine
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptStatus
import dev.jelshoj.kvitteria.core.domain.receipt.ReconciliationState
import java.math.BigDecimal
import java.time.Instant
import kotlinx.serialization.Serializable

@Serializable
data class PagedResultDto<T>(
    val items: List<T>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
)

fun <T, R> PagedResultDto<T>.toPage(map: (T) -> R): Page<R> = Page(items.map(map), page, pageSize, totalCount)

@Serializable
data class ReceiptLineDto(
    val id: String,
    val rawName: String,
    @Serializable(with = MoneySerializer::class) val quantity: BigDecimal,
    @Serializable(with = MoneySerializer::class) val unitPrice: BigDecimal,
    @Serializable(with = MoneySerializer::class) val lineTotal: BigDecimal,
    val canonicalItemId: String?,
    val canonicalItemDisplayName: String?,
    val mealId: String?,
    val mealDisplayName: String?,
    val categoryId: String?,
    val categoryDisplayName: String?,
)

@Serializable
data class ReceiptMealCoverageDto(
    val mealId: String,
    val mealDisplayName: String,
    val daysCovered: Int,
)

@Serializable
data class ReceiptAcceptanceDto(
    @Serializable(with = MoneySerializer::class) val difference: BigDecimal,
    val memberId: String?,
    val memberDisplayName: String?,
    @Serializable(with = InstantSerializer::class) val acceptedAt: Instant,
    val note: String?,
)

fun ReceiptAcceptanceDto.toAcceptance(): Acceptance =
    Acceptance(difference, memberId, memberDisplayName, acceptedAt, note)

@Serializable
data class ReceiptDto(
    val id: String,
    val merchant: String,
    @Serializable(with = InstantSerializer::class) val purchaseDate: Instant,
    @Serializable(with = MoneySerializer::class) val grandTotal: BigDecimal,
    @Serializable(with = MoneySerializer::class) val pantTotal: BigDecimal,
    @Serializable(with = ReconciliationStateSerializer::class) val reconciliation: ReconciliationState,
    val acceptance: ReceiptAcceptanceDto? = null,
    @Serializable(with = ReceiptStatusSerializer::class) val status: ReceiptStatus,
    val receiptLines: List<ReceiptLineDto>,
    val receiptMealCoverage: List<ReceiptMealCoverageDto>,
    val error: String?,
    val memberId: String?,
    val memberDisplayName: String?,
    // Defaulted, so a server from before image intake still reads.
    val hasImage: Boolean = false,
    val possibleDuplicateOfId: String? = null,
)

@Serializable
data class PutReceiptLineCategoryDto(val categoryId: String?, val setAsDefault: Boolean)

@Serializable
data class PutReceiptLineMealDto(val mealId: String?, val setAsDefault: Boolean)

@Serializable
data class PatchReceiptLineDto(
    val rawName: String?,
    @Serializable(with = MoneySerializer::class) val quantity: BigDecimal?,
    @Serializable(with = MoneySerializer::class) val unitPrice: BigDecimal?,
    @Serializable(with = MoneySerializer::class) val lineTotal: BigDecimal?,
)

@Serializable
data class PatchReceiptDto(
    @Serializable(with = MoneySerializer::class) val grandTotal: BigDecimal?,
    @Serializable(with = MoneySerializer::class) val pantTotal: BigDecimal?,
)

@Serializable
data class CreateReceiptLineDto(
    val rawName: String,
    @Serializable(with = MoneySerializer::class) val quantity: BigDecimal,
    @Serializable(with = MoneySerializer::class) val unitPrice: BigDecimal,
    @Serializable(with = MoneySerializer::class) val lineTotal: BigDecimal,
)

@Serializable
data class AcceptDifferenceDto(
    @Serializable(with = MoneySerializer::class) val difference: BigDecimal,
    val note: String?,
)

@Serializable
data class MealCoverageEntryDto(val mealId: String, val daysCovered: Int)

@Serializable
data class PutReceiptMealCoverageDto(val coverage: List<MealCoverageEntryDto>)

fun ReceiptLineDto.toReceiptLine(): ReceiptLine = ReceiptLine(
    id = id,
    rawName = rawName,
    quantity = quantity,
    unitPrice = unitPrice,
    lineTotal = lineTotal,
    canonicalItemId = canonicalItemId,
    canonicalItemName = canonicalItemDisplayName,
    mealId = mealId,
    mealName = mealDisplayName,
    categoryId = categoryId,
    categoryName = categoryDisplayName,
)

fun ReceiptMealCoverageDto.toMealCoverage(): MealCoverage = MealCoverage(mealId, mealDisplayName, daysCovered)

fun ReceiptDto.toReceipt(): Receipt = Receipt(
    id = id,
    merchant = merchant,
    purchaseDate = purchaseDate,
    grandTotal = grandTotal,
    pantTotal = pantTotal,
    reconciliation = reconciliation,
    acceptance = acceptance?.toAcceptance(),
    status = status,
    lines = receiptLines.map { it.toReceiptLine() },
    coverage = receiptMealCoverage.map { it.toMealCoverage() },
    warning = error,
    memberId = memberId,
    memberName = memberDisplayName,
    hasImage = hasImage,
    possibleDuplicateOfId = possibleDuplicateOfId,
)
