/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.update

import dev.jelshoj.kvitteria.core.data.network.AnonymousHttpClient
import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.data.network.safeEmptyCall
import dev.jelshoj.kvitteria.core.data.serialization.InstantSerializer
import dev.jelshoj.kvitteria.core.data.serialization.KvitteriaJson
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.core.domain.session.PublicUpdateStatus
import dev.jelshoj.kvitteria.core.domain.session.UpdateRepository
import dev.jelshoj.kvitteria.core.domain.session.UpdateState
import dev.jelshoj.kvitteria.core.domain.session.UpdateStatus
import io.ktor.client.plugins.timeout
import io.ktor.client.request.get
import io.ktor.client.request.post
import io.ktor.client.statement.bodyAsText
import io.ktor.http.isSuccess
import java.io.IOException
import java.time.Instant
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException

@Serializable
data class UpdateStatusDto(
    val latestVersion: String? = null,
    val updateAvailable: Boolean,
    val releaseUrl: String? = null,
    val canStart: Boolean,
    val state: String? = null,
    val detail: String? = null,
)

/** Written by the update script and served by Caddy, so it's outside the OpenAPI document. */
@Serializable
data class PublicUpdateStatusDto(
    val state: String,
    val targetVersion: String? = null,
    @Serializable(InstantSerializer::class) val updatedAt: Instant,
)

fun UpdateStatusDto.toDomain() = UpdateStatus(
    latestVersion = latestVersion,
    updateAvailable = updateAvailable,
    releaseUrl = releaseUrl,
    canStart = canStart,
    state = state?.let(UpdateState::fromWire),
    detail = detail,
)

/** Short, because this runs on the gate and between polls. */
private const val STATUS_TIMEOUT_MS = 8_000L

class KtorUpdateRepository(
    private val api: ApiHttpClient,
    private val anonymous: AnonymousHttpClient,
) : UpdateRepository {
    override suspend fun status(): Result<UpdateStatus, ApiError> =
        safeCall<UpdateStatusDto> { api.client.get("/api/system/update") }.map(UpdateStatusDto::toDomain)

    override suspend fun start(): EmptyResult<ApiError> =
        safeEmptyCall { api.client.post("/api/system/update") }

    override suspend fun publicStatus(): PublicUpdateStatus? = try {
        val response = anonymous.client.get("/update-status") { timeout { requestTimeoutMillis = STATUS_TIMEOUT_MS } }
        if (response.status.isSuccess()) {
            val dto = KvitteriaJson.decodeFromString<PublicUpdateStatusDto>(response.bodyAsText())
            PublicUpdateStatus(UpdateState.fromWire(dto.state), dto.targetVersion?.takeIf { it.isNotBlank() }, dto.updatedAt)
        } else {
            null
        }
    } catch (_: IOException) {
        null
    } catch (_: SerializationException) {
        null
    } catch (_: IllegalArgumentException) {
        null
    }
}
