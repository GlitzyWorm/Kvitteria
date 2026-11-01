/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.receipt

import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.core.domain.onSuccess
import dev.jelshoj.kvitteria.core.domain.receipt.LineCorrection
import dev.jelshoj.kvitteria.core.domain.receipt.MealCoverage
import dev.jelshoj.kvitteria.core.domain.receipt.Page
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptLine
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptRepository
import io.ktor.client.request.get
import io.ktor.client.request.parameter
import io.ktor.client.request.patch
import io.ktor.client.request.put
import io.ktor.client.request.setBody
import io.ktor.http.ContentType
import io.ktor.http.contentType
import java.util.concurrent.ConcurrentHashMap

class KtorReceiptRepository(private val api: ApiHttpClient) : ReceiptRepository {
    /** Listed receipts, so that detail can paint at once. */
    private val cache = ConcurrentHashMap<String, Receipt>()

    override suspend fun receipts(
        page: Int,
        pageSize: Int,
        search: String?,
        unreconciledOnly: Boolean,
    ): Result<Page<Receipt>, ApiError> =
        safeCall<PagedResultDto<ReceiptDto>> {
            api.client.get("/api/receipts") {
                parameter("page", page)
                parameter("pageSize", pageSize)
                parameter("search", search?.takeIf { it.isNotBlank() })
                parameter("reconciled", false.takeIf { unreconciledOnly })
            }
        }.map { result -> result.toPage { it.toReceipt() } }
            .onSuccess { result -> result.items.forEach { cache[it.id] = it } }

    override suspend fun receipt(id: String): Result<Receipt, ApiError> =
        safeCall<ReceiptDto> { api.client.get("/api/receipts/$id") }
            .map { it.toReceipt() }
            .onSuccess { cache[it.id] = it }

    override fun peek(id: String): Receipt? = cache[id]

    override suspend fun setLineCategory(lineId: String, categoryId: String?): Result<ReceiptLine, ApiError> =
        putLine("/api/receipt-lines/$lineId/category", PutReceiptLineCategoryDto(categoryId, setAsDefault = false))

    override suspend fun setLineMeal(lineId: String, mealId: String?): Result<ReceiptLine, ApiError> =
        putLine("/api/receipt-lines/$lineId/meal", PutReceiptLineMealDto(mealId, setAsDefault = false))

    override suspend fun correctLine(lineId: String, correction: LineCorrection): Result<ReceiptLine, ApiError> =
        safeCall<ReceiptLineDto> {
            api.client.patch("/api/receipt-lines/$lineId") {
                contentType(ContentType.Application.Json)
                setBody(
                    PatchReceiptLineDto(
                        correction.rawName,
                        correction.quantity,
                        correction.unitPrice,
                        correction.lineTotal,
                    ),
                )
            }
        }.map { it.toReceiptLine() }.onSuccess(::remember)

    override suspend fun setCoverage(
        receiptId: String,
        days: Map<String, Int>,
    ): Result<List<MealCoverage>, ApiError> =
        safeCall<List<ReceiptMealCoverageDto>> {
            api.client.put("/api/receipts/$receiptId/meal-coverage") {
                contentType(ContentType.Application.Json)
                setBody(PutReceiptMealCoverageDto(days.map { (mealId, count) -> MealCoverageEntryDto(mealId, count) }))
            }
        }.map { coverage -> coverage.map { it.toMealCoverage() } }
            .onSuccess { coverage -> cache.computeIfPresent(receiptId) { _, receipt -> receipt.copy(coverage = coverage) } }

    private suspend inline fun <reified B> putLine(route: String, body: B): Result<ReceiptLine, ApiError> =
        safeCall<ReceiptLineDto> {
            api.client.put(route) {
                contentType(ContentType.Application.Json)
                setBody(body)
            }
        }.map { it.toReceiptLine() }.onSuccess(::remember)

    private fun remember(line: ReceiptLine) {
        cache.replaceAll { _, receipt ->
            if (receipt.lines.none { it.id == line.id }) {
                receipt
            } else {
                receipt.copy(lines = receipt.lines.map { if (it.id == line.id) line else it })
            }
        }
    }
}
