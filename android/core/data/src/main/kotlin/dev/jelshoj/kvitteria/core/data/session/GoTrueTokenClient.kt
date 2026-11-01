/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.session

import dev.jelshoj.kvitteria.core.data.network.AnonymousHttpClient
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import io.ktor.client.call.body
import io.ktor.client.request.bearerAuth
import io.ktor.client.request.parameter
import io.ktor.client.request.post
import io.ktor.client.request.setBody
import io.ktor.http.ContentType
import io.ktor.http.contentType
import io.ktor.http.isSuccess
import io.ktor.serialization.ContentConvertException
import java.io.IOException
import java.time.Clock
import kotlin.coroutines.cancellation.CancellationException
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException

/**
 * GoTrue's token response. It belongs to the identity provider rather than to the API, so it isn't part
 * of the OpenAPI document. A signup can answer 200 with no tokens when confirmation is required.
 */
@Serializable
data class GoTrueSessionDto(
    @SerialName("access_token") val accessToken: String = "",
    @SerialName("refresh_token") val refreshToken: String = "",
    @SerialName("expires_in") val expiresIn: Long? = null,
    @SerialName("expires_at") val expiresAt: Long? = null,
    val user: GoTrueUserDto? = null,
) {
    val hasTokens: Boolean get() = accessToken.isNotEmpty() && refreshToken.isNotEmpty()
}

@Serializable
data class GoTrueUserDto(val email: String? = null)

fun GoTrueSessionDto.toStoredSession(fallbackEmail: String, clock: Clock): StoredSession =
    StoredSession.fromTokenResponse(accessToken, refreshToken, expiresAt, expiresIn, user?.email ?: fallbackEmail, clock.instant())

/** Caddy strips this prefix and GoTrue serves at its root. */
const val AUTH_PREFIX: String = "/auth/v1"

sealed interface RefreshOutcome {
    data class Refreshed(val session: StoredSession) : RefreshOutcome

    data object Rejected : RefreshOutcome

    data object Unreachable : RefreshOutcome
}

/** The token calls [GoTrueSessionManager] needs. */
interface TokenClient {
    suspend fun refresh(session: StoredSession): RefreshOutcome

    suspend fun signOut(session: StoredSession)
}

/** Always the anonymous client. Sending a refresh through the client that refreshes on 401 would recurse. */
class GoTrueTokenClient(
    private val http: AnonymousHttpClient,
    private val clock: Clock,
) : TokenClient {
    override suspend fun refresh(session: StoredSession): RefreshOutcome = try {
        val response = http.client.post("$AUTH_PREFIX/token") {
            parameter("grant_type", "refresh_token")
            contentType(ContentType.Application.Json)
            setBody(RefreshGrant(session.refreshToken))
        }
        if (response.status.isSuccess()) {
            val tokens = response.body<GoTrueSessionDto>()
            if (tokens.hasTokens) RefreshOutcome.Refreshed(tokens.toStoredSession(session.email, clock)) else RefreshOutcome.Rejected
        } else {
            RefreshOutcome.Rejected
        }
    } catch (_: IOException) {
        RefreshOutcome.Unreachable
    } catch (_: SerializationException) {
        RefreshOutcome.Rejected
    } catch (_: ContentConvertException) {
        RefreshOutcome.Rejected
    }

    /**
     * Best effort. The local session is cleared whatever happens here, because someone who taps sign
     * out is signed out even with the server unreachable.
     */
    override suspend fun signOut(session: StoredSession) {
        try {
            http.client.post("$AUTH_PREFIX/logout") { bearerAuth(session.accessToken) }
        } catch (e: Exception) {
            if (e is CancellationException) throw e
        }
    }

    @Serializable
    private data class RefreshGrant(@SerialName("refresh_token") val refreshToken: String)
}
