/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.receipt

import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.data.network.safeEmptyCall
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.core.domain.onSuccess
import dev.jelshoj.kvitteria.core.domain.receipt.LineCorrection
import dev.jelshoj.kvitteria.core.domain.receipt.MealCoverage
import dev.jelshoj.kvitteria.core.domain.receipt.NewLine
import dev.jelshoj.kvitteria.core.domain.receipt.Page
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptLine
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptRepository
import io.ktor.client.request.delete
import io.ktor.client.request.get
import io.ktor.client.request.parameter
import io.ktor.client.request.patch
import io.ktor.client.request.post
import io.ktor.client.request.put
import io.ktor.client.request.setBody
import io.ktor.http.ContentType
import io.ktor.http.contentType
import java.math.BigDecimal
import java.util.concurrent.ConcurrentHashMap
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.asSharedFlow

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

    override suspend fun correctTotals(
        receiptId: String,
        grandTotal: BigDecimal?,
        pantTotal: BigDecimal?,
    ): Result<Receipt, ApiError> =
        safeCall<ReceiptDto> {
            api.client.patch("/api/receipts/$receiptId") {
                contentType(ContentType.Application.Json)
                setBody(PatchReceiptDto(grandTotal, pantTotal))
            }
        }.map { it.toReceipt() }.onSuccess { cache[it.id] = it }

    override suspend fun addLine(receiptId: String, line: NewLine): Result<ReceiptLine, ApiError> =
        safeCall<ReceiptLineDto> {
            api.client.post("/api/receipts/$receiptId/lines") {
                contentType(ContentType.Application.Json)
                setBody(CreateReceiptLineDto(line.rawName, line.quantity, line.unitPrice, line.lineTotal))
            }
        }.map { it.toReceiptLine() }.onSuccess { added ->
            cache.computeIfPresent(receiptId) { _, receipt -> receipt.copy(lines = receipt.lines + added) }
        }

    override suspend fun deleteLine(lineId: String): EmptyResult<ApiError> {
        val result = safeEmptyCall { api.client.delete("/api/receipt-lines/$lineId") }
        if (result is Result.Success) {
            cache.replaceAll { _, receipt -> receipt.copy(lines = receipt.lines.filter { it.id != lineId }) }
        }
        return result
    }

    override suspend fun acceptDifference(
        receiptId: String,
        difference: BigDecimal,
        note: String?,
    ): Result<Receipt, ApiError> =
        safeCall<ReceiptDto> {
            api.client.post("/api/receipts/$receiptId/accept-difference") {
                contentType(ContentType.Application.Json)
                setBody(AcceptDifferenceDto(difference, note?.takeIf { it.isNotBlank() }))
            }
        }.map { it.toReceipt() }.onSuccess { cache[it.id] = it }

    override suspend fun withdrawAcceptance(receiptId: String): EmptyResult<ApiError> {
        val result = safeEmptyCall { api.client.delete("/api/receipts/$receiptId/accept-difference") }
        // The state is the server's to derive, so the copy kept here is dropped rather than patched.
        if (result is Result.Success) cache.remove(receiptId)
        return result
    }

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

    private val _deleted = MutableSharedFlow<String>(extraBufferCapacity = 8)
    override val deleted: Flow<String> = _deleted.asSharedFlow()

    override suspend fun delete(id: String): EmptyResult<ApiError> {
        val result = safeEmptyCall { api.client.delete("/api/receipts/$id") }
        val gone = result is Result.Success || (result as? Result.Error)?.error?.kind == DataError.Network.NOT_FOUND
        if (gone) {
            cache.remove(id)
            _deleted.tryEmit(id)
        }
        return result
    }

    override suspend fun image(id: String): Result<ByteArray, ApiError> =
        safeCall<ByteArray> { api.client.get("/api/receipts/$id/image") }

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
