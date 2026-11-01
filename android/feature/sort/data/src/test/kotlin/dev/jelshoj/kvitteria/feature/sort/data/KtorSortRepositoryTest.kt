/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.sort.data

import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.hasSize
import assertk.assertions.isEmpty
import assertk.assertions.isEqualTo
import assertk.assertions.isInstanceOf
import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.serialization.KvitteriaJson
import dev.jelshoj.kvitteria.core.domain.Result
import io.ktor.client.HttpClient
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.respond
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.headersOf
import io.ktor.serialization.kotlinx.json.json
import kotlinx.coroutines.test.runTest
import org.junit.jupiter.api.Test

class KtorSortRepositoryTest {
    private val total = 230
    private val pagesRequested = mutableListOf<Int>()

    private val engine = MockEngine { request ->
        val page = request.url.parameters["page"]!!.toInt()
        val size = request.url.parameters["pageSize"]!!.toInt()
        pagesRequested += page
        val from = (page - 1) * size
        val items = (from until minOf(from + size, total)).joinToString(",") { index ->
            """{"id":"l$index","rawName":"R","quantity":1,"unitPrice":2.5,"lineTotal":2.5,"receiptId":"r",
                "merchantName":"Netto","purchaseDate":"2026-08-18T10:00:00Z","memberId":null,"memberDisplayName":null}"""
        }
        respond(
            """{"items":[$items],"page":$page,"pageSize":$size,"totalCount":$total}""",
            HttpStatusCode.OK,
            headersOf(HttpHeaders.ContentType, "application/json"),
        )
    }

    private val api = ApiHttpClient(HttpClient(engine) { install(ContentNegotiation) { json(KvitteriaJson) } })

    @Test
    fun `the queue pages through more than 100 lines`() = runTest {
        val result = KtorSortRepository(api).queue()

        assertThat(result).isInstanceOf(Result.Success::class)
        assertThat((result as Result.Success).data).hasSize(total)
        assertThat(pagesRequested).containsExactly(1, 2, 3)
    }

    @Test
    fun `skips are held until cleared`() {
        val skipped = InMemorySkippedLines()
        skipped.skip("a")
        assertThat(skipped.ids).isEqualTo(setOf("a"))
        skipped.clear()
        assertThat(skipped.ids).isEmpty()
    }
}
