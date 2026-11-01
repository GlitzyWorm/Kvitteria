/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.session

import assertk.assertThat
import assertk.assertions.containsOnly
import assertk.assertions.isEqualTo
import assertk.assertions.isNotNull
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.data.network.HttpClientFactory
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.MockRequestHandleScope
import io.ktor.client.engine.mock.respond
import io.ktor.client.request.HttpRequestData
import io.ktor.client.request.HttpResponseData
import io.ktor.client.request.get
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.headersOf
import java.io.IOException
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import org.junit.jupiter.api.Test

class SessionRefreshTest {
    private val now = Instant.parse("2026-08-26T12:00:00Z")
    private val clock = Clock.fixed(now, ZoneOffset.UTC)
    private val json = headersOf(HttpHeaders.ContentType, "application/json")
    private val old = StoredSession("old", "refresh-1", now.plusSeconds(3600), "you@household.dk")

    private val refreshes = AtomicInteger()
    private val apiTokens = CopyOnWriteArrayList<String>()

    private fun MockRequestHandleScope.refreshed(access: String): HttpResponseData = respond(
        """{"access_token":"$access","refresh_token":"refresh-2","expires_in":3600}""",
        HttpStatusCode.OK,
        json,
    )

    private fun MockRequestHandleScope.apiAnswer(request: HttpRequestData, valid: String): HttpResponseData {
        val token = request.headers[HttpHeaders.Authorization].orEmpty().removePrefix("Bearer ")
        apiTokens += token
        return if (token == valid) respond("""{"ok":true}""", HttpStatusCode.OK, json) else respond("", HttpStatusCode.Unauthorized)
    }

    private inner class Fixture(
        session: StoredSession?,
        identity: suspend MockRequestHandleScope.() -> HttpResponseData,
        valid: String = "new",
    ) {
        val store = FakeSessionStore(session)
        val settings = FakeServerSettings()
        val engine = MockEngine { request ->
            if (request.url.encodedPath.startsWith("/auth/v1")) {
                refreshes.incrementAndGet()
                identity()
            } else {
                apiAnswer(request, valid)
            }
        }
        val manager = GoTrueSessionManager(
            store,
            GoTrueTokenClient(HttpClientFactory.anonymous(engine, settings), clock),
            clock,
        )
        val api = HttpClientFactory.api(engine, settings, manager)

        suspend fun call(): Result<Map<String, Boolean>, ApiError> = safeCall { api.client.get("/api/x") }
    }

    @Test
    fun `a burst of 401s sends one refresh and retries each request once`() = runTest {
        val fixture = Fixture(old, identity = {
            delay(50)
            refreshed("new")
        })
        fixture.manager.load()

        val results = withContext(Dispatchers.Default) {
            coroutineScope { (1..8).map { async { fixture.call() } }.awaitAll() }
        }

        assertThat(refreshes.get()).isEqualTo(1)
        assertThat(results.toSet()).containsOnly(Result.Success(mapOf("ok" to true)))
        assertThat(apiTokens.count { it == "new" }).isEqualTo(8)
    }

    @Test
    fun `a token about to expire is refreshed before the request`() = runTest {
        val expiring = old.copy(expiresAt = now.plusSeconds(30))
        val fixture = Fixture(expiring, identity = { refreshed("new") })
        fixture.manager.load()

        assertThat(fixture.call()).isEqualTo(Result.Success(mapOf("ok" to true)))
        assertThat(refreshes.get()).isEqualTo(1)
        assertThat(apiTokens.toList()).containsOnly("new")
    }

    @Test
    fun `a rejected refresh clears the session`() = runTest {
        val fixture = Fixture(old, identity = { respond("""{"error":"invalid_grant"}""", HttpStatusCode.BadRequest, json) })
        fixture.manager.load()

        val result = fixture.call()

        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.UNAUTHORIZED)))
        assertThat(fixture.manager.session.value).isNull()
        assertThat(fixture.store.stored).isNull()
    }

    @Test
    fun `a refresh that itself answers 401 is a rejection and starts no other refresh`() = runTest {
        val fixture = Fixture(old, identity = { respond("", HttpStatusCode.Unauthorized) })
        fixture.manager.load()

        fixture.call()

        assertThat(refreshes.get()).isEqualTo(1)
        assertThat(fixture.manager.session.value).isNull()
    }

    @Test
    fun `a refresh that can't reach the server keeps the session and reports a network error`() = runTest {
        val fixture = Fixture(old, identity = { throw IOException("tunnel") })
        fixture.manager.load()

        val result = fixture.call()

        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.NO_INTERNET)))
        assertThat(fixture.manager.session.value).isEqualTo(old)
        assertThat(fixture.store.stored).isNotNull()
    }

    @Test
    fun `a rate limited refresh keeps the session and reports a network error`() = runTest {
        val fixture = Fixture(old, identity = { respond("", HttpStatusCode.TooManyRequests) })
        fixture.manager.load()

        val result = fixture.call()

        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.NO_INTERNET)))
        assertThat(fixture.manager.session.value).isEqualTo(old)
        assertThat(fixture.store.stored).isNotNull()
    }

    @Test
    fun `a refresh answered with a gateway error keeps the session and reports a network error`() = runTest {
        val fixture = Fixture(old, identity = { respond("", HttpStatusCode.BadGateway) })
        fixture.manager.load()

        val result = fixture.call()

        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.NO_INTERNET)))
        assertThat(fixture.manager.session.value).isEqualTo(old)
        assertThat(fixture.store.stored).isNotNull()
    }

    @Test
    fun `an expiring token that can't be refreshed also keeps the session`() = runTest {
        val expiring = old.copy(expiresAt = now.plusSeconds(30))
        val fixture = Fixture(expiring, identity = { throw IOException("tunnel") })
        fixture.manager.load()

        assertThat(fixture.call()).isEqualTo(Result.Error(ApiError(DataError.Network.NO_INTERNET)))
        assertThat(fixture.manager.session.value).isEqualTo(expiring)
    }

    @Test
    fun `sign out clears the session even when the server can't be reached`() = runTest {
        val fixture = Fixture(old, identity = { throw IOException("tunnel") })
        fixture.manager.load()

        fixture.manager.signOut()

        assertThat(fixture.manager.session.value).isNull()
        assertThat(fixture.store.stored).isNull()
    }
}
