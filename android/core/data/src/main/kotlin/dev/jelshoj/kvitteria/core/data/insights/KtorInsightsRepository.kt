/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.insights

import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.data.receipt.PagedResultDto
import dev.jelshoj.kvitteria.core.data.receipt.toPage
import dev.jelshoj.kvitteria.core.data.serialization.MoneySerializer
import dev.jelshoj.kvitteria.core.data.serialization.YearMonthSerializer
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.insights.CategoryAmount
import dev.jelshoj.kvitteria.core.domain.insights.CategoryBreakdown
import dev.jelshoj.kvitteria.core.domain.insights.InsightsRepository
import dev.jelshoj.kvitteria.core.domain.insights.MealCoverageMonth
import dev.jelshoj.kvitteria.core.domain.insights.MealCoverageStats
import dev.jelshoj.kvitteria.core.domain.insights.PriceHistory
import dev.jelshoj.kvitteria.core.domain.insights.PriceMonth
import dev.jelshoj.kvitteria.core.domain.insights.PricedItem
import dev.jelshoj.kvitteria.core.domain.insights.PricedName
import dev.jelshoj.kvitteria.core.domain.insights.SpendingSummary
import dev.jelshoj.kvitteria.core.domain.insights.TrendMonth
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.core.domain.receipt.Page
import io.ktor.client.request.get
import io.ktor.client.request.parameter
import java.math.BigDecimal
import java.time.YearMonth
import kotlinx.serialization.Serializable

private typealias Money = @Serializable(with = MoneySerializer::class) BigDecimal
private typealias WireMonth = @Serializable(with = YearMonthSerializer::class) YearMonth

@Serializable
data class SpendingSummaryDto(
    val month: WireMonth,
    val total: Money,
    val receiptCount: Int,
    val pantTotal: Money,
    val linesTotal: Money,
    val uncategorizedTotal: Money,
    val previousTotal: Money,
    val changePercent: Money?,
)

@Serializable
data class CategoryAmountDto(
    val categoryId: String?,
    val displayName: String?,
    val amount: Money,
    val compareAmount: Money?,
    val changePercent: Money?,
)

@Serializable
data class CategoryBreakdownDto(
    val month: WireMonth,
    val compareToMonth: WireMonth?,
    val parentCategoryId: String?,
    val rows: List<CategoryAmountDto>,
    val uncategorized: CategoryAmountDto?,
)

@Serializable
data class SpendingTrendMonthDto(
    val month: WireMonth,
    val total: Money,
    val receiptCount: Int,
    val changePercent: Money?,
)

@Serializable
data class PricedNameDto(val key: String, val label: String, val purchaseCount: Int)

@Serializable
data class PricedItemDto(
    val id: String,
    val displayName: String,
    val purchaseCount: Int,
    val names: List<PricedNameDto>,
)

@Serializable
data class PriceMonthDto(val month: WireMonth, val price: Money?, val purchaseCount: Int)

@Serializable
data class PriceHistoryDto(
    val itemId: String,
    val displayName: String,
    val months: List<PriceMonthDto>,
    val latestPrice: Money?,
    val lowestPrice: Money?,
    val changePercent: Money?,
    val rawNameKey: String?,
    val rawNameLabel: String?,
)

@Serializable
data class MealCoverageMonthDto(
    val month: WireMonth,
    val spend: Money,
    val daysCovered: Int,
    val costPerDay: Money?,
)

@Serializable
data class MealCoverageStatsDto(
    val mealId: String,
    val displayName: String,
    val months: List<MealCoverageMonthDto>,
)

fun SpendingSummaryDto.toSpendingSummary(): SpendingSummary = SpendingSummary(
    month, total, receiptCount, pantTotal, linesTotal, uncategorizedTotal, previousTotal, changePercent,
)

fun CategoryAmountDto.toCategoryAmount(): CategoryAmount =
    CategoryAmount(categoryId, displayName, amount, compareAmount, changePercent)

fun CategoryBreakdownDto.toCategoryBreakdown(): CategoryBreakdown = CategoryBreakdown(
    month = month,
    compareTo = compareToMonth,
    parentId = parentCategoryId,
    rows = rows.map { it.toCategoryAmount() },
    uncategorized = uncategorized?.toCategoryAmount(),
)

fun SpendingTrendMonthDto.toTrendMonth(): TrendMonth = TrendMonth(month, total, receiptCount, changePercent)

fun PricedItemDto.toPricedItem(): PricedItem = PricedItem(
    id = id,
    name = displayName,
    purchaseCount = purchaseCount,
    names = names.map { PricedName(it.key, it.label, it.purchaseCount) },
)

fun PriceHistoryDto.toPriceHistory(): PriceHistory = PriceHistory(
    itemId = itemId,
    name = displayName,
    months = months.map { PriceMonth(it.month, it.price, it.purchaseCount) },
    latestPrice = latestPrice,
    lowestPrice = lowestPrice,
    changePercent = changePercent,
    nameKey = rawNameKey,
    nameLabel = rawNameLabel,
)

fun MealCoverageStatsDto.toMealCoverageStats(): MealCoverageStats = MealCoverageStats(
    mealId = mealId,
    name = displayName,
    months = months.map { MealCoverageMonth(it.month, it.spend, it.daysCovered, it.costPerDay) },
)

class KtorInsightsRepository(private val api: ApiHttpClient) : InsightsRepository {
    override suspend fun summary(month: YearMonth): Result<SpendingSummary, ApiError> =
        safeCall<SpendingSummaryDto> {
            api.client.get("/api/insights/summary") { parameter("month", month.toString()) }
        }.map { it.toSpendingSummary() }

    override suspend fun categories(
        month: YearMonth,
        compareTo: YearMonth?,
        parentId: String?,
    ): Result<CategoryBreakdown, ApiError> =
        safeCall<CategoryBreakdownDto> {
            api.client.get("/api/insights/categories") {
                parameter("month", month.toString())
                parameter("compareTo", compareTo?.toString())
                parameter("parentId", parentId)
            }
        }.map { it.toCategoryBreakdown() }

    override suspend fun trend(endMonth: YearMonth, months: Int): Result<List<TrendMonth>, ApiError> =
        safeCall<List<SpendingTrendMonthDto>> {
            api.client.get("/api/insights/trend") {
                parameter("endMonth", endMonth.toString())
                parameter("months", months)
            }
        }.map { list -> list.map { it.toTrendMonth() } }

    override suspend fun pricedItems(page: Int, pageSize: Int): Result<Page<PricedItem>, ApiError> =
        safeCall<PagedResultDto<PricedItemDto>> {
            api.client.get("/api/insights/prices") {
                parameter("page", page)
                parameter("pageSize", pageSize)
            }
        }.map { result -> result.toPage { it.toPricedItem() } }

    override suspend fun priceHistory(itemId: String, months: Int, name: String?): Result<PriceHistory, ApiError> =
        safeCall<PriceHistoryDto> {
            api.client.get("/api/insights/prices/$itemId") {
                parameter("months", months)
                // Ktor leaves a null parameter out of the query, so an unnarrowed history sends no `name`.
                parameter("name", name)
            }
        }.map { it.toPriceHistory() }

    override suspend fun mealCoverage(endMonth: YearMonth, months: Int): Result<List<MealCoverageStats>, ApiError> =
        safeCall<List<MealCoverageStatsDto>> {
            api.client.get("/api/insights/meal-coverage") {
                parameter("endMonth", endMonth.toString())
                parameter("months", months)
            }
        }.map { list -> list.map { it.toMealCoverageStats() } }
}
