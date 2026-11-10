/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.insights

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.receipt.Page
import java.math.BigDecimal
import java.time.YearMonth

data class SpendingSummary(
    val month: YearMonth,
    val total: BigDecimal,
    val receiptCount: Int,
    val pantTotal: BigDecimal,
    val linesTotal: BigDecimal,
    val uncategorizedTotal: BigDecimal,
    val previousTotal: BigDecimal,
    val changePercent: BigDecimal?,
)

data class CategoryAmount(
    val categoryId: String?,
    val name: String?,
    val amount: BigDecimal,
    val compareAmount: BigDecimal?,
    val changePercent: BigDecimal?,
)

data class CategoryBreakdown(
    val month: YearMonth,
    val compareTo: YearMonth?,
    val parentId: String?,
    val rows: List<CategoryAmount>,
    val uncategorized: CategoryAmount?,
)

data class TrendMonth(
    val month: YearMonth,
    val total: BigDecimal,
    val receiptCount: Int,
    val changePercent: BigDecimal?,
)

data class PricedName(
    /** The normalized form, sent back to the server as `name`. */
    val key: String,
    val label: String,
    val purchaseCount: Int,
)

data class PricedItem(
    val id: String,
    val name: String,
    val purchaseCount: Int,
    val names: List<PricedName>,
)

data class PriceMonth(
    val month: YearMonth,
    val price: BigDecimal?,
    val purchaseCount: Int,
)

data class PriceHistory(
    val itemId: String,
    val name: String,
    val months: List<PriceMonth>,
    val latestPrice: BigDecimal?,
    val lowestPrice: BigDecimal?,
    val changePercent: BigDecimal?,
    /** Set only when the history was narrowed to one raw name. */
    val nameKey: String?,
    val nameLabel: String?,
)

data class MealCoverageMonth(
    val month: YearMonth,
    val spend: BigDecimal,
    val daysCovered: Int,
    val costPerDay: BigDecimal?,
)

data class MealCoverageStats(
    val mealId: String,
    val name: String,
    val months: List<MealCoverageMonth>,
)

interface InsightsRepository {
    suspend fun summary(month: YearMonth): Result<SpendingSummary, ApiError>

    suspend fun categories(
        month: YearMonth,
        compareTo: YearMonth?,
        parentId: String?,
    ): Result<CategoryBreakdown, ApiError>

    suspend fun trend(endMonth: YearMonth, months: Int): Result<List<TrendMonth>, ApiError>

    suspend fun pricedItems(page: Int, pageSize: Int): Result<Page<PricedItem>, ApiError>

    suspend fun priceHistory(itemId: String, months: Int, name: String? = null): Result<PriceHistory, ApiError>

    suspend fun mealCoverage(endMonth: YearMonth, months: Int): Result<List<MealCoverageStats>, ApiError>
}
