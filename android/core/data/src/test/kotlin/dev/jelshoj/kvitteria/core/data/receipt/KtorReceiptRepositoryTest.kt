/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.receipt

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isInstanceOf
import assertk.assertions.isNotNull
import assertk.assertions.isNull
import assertk.assertions.isTrue
import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.HttpClientFactory
import dev.jelshoj.kvitteria.core.data.session.FakeServerSettings
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.receipt.ReconciliationState
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.MockRequestHandler
import io.ktor.client.engine.mock.respond
import io.ktor.client.request.HttpRequestData
import io.ktor.client.engine.mock.toByteArray
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpMethod
import io.ktor.http.HttpStatusCode
import io.ktor.http.fullPath
import io.ktor.http.headersOf
import java.math.BigDecimal
import kotlinx.coroutines.test.runTest
import org.junit.jupiter.api.Test

class KtorReceiptRepositoryTest {
    private val json = headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString())
    private val problem = headersOf(HttpHeaders.ContentType, "application/problem+json")
    private val requests = mutableListOf<HttpRequestData>()

    private val receiptJson = """
        {"id":"r1","merchant":"Netto","purchaseDate":"2026-10-26T12:07:00Z","grandTotal":72.35,"pantTotal":3,
         "reconciliation":"Reconciled","status":3,"receiptLines":[],"receiptMealCoverage":[],"error":null,
         "memberId":"m","memberDisplayName":"Sofie","hasImage":true,"possibleDuplicateOfId":"r0"}
    """.trimIndent()

    private fun repository(handler: MockRequestHandler): KtorReceiptRepository {
        val engine = MockEngine { request ->
            requests += request
            handler(request)
        }
        val api = ApiHttpClient(HttpClientFactory.anonymous(engine, FakeServerSettings()).client)
        return KtorReceiptRepository(api)
    }

    @Test
    fun `reads the image and duplicate fields`() = runTest {
        val result = repository { respond(receiptJson, HttpStatusCode.OK, json) }.receipt("r1")

        val receipt = (result as Result.Success).data
        assertThat(receipt.hasImage).isTrue()
        assertThat(receipt.possibleDuplicateOfId).isEqualTo("r0")
    }

    @Test
    fun `a receipt from an older server has no image`() = runTest {
        val older = receiptJson.substringBefore(""","hasImage"""") + "}"
        val result = repository { respond(older, HttpStatusCode.OK, json) }.receipt("r1")

        val receipt = (result as Result.Success).data
        assertThat(receipt.hasImage).isEqualTo(false)
        assertThat(receipt.possibleDuplicateOfId).isNull()
    }

    @Test
    fun `a state name this app does not know reads as unknown`() = runTest {
        val newer = receiptJson.replace("\"Reconciled\"", "\"Disputed\"")
        val result = repository { respond(newer, HttpStatusCode.OK, json) }.receipt("r1")

        assertThat((result as Result.Success).data.reconciliation).isEqualTo(ReconciliationState.Unknown)
    }

    @Test
    fun `an accepted receipt carries who accepted what`() = runTest {
        val accepted = receiptJson.replace("\"Reconciled\"", "\"Accepted\"").replace(
            "\"status\":3",
            "\"acceptance\":{\"difference\":0.50,\"memberId\":\"m\",\"memberDisplayName\":\"Sofie\"," +
                "\"acceptedAt\":\"2026-11-09T10:00:00Z\",\"note\":\"Rounding\"},\"status\":3",
        )
        val receipt = (repository { respond(accepted, HttpStatusCode.OK, json) }.receipt("r1") as Result.Success).data

        assertThat(receipt.reconciliation).isEqualTo(ReconciliationState.Accepted)
        assertThat(receipt.acceptance?.difference).isEqualTo(BigDecimal("0.50"))
        assertThat(receipt.acceptance?.memberName).isEqualTo("Sofie")
        assertThat(receipt.acceptance?.note).isEqualTo("Rounding")
    }

    @Test
    fun `accepting states the difference and keeps the answer`() = runTest {
        val repository = repository { respond(receiptJson, HttpStatusCode.OK, json) }

        val result = repository.acceptDifference("r1", BigDecimal("0.50"), "  ")

        assertThat(result).isInstanceOf<Result.Success<*>>()
        assertThat(requests.single().method).isEqualTo(HttpMethod.Post)
        assertThat(requests.single().url.fullPath).isEqualTo("/api/receipts/r1/accept-difference")
        assertThat(String(requests.single().body.toByteArray())).isEqualTo("""{"difference":0.50,"note":null}""")
        assertThat(repository.peek("r1")).isNotNull()
    }

    @Test
    fun `a stale difference is a 409 carrying the server's message`() = runTest {
        val repository = repository {
            respond("""{"status":409,"detail":"The difference is now 2,00 kr, not 0,50 kr."}""", HttpStatusCode.Conflict, problem)
        }

        val result = repository.acceptDifference("r1", BigDecimal("0.50"), null)

        assertThat(result).isEqualTo(
            Result.Error(ApiError(DataError.Network.CONFLICT, "The difference is now 2,00 kr, not 0,50 kr.")),
        )
    }

    @Test
    fun `withdrawing deletes the acceptance and forgets the copy`() = runTest {
        val repository = repository { request ->
            if (request.method == HttpMethod.Delete) respond("", HttpStatusCode.NoContent) else respond(receiptJson, HttpStatusCode.OK, json)
        }
        repository.receipt("r1")

        val result = repository.withdrawAcceptance("r1")

        assertThat(result).isInstanceOf<Result.Success<Unit>>()
        assertThat(requests.last().url.fullPath).isEqualTo("/api/receipts/r1/accept-difference")
        assertThat(repository.peek("r1")).isNull()
    }

    @Test
    fun `deleting the only line is a 409 and leaves the cache alone`() = runTest {
        val repository = repository { request ->
            if (request.method == HttpMethod.Delete) {
                respond("""{"status":409,"detail":"This is the receipt's only line."}""", HttpStatusCode.Conflict, problem)
            } else {
                respond(receiptJson, HttpStatusCode.OK, json)
            }
        }
        repository.receipt("r1")

        val result = repository.deleteLine("l1")

        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.CONFLICT, "This is the receipt's only line.")))
        assertThat(repository.peek("r1")).isNotNull()
    }

    @Test
    fun `correcting totals sends only the figures and keeps the receipt`() = runTest {
        val repository = repository { respond(receiptJson, HttpStatusCode.OK, json) }

        repository.correctTotals("r1", null, BigDecimal("5.00"))

        assertThat(requests.single().method).isEqualTo(HttpMethod.Patch)
        assertThat(String(requests.single().body.toByteArray())).isEqualTo("""{"grandTotal":null,"pantTotal":5.00}""")
        assertThat(repository.peek("r1")).isNotNull()
    }

    @Test
    fun `a 204 deletes and forgets the receipt`() = runTest {
        val repository = repository { request ->
            if (request.method == HttpMethod.Delete) {
                respond("", HttpStatusCode.NoContent)
            } else {
                respond(receiptJson, HttpStatusCode.OK, json)
            }
        }
        repository.receipt("r1")
        assertThat(repository.peek("r1")).isNotNull()

        val result = repository.delete("r1")

        assertThat(result).isInstanceOf<Result.Success<Unit>>()
        assertThat(requests.last().method).isEqualTo(HttpMethod.Delete)
        assertThat(requests.last().url.fullPath).isEqualTo("/api/receipts/r1")
        assertThat(repository.peek("r1")).isNull()
    }

    @Test
    fun `a 404 on delete carries the server's message and forgets the receipt`() = runTest {
        val repository = repository { request ->
            if (request.method == HttpMethod.Delete) {
                respond("""{"status":404,"detail":"Receipt with id r1 not found"}""", HttpStatusCode.NotFound, problem)
            } else {
                respond(receiptJson, HttpStatusCode.OK, json)
            }
        }
        repository.receipt("r1")

        val result = repository.delete("r1")

        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.NOT_FOUND, "Receipt with id r1 not found")))
        assertThat(repository.peek("r1")).isNull()
    }

    @Test
    fun `the image is returned as the bytes the server sent`() = runTest {
        val jpeg = byteArrayOf(0xFF.toByte(), 0xD8.toByte(), 1, 2, 3, 0xFF.toByte(), 0xD9.toByte())
        val result = repository {
            respond(jpeg, HttpStatusCode.OK, headersOf(HttpHeaders.ContentType, "image/jpeg"))
        }.image("r1")

        assertThat((result as Result.Success).data.toList()).isEqualTo(jpeg.toList())
        assertThat(requests.single().url.fullPath).isEqualTo("/api/receipts/r1/image")
    }

    @Test
    fun `a receipt without an image is a 404`() = runTest {
        val result = repository {
            respond("""{"status":404,"detail":"Receipt with id r1 has no image"}""", HttpStatusCode.NotFound, problem)
        }.image("r1")

        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.NOT_FOUND, "Receipt with id r1 has no image")))
    }
}
