/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.network

import assertk.assertFailure
import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isInstanceOf
import dev.jelshoj.kvitteria.core.data.serialization.KvitteriaJson
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError.Network
import dev.jelshoj.kvitteria.core.domain.Result
import io.ktor.client.HttpClient
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.respond
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.client.request.get
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.headersOf
import io.ktor.serialization.kotlinx.json.json
import java.io.IOException
import java.net.SocketTimeoutException
import kotlin.coroutines.cancellation.CancellationException
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.Serializable
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource

class SafeCallTest {
    @Serializable
    private data class Body(val value: Int)

    private fun client(status: HttpStatusCode, body: String, type: ContentType = ContentType.Application.Json) =
        HttpClient(MockEngine { respond(body, status, headersOf(HttpHeaders.ContentType, type.toString())) }) {
            install(ContentNegotiation) { json(KvitteriaJson) }
        }

    private fun failing(e: Throwable) = HttpClient(MockEngine { throw e })

    @ParameterizedTest
    @CsvSource(
        "400,BAD_REQUEST", "401,UNAUTHORIZED", "403,FORBIDDEN", "404,NOT_FOUND", "408,REQUEST_TIMEOUT",
        "409,CONFLICT", "413,PAYLOAD_TOO_LARGE", "429,TOO_MANY_REQUESTS", "500,SERVER_ERROR",
        "502,BAD_GATEWAY", "503,SERVICE_UNAVAILABLE", "504,SERVER_ERROR", "418,UNKNOWN",
    )
    fun `each status has its error`(status: Int, expected: Network) = runTest {
        val http = client(HttpStatusCode.fromValue(status), """{"detail":"the reason"}""")
        val result: Result<Body, ApiError> = safeCall { http.get("/x") }
        assertThat(result).isEqualTo(Result.Error(ApiError(expected, "the reason")))
    }

    @Test
    fun `a success is decoded`() = runTest {
        val http = client(HttpStatusCode.OK, """{"value":3,"addedLater":true}""")
        val result: Result<Body, ApiError> = safeCall { http.get("/x") }
        assertThat(result).isEqualTo(Result.Success(Body(3)))
    }

    @Test
    fun `a non-JSON error body has no detail and is still its status`() = runTest {
        val http = client(HttpStatusCode.BadGateway, "<html>502</html>", ContentType.Text.Html)
        val result: Result<Body, ApiError> = safeCall { http.get("/x") }
        assertThat(result).isEqualTo(Result.Error(ApiError(Network.BAD_GATEWAY)))
    }

    @Test
    fun `a success that can't be decoded is a serialization error`() = runTest {
        val http = client(HttpStatusCode.OK, """{"other":1}""")
        val result: Result<Body, ApiError> = safeCall { http.get("/x") }
        assertThat(result).isEqualTo(Result.Error(ApiError(Network.SERIALIZATION)))
    }

    @Test
    fun `no route to the server is no internet`() = runTest {
        val http = failing(IOException("unreachable"))
        val result: Result<Body, ApiError> = safeCall { http.get("/x") }
        assertThat(result).isEqualTo(Result.Error(ApiError(Network.NO_INTERNET)))
    }

    @Test
    fun `a socket timeout is a timeout`() = runTest {
        val http = failing(SocketTimeoutException())
        val result: Result<Body, ApiError> = safeCall { http.get("/x") }
        assertThat(result).isEqualTo(Result.Error(ApiError(Network.REQUEST_TIMEOUT)))
    }

    @Test
    fun `a failing empty call keeps its detail`() = runTest {
        val http = client(HttpStatusCode.Conflict, """{"detail":"already handled"}""")
        assertThat(safeEmptyCall { http.get("/x") })
            .isEqualTo(Result.Error(ApiError(Network.CONFLICT, "already handled")))
    }

    @Test
    fun `cancellation is not swallowed`() {
        assertFailure { exceptionToError(CancellationException()) }.isInstanceOf(CancellationException::class)
    }
}
