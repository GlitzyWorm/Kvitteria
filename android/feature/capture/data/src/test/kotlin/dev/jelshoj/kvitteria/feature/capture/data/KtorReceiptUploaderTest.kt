/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.data

import assertk.assertThat
import assertk.assertions.contains
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.HttpClientFactory
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.feature.capture.domain.PreparedImage
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.respond
import io.ktor.client.engine.mock.toByteArray
import io.ktor.client.request.HttpRequestData
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpMethod
import io.ktor.http.HttpStatusCode
import io.ktor.http.content.OutgoingContent
import io.ktor.http.fullPath
import io.ktor.http.headersOf
import java.io.IOException
import java.time.Instant
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.test.runTest
import org.junit.jupiter.api.Test

class KtorReceiptUploaderTest {
    private class Server : ServerSettings {
        override val address: StateFlow<String?> = MutableStateFlow("http://server.test:8080")

        override suspend fun load(): String? = address.value

        override suspend fun save(address: String) = Unit
    }

    private val requests = mutableListOf<HttpRequestData>()
    private val bodies = mutableListOf<String>()
    private val image = PreparedImage(byteArrayOf(1, 2, 3), 3, 1, Instant.parse("2026-10-26T12:07:00Z"))
    private val json = headersOf(HttpHeaders.ContentType, "application/json")
    private val problem = headersOf(HttpHeaders.ContentType, "application/problem+json")

    private fun uploader(status: HttpStatusCode, body: String, headers: io.ktor.http.Headers = problem): KtorReceiptUploader {
        val engine = MockEngine { request ->
            requests += request
            bodies += request.body.toByteArray().decodeToString()
            respond(body, status, headers)
        }
        return KtorReceiptUploader(ApiHttpClient(HttpClientFactory.anonymous(engine, Server()).client))
    }

    private fun failing(e: Exception) = KtorReceiptUploader(
        ApiHttpClient(HttpClientFactory.anonymous(MockEngine { throw e }, Server()).client),
    )

    private fun detail(text: String) = """{"status":0,"detail":"$text"}"""

    @Test
    fun `a 202 is the intake id, posted as multipart with the capture time`() = runTest {
        val result = uploader(HttpStatusCode.Accepted, """{"intakeId":"i1"}""", json).upload(image)

        assertThat(result).isEqualTo(Result.Success("i1"))
        val request = requests.single()
        assertThat(request.method).isEqualTo(HttpMethod.Post)
        assertThat(request.url.fullPath).isEqualTo("/api/receipts/uploads")
        assertThat((request.body as OutgoingContent).contentType.toString()).contains("multipart/form-data")
        assertThat(bodies.single()).contains("name=\"image\"; filename=\"receipt.jpg\"")
        assertThat(bodies.single()).contains("2026-10-26T12:07:00Z")
    }

    @Test
    fun `no capture time sends no field`() = runTest {
        uploader(HttpStatusCode.Accepted, """{"intakeId":"i1"}""", json)
            .upload(PreparedImage(byteArrayOf(1), 1, 1, capturedAt = null))

        assertThat(bodies.single().indexOf("capturedAt")).isEqualTo(-1)
    }

    @Test
    fun `a 400 carries the limit the server named`() = runTest {
        val result = uploader(HttpStatusCode.BadRequest, detail("at most 1000000 pixels")).upload(image)
        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.BAD_REQUEST, "at most 1000000 pixels")))
    }

    @Test
    fun `a 403 is forbidden`() = runTest {
        val result = uploader(HttpStatusCode.Forbidden, "").upload(image)
        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.FORBIDDEN, null)))
    }

    @Test
    fun `a 409 carries the server's message`() = runTest {
        val result = uploader(HttpStatusCode.Conflict, detail("This exact image has already been uploaded.")).upload(image)
        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.CONFLICT, "This exact image has already been uploaded.")))
    }

    @Test
    fun `a 503 is the full queue`() = runTest {
        val result = uploader(HttpStatusCode.ServiceUnavailable, detail("The extraction queue is full.")).upload(image)
        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.SERVICE_UNAVAILABLE, "The extraction queue is full.")))
    }

    @Test
    fun `no connection is a network failure with no message`() = runTest {
        val result = failing(IOException("down")).upload(image)
        assertThat((result as Result.Error).error.kind).isEqualTo(DataError.Network.NO_INTERNET)
        assertThat(result.error.detail).isNull()
    }

}
