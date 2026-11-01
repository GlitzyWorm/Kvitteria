/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.session

import dev.jelshoj.kvitteria.core.domain.Error
import dev.jelshoj.kvitteria.core.domain.Result
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

data class ServerVersion(val version: String, val commit: String)

enum class ProbeError : Error {
    /** Nothing answered. */
    UNREACHABLE,

    /** Something answered, and it wasn't a Kvitteria backend. */
    NOT_KVITTERIA,
}

/** Whether a server already has an active admin, as far as an anonymous caller can tell. */
enum class HouseholdState {
    /** The report didn't name the household check, or couldn't be read at all. */
    Unknown,

    /** No active admin yet. The first account to sign in claims the household. */
    Unclaimed,

    /** An admin exists, so a new account needs to have been pre-authorized. */
    Claimed,
}

/** What can be asked of a server before anyone is signed in. */
interface ServerProbe {
    /** Proves an origin is a Kvitteria backend. [origin] is a candidate; null means the stored server. */
    suspend fun version(origin: String? = null): Result<ServerVersion, ProbeError>

    suspend fun household(): HouseholdState
}

/**
 * Reads `GET /health/ready`. The `household` check inside it is the only signal available before a token
 * exists, and it reports `Degraded` exactly while no active admin does.
 */
object HealthProbe {
    private val json = Json { ignoreUnknownKeys = true }

    /**
     * Must be given the body whatever the status code was: the health middleware answers 503 for an
     * unhealthy report, and a cold Ollama is unhealthy while the household question is perfectly
     * answerable.
     */
    fun readHouseholdState(body: String?): HouseholdState {
        if (body.isNullOrBlank()) return HouseholdState.Unknown

        val root = try {
            json.parseToJsonElement(body)
        } catch (_: SerializationException) {
            return HouseholdState.Unknown
        }

        val checks = (root as? JsonObject)?.get("checks") as? JsonArray ?: return HouseholdState.Unknown
        val household = checks.filterIsInstance<JsonObject>().firstOrNull { check ->
            (check["name"] as? JsonPrimitive)?.takeIf { it.isString }?.content == "household"
        } ?: return HouseholdState.Unknown

        return when ((household["status"] as? JsonPrimitive)?.takeIf { it.isString }?.content) {
            "Degraded" -> HouseholdState.Unclaimed
            "Healthy" -> HouseholdState.Claimed
            // Unhealthy means the check itself failed, so nothing is known about the household.
            else -> HouseholdState.Unknown
        }
    }
}
