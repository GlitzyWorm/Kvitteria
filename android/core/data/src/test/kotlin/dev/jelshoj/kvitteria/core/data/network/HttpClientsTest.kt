/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.network

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.data.session.FakeServerSettings
import dev.jelshoj.kvitteria.core.data.session.FakeSessionStore
import dev.jelshoj.kvitteria.core.data.session.GoTrueSessionManager
import dev.jelshoj.kvitteria.core.data.session.GoTrueTokenClient
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.respondOk
import io.ktor.client.request.HttpRequestData
import io.ktor.client.request.get
import io.ktor.http.HttpHeaders
import io.ktor.http.fullPath
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import kotlinx.coroutines.test.runTest
import org.junit.jupiter.api.Test

class HttpClientsTest {
    private val now = Instant.parse("2026-08-26T12:00:00Z")
    private val clock = Clock.fixed(now, ZoneOffset.UTC)
    private val session = StoredSession("first", "refresh", now.plusSeconds(3600), "you@household.dk")

    private val requests = mutableListOf<HttpRequestData>()
    private val engine = MockEngine { request ->
        requests += request
        respondOk()
    }
    private val settings = FakeServerSettings("http://server.test:8080")
    private val store = FakeSessionStore(session)
    private val anonymous = HttpClientFactory.anonymous(engine, settings)
    private val manager = GoTrueSessionManager(store, GoTrueTokenClient(anonymous, clock), clock)
    private val api = HttpClientFactory.api(engine, settings, manager)

    private fun bearerOf(index: Int = requests.lastIndex) = requests[index].headers[HttpHeaders.Authorization]

    @Test
    fun `api requests carry the bearer`() = runTest {
        manager.load()
        api.client.get("/api/users/me")
        assertThat(bearerOf()).isEqualTo("Bearer first")
    }

    @Test
    fun `identity requests carry no bearer`() = runTest {
        manager.load()
        anonymous.client.get("/auth/v1/health")
        assertThat(bearerOf()).isNull()
    }

    @Test
    fun `a session change clears the cached token`() = runTest {
        manager.load()
        api.client.get("/api/users/me")

        manager.adopt(session.copy(accessToken = "second"))
        api.client.get("/api/users/me")

        assertThat(bearerOf()).isEqualTo("Bearer second")
    }

    @Test
    fun `signing out stops the cached token being sent`() = runTest {
        manager.load()
        api.client.get("/api/users/me")

        manager.clear()
        api.client.get("/api/users/me")

        assertThat(bearerOf()).isNull()
    }

    @Test
    fun `a changed server applies to the next request`() = runTest {
        manager.load()
        api.client.get("/api/receipts?page=2")
        assertThat(requests.last().url.toString()).isEqualTo("http://server.test:8080/api/receipts?page=2")

        settings.save("https://other.example.dk")
        api.client.get("/api/receipts?page=2")
        assertThat(requests.last().url.toString()).isEqualTo("https://other.example.dk/api/receipts?page=2")
    }

    @Test
    fun `an absolute URL is left alone, so a candidate can be probed before it's stored`() = runTest {
        anonymous.client.get("http://candidate.test:9000/version")
        assertThat(requests.last().url.host).isEqualTo("candidate.test")
        assertThat(requests.last().url.port).isEqualTo(9000)
        assertThat(requests.last().url.fullPath).isEqualTo("/version")
    }

    @Test
    fun `no stored server is a network failure rather than a request to localhost`() = runTest {
        val none = HttpClientFactory.anonymous(engine, FakeServerSettings(null))
        val result: Result<Unit, ApiError> = safeCall { none.client.get("/version") }
        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.NO_INTERNET)))
        assertThat(requests.size).isEqualTo(0)
    }
}
