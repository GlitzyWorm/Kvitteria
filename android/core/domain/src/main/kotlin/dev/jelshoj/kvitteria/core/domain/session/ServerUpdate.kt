/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.session

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import java.time.Duration
import java.time.Instant

/** What a person runs on the server, shown where the app can't do it for them. */
const val UPDATE_COMMAND = "./scripts/update.sh"
const val RESTORE_COMMAND = "./scripts/update.sh --restore"

/** Named by the update script rather than by .NET, so these are its spellings. */
enum class UpdateState(val wireName: String) {
    Running("running"),
    Succeeded("succeeded"),
    Failed("failed"),
    RolledBack("rolled-back"),
    RestoreRequired("restore-required"),

    /** A running state too old to be true. The host was probably lost part way through. */
    Interrupted("interrupted"),
    Unknown("");

    companion object {
        fun fromWire(name: String): UpdateState = entries.firstOrNull { it.wireName == name } ?: Unknown
    }
}

/** `GET /api/system/update`. [state] is the last update's, and null before any has run. */
data class UpdateStatus(
    val latestVersion: String?,
    val updateAvailable: Boolean,
    val releaseUrl: String?,
    val canStart: Boolean,
    val state: UpdateState?,
    val detail: String?,
)

/** `GET /update-status`, which Caddy serves to anyone, so it says what's happening and never why. */
data class PublicUpdateStatus(
    val state: UpdateState,
    val targetVersion: String?,
    val updatedAt: Instant,
) {
    /** The script can't report that it died, so a `running` that hasn't changed in this long isn't believed. */
    fun stateAt(now: Instant): UpdateState =
        if (state == UpdateState.Running && Duration.between(updatedAt, now) > RUNNING_LIMIT) UpdateState.Interrupted else state

    private companion object {
        val RUNNING_LIMIT: Duration = Duration.ofMinutes(30)
    }
}

interface UpdateRepository {
    suspend fun status(): Result<UpdateStatus, ApiError>

    suspend fun start(): EmptyResult<ApiError>

    /**
     * Null when there's nothing to read, for any reason: no update has run, or nothing answered. This is
     * what clients have while the backend is down, so it works with no session.
     */
    suspend fun publicStatus(): PublicUpdateStatus?
}
