/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.update

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isInstanceOf
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.HttpClientFactory
import dev.jelshoj.kvitteria.core.data.session.FakeServerSettings
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.session.PublicUpdateStatus
import dev.jelshoj.kvitteria.core.domain.session.UpdateState
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.MockRequestHandler
import io.ktor.client.engine.mock.respond
import io.ktor.client.engine.mock.respondError
import io.ktor.client.plugins.defaultRequest
import io.ktor.client.request.HttpRequestData
import io.ktor.client.request.header
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpMethod
import io.ktor.http.HttpStatusCode
import io.ktor.http.fullPath
import io.ktor.http.headersOf
import java.io.IOException
import java.time.Instant
import kotlinx.coroutines.test.runTest
import org.junit.jupiter.api.Test

class KtorUpdateRepositoryTest {
    private val json = headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString())
    private val requests = mutableListOf<HttpRequestData>()

    private fun repository(handler: MockRequestHandler): KtorUpdateRepository {
        val engine = MockEngine { request ->
            requests += request
            handler(request)
        }
        val anonymous = HttpClientFactory.anonymous(engine, FakeServerSettings())
        // Only has to be told apart from the anonymous client, so a fixed header stands in for the session.
        val api = ApiHttpClient(anonymous.client.config { defaultRequest { header(HttpHeaders.Authorization, "Bearer token") } })
        return KtorUpdateRepository(api, anonymous)
    }

    @Test
    fun `reads what Caddy serves`() = runTest {
        val status = repository {
            respond("""{"state":"running","targetVersion":"1.2.0","updatedAt":"2026-11-02T12:00:00Z"}""", HttpStatusCode.OK, json)
        }.publicStatus()

        assertThat(status).isEqualTo(PublicUpdateStatus(UpdateState.Running, "1.2.0", Instant.parse("2026-11-02T12:00:00Z")))
        assertThat(requests.single().url.fullPath).isEqualTo("/update-status")
    }

    @Test
    fun `carries no bearer, because it has to work without a session`() = runTest {
        repository { respond("""{"state":"running","updatedAt":"2026-11-02T12:00:00Z"}""", HttpStatusCode.OK, json) }.publicStatus()

        assertThat(requests.single().headers[HttpHeaders.Authorization]).isNull()
    }

    @Test
    fun `404 means no update has run`() = runTest {
        assertThat(repository { respondError(HttpStatusCode.NotFound) }.publicStatus()).isNull()
    }

    @Test
    fun `a state this app doesn't know reads as Unknown`() = runTest {
        val status = repository {
            respond("""{"state":"paused","targetVersion":"","updatedAt":"2026-11-02T12:00:00Z"}""", HttpStatusCode.OK, json)
        }.publicStatus()

        assertThat(status?.state).isEqualTo(UpdateState.Unknown)
        assertThat(status?.targetVersion).isNull()
    }

    @Test
    fun `hyphenated states are read by the script's spelling`() = runTest {
        val status = repository {
            respond("""{"state":"restore-required","updatedAt":"2026-11-02T12:00:00Z"}""", HttpStatusCode.OK, json)
        }.publicStatus()

        assertThat(status?.state).isEqualTo(UpdateState.RestoreRequired)
    }

    @Test
    fun `a body that isn't the file is nothing to read`() = runTest {
        assertThat(repository { respond("<html>hello</html>", HttpStatusCode.OK) }.publicStatus()).isNull()
        assertThat(repository { respond("""{"state":"running"}""", HttpStatusCode.OK, json) }.publicStatus()).isNull()
    }

    @Test
    fun `nothing answering is nothing to read`() = runTest {
        assertThat(repository { throw IOException("refused") }.publicStatus()).isNull()
    }

    @Test
    fun `the status maps the last update`() = runTest {
        val result = repository {
            respond(
                """{"currentVersion":"1.1.0","latestVersion":"1.2.0","updateAvailable":true,"releaseUrl":"https://x",
                    "lastCheckedAt":null,"canStart":true,"state":"rolled-back","targetVersion":"1.2.0","detail":"why",
                    "updatedAt":null}""",
                HttpStatusCode.OK,
                json,
            )
        }.status() as Result.Success

        assertThat(result.data.latestVersion).isEqualTo("1.2.0")
        assertThat(result.data.state).isEqualTo(UpdateState.RolledBack)
        assertThat(result.data.detail).isEqualTo("why")
    }

    @Test
    fun `an older server's 404 is a failure, which hides the row`() = runTest {
        assertThat(repository { respondError(HttpStatusCode.NotFound) }.status()).isInstanceOf(Result.Error::class)
    }

    @Test
    fun `starting posts with no body`() = runTest {
        repository { respond("", HttpStatusCode.Accepted) }.start()

        assertThat(requests.single().method).isEqualTo(HttpMethod.Post)
        assertThat(requests.single().url.fullPath).isEqualTo("/api/system/update")
    }

    @Test
    fun `a refusal carries the server's message`() = runTest {
        val result = repository {
            respond("""{"detail":"An update is already running."}""", HttpStatusCode.Conflict, json)
        }.start()

        assertThat(result).isEqualTo(Result.Error(ApiError(DataError.Network.CONFLICT, "An update is already running.")))
    }
}

class PublicUpdateStatusTest {
    private val changed = Instant.parse("2026-11-02T12:00:00Z")

    @Test
    fun `running is believed for half an hour`() {
        val status = PublicUpdateStatus(UpdateState.Running, "1.2.0", changed)

        assertThat(status.stateAt(changed.plusSeconds(29 * 60))).isEqualTo(UpdateState.Running)
        assertThat(status.stateAt(changed.plusSeconds(31 * 60))).isEqualTo(UpdateState.Interrupted)
    }

    @Test
    fun `only running goes stale`() {
        val status = PublicUpdateStatus(UpdateState.RestoreRequired, "1.2.0", changed)

        assertThat(status.stateAt(changed.plusSeconds(86_400))).isEqualTo(UpdateState.RestoreRequired)
    }
}
