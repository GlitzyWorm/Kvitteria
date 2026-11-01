/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.work

import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.domain.WorkCounts
import dev.jelshoj.kvitteria.core.domain.onSuccess
import io.ktor.client.request.get
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.serialization.Serializable

@Serializable
data class UnassignedLineCountDto(val count: Int)

/** Only the field the badge needs. The Pipeline tab reads the whole report itself. */
@Serializable
data class FailedCountDto(val failed: Int)

class KtorWorkCounts(private val api: ApiHttpClient) : WorkCounts {
    private val unassigned = MutableStateFlow(0)
    private val failed = MutableStateFlow(0)

    override val unassignedLines: StateFlow<Int> = unassigned.asStateFlow()
    override val failedMail: StateFlow<Int> = failed.asStateFlow()

    /** A failure leaves the badges as they were, because a badge isn't worth an error message. */
    override suspend fun refresh(): Unit = coroutineScope {
        val lines = async { safeCall<UnassignedLineCountDto> { api.client.get("/api/receipt-lines/unassigned/count") } }
        val status = async { safeCall<FailedCountDto> { api.client.get("/api/pipeline/status") } }
        lines.await().onSuccess { unassigned.value = it.count }
        status.await().onSuccess { failed.value = it.failed }
    }

    override fun setUnassignedLines(count: Int) {
        unassigned.value = count
    }

    override fun setFailedMail(count: Int) {
        failed.value = count
    }
}
