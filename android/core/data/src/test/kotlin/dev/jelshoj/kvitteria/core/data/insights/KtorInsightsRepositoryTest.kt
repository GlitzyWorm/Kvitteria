/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.insights

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.HttpClientFactory
import dev.jelshoj.kvitteria.core.data.session.FakeServerSettings
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.respond
import io.ktor.client.request.HttpRequestData
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.headersOf
import kotlinx.coroutines.test.runTest
import org.junit.jupiter.api.Test

class KtorInsightsRepositoryTest {
    private val json = headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString())
    private val requests = mutableListOf<HttpRequestData>()

    private fun repository(): KtorInsightsRepository {
        val engine = MockEngine { request ->
            requests += request
            respond(
                """{"itemId":"i","displayName":"Mælk","months":[],"latestPrice":null,"lowestPrice":null,
                    "changePercent":null,"rawNameKey":null,"rawNameLabel":null}""",
                HttpStatusCode.OK,
                json,
            )
        }
        return KtorInsightsRepository(ApiHttpClient(HttpClientFactory.anonymous(engine, FakeServerSettings()).client))
    }

    @Test
    fun `a narrowed history sends the name`() = runTest {
        repository().priceHistory("i", 6, "arla letmælk 1l")

        assertThat(requests.single().url.parameters["name"]).isEqualTo("arla letmælk 1l")
        assertThat(requests.single().url.parameters["months"]).isEqualTo("6")
    }

    @Test
    fun `an unnarrowed history sends no name`() = runTest {
        repository().priceHistory("i", 6, null)

        assertThat(requests.single().url.parameters["name"]).isNull()
    }
}
