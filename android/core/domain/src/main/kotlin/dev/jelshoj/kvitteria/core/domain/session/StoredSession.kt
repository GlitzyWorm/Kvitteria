/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.session

import java.time.Duration
import java.time.Instant

/**
 * The part of a GoTrue session worth keeping between launches.
 *
 * [refreshToken] is single-use: GoTrue rotates it on every refresh with a ten-second reuse interval, so
 * two concurrent refreshes spend the session. [email] travels with it so the rejected state can name
 * the address that was refused.
 */
data class StoredSession(
    val accessToken: String,
    val refreshToken: String,
    val expiresAt: Instant,
    val email: String,
) {
    fun isExpiring(now: Instant, skew: Duration = REFRESH_SKEW): Boolean =
        Duration.between(now, expiresAt) <= skew

    companion object {
        /**
         * How early a token counts as spent. A request starting inside this window would very likely
         * arrive after expiry, and a proactive refresh is cheaper than a 401 and a retry.
         */
        val REFRESH_SKEW: Duration = Duration.ofSeconds(60)

        /**
         * Builds a session from a GoTrue token response. [expiresAt] wins because it comes from the
         * server's clock. The relative form is only correct at the moment the response is read, and a
         * response with neither is already spent, which sends it straight to a refresh.
         */
        fun fromTokenResponse(
            accessToken: String,
            refreshToken: String,
            expiresAt: Long?,
            expiresIn: Long?,
            email: String,
            now: Instant,
        ): StoredSession = StoredSession(
            accessToken = accessToken,
            refreshToken = refreshToken,
            expiresAt = expiresAt?.let(Instant::ofEpochSecond) ?: now.plusSeconds(expiresIn ?: 0),
            email = email,
        )
    }
}
