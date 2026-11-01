/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.sort.data

import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.data.receipt.PagedResultDto
import dev.jelshoj.kvitteria.core.data.receipt.ReceiptLineDto
import dev.jelshoj.kvitteria.core.data.serialization.InstantSerializer
import dev.jelshoj.kvitteria.core.data.serialization.MoneySerializer
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.feature.sort.domain.SkippedLines
import dev.jelshoj.kvitteria.feature.sort.domain.SortRepository
import dev.jelshoj.kvitteria.feature.sort.domain.SortRequest
import dev.jelshoj.kvitteria.feature.sort.domain.SortResult
import dev.jelshoj.kvitteria.feature.sort.domain.UnassignedLine
import io.ktor.client.request.get
import io.ktor.client.request.parameter
import io.ktor.client.request.post
import io.ktor.client.request.setBody
import io.ktor.http.ContentType
import io.ktor.http.contentType
import java.math.BigDecimal
import java.time.Instant
import java.util.concurrent.ConcurrentHashMap
import kotlinx.serialization.Serializable
import org.koin.core.module.dsl.bind
import org.koin.core.module.dsl.singleOf
import org.koin.dsl.module

@Serializable
data class UnassignedLineDto(
    val id: String,
    val rawName: String,
    @Serializable(with = MoneySerializer::class) val quantity: BigDecimal,
    @Serializable(with = MoneySerializer::class) val unitPrice: BigDecimal,
    @Serializable(with = MoneySerializer::class) val lineTotal: BigDecimal,
    val receiptId: String,
    val merchantName: String,
    @Serializable(with = InstantSerializer::class) val purchaseDate: Instant,
    val memberId: String?,
    val memberDisplayName: String?,
)

@Serializable
data class SortReceiptLineDto(
    val canonicalItemId: String?,
    val newItemName: String?,
    val categoryId: String?,
    val mealId: String?,
    val rememberAlias: Boolean,
    val setAsDefaults: Boolean,
)

@Serializable
data class SortReceiptLineResultDto(
    val line: ReceiptLineDto,
    val otherLinesAssigned: Int,
    val remainingCount: Int,
)

fun UnassignedLineDto.toUnassignedLine(): UnassignedLine = UnassignedLine(
    id = id,
    rawName = rawName,
    quantity = quantity,
    unitPrice = unitPrice,
    lineTotal = lineTotal,
    receiptId = receiptId,
    merchant = merchantName,
    purchaseDate = purchaseDate,
    memberName = memberDisplayName,
)

class KtorSortRepository(private val api: ApiHttpClient) : SortRepository {
    override suspend fun queue(): Result<List<UnassignedLine>, ApiError> {
        val collected = mutableListOf<UnassignedLine>()
        var page = 1
        while (true) {
            val result = safeCall<PagedResultDto<UnassignedLineDto>> {
                api.client.get("/api/receipt-lines/unassigned") {
                    parameter("page", page)
                    parameter("pageSize", PAGE_SIZE)
                }
            }
            when (result) {
                is Result.Error -> return result
                is Result.Success -> {
                    collected += result.data.items.map { it.toUnassignedLine() }
                    if (result.data.items.isEmpty() || collected.size >= result.data.totalCount) {
                        return Result.Success(collected)
                    }
                }
            }
            page++
        }
    }

    override suspend fun sort(lineId: String, request: SortRequest): Result<SortResult, ApiError> =
        safeCall<SortReceiptLineResultDto> {
            api.client.post("/api/receipt-lines/$lineId/sort") {
                contentType(ContentType.Application.Json)
                setBody(
                    SortReceiptLineDto(
                        canonicalItemId = request.canonicalItemId,
                        newItemName = request.newItemName,
                        categoryId = request.categoryId,
                        mealId = request.mealId,
                        rememberAlias = request.rememberAlias,
                        setAsDefaults = request.setAsDefaults,
                    ),
                )
            }
        }.map { SortResult(it.otherLinesAssigned, it.remainingCount) }

    private companion object {
        const val PAGE_SIZE = 100
    }
}

/** Held in memory on purpose, so a skipped line returns after the app restarts. */
class InMemorySkippedLines : SkippedLines {
    private val skipped = ConcurrentHashMap.newKeySet<String>()

    override val ids: Set<String> get() = skipped.toSet()

    override fun skip(id: String) {
        skipped += id
    }

    override fun clear() = skipped.clear()
}

val sortDataModule = module {
    singleOf(::KtorSortRepository) { bind<SortRepository>() }
    singleOf(::InMemorySkippedLines) { bind<SkippedLines>() }
}
