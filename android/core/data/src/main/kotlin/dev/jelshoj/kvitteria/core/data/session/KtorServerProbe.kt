/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.session

import dev.jelshoj.kvitteria.core.data.network.AnonymousHttpClient
import dev.jelshoj.kvitteria.core.data.serialization.KvitteriaJson
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.session.HealthProbe
import dev.jelshoj.kvitteria.core.domain.session.HouseholdState
import dev.jelshoj.kvitteria.core.domain.session.ProbeError
import dev.jelshoj.kvitteria.core.domain.session.ServerProbe
import dev.jelshoj.kvitteria.core.domain.session.ServerVersion
import io.ktor.client.plugins.timeout
import io.ktor.client.request.HttpRequestBuilder
import io.ktor.client.request.get
import io.ktor.client.statement.bodyAsText
import io.ktor.http.isSuccess
import java.io.IOException
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException

@Serializable
data class VersionDto(val version: String, val commit: String)

/** Short on purpose: this runs on the splash, and a wrong address should reach the retry screen. */
private const val PROBE_TIMEOUT_MS = 8_000L

class KtorServerProbe(private val http: AnonymousHttpClient) : ServerProbe {
    private fun HttpRequestBuilder.probeTimeout() {
        timeout { requestTimeoutMillis = PROBE_TIMEOUT_MS }
    }

    override suspend fun version(origin: String?): Result<ServerVersion, ProbeError> {
        val (status, body) = try {
            val response = http.client.get("${origin.orEmpty()}/version") { probeTimeout() }
            response.status to response.bodyAsText()
        } catch (_: IOException) {
            return Result.Error(ProbeError.UNREACHABLE)
        }
        if (!status.isSuccess()) return Result.Error(ProbeError.NOT_KVITTERIA)

        return try {
            val version = KvitteriaJson.decodeFromString<VersionDto>(body)
            if (version.version.isBlank()) {
                Result.Error(ProbeError.NOT_KVITTERIA)
            } else {
                Result.Success(ServerVersion(version.version, version.commit))
            }
        } catch (_: SerializationException) {
            Result.Error(ProbeError.NOT_KVITTERIA)
        } catch (_: IllegalArgumentException) {
            Result.Error(ProbeError.NOT_KVITTERIA)
        }
    }

    override suspend fun household(): HouseholdState = try {
        HealthProbe.readHouseholdState(http.client.get("/health/ready") { probeTimeout() }.bodyAsText())
    } catch (_: IOException) {
        HouseholdState.Unknown
    }
}
