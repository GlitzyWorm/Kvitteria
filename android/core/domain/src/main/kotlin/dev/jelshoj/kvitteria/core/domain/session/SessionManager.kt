/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.session

import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import kotlinx.coroutines.flow.StateFlow

/** How an attempt to get a usable token ended. */
sealed interface SessionState {
    data class Active(val session: StoredSession) : SessionState

    /** No session is left, because there never was one or the identity provider rejected it. */
    data object Ended : SessionState

    /**
     * The refresh never reached the identity provider. The stored session is kept: the token may
     * outlive the outage, and losing connection is no reason to sign the household out.
     */
    data object Unreachable : SessionState
}

/** The signed-in session, and the single place a refresh is allowed to happen. */
interface SessionManager {
    val session: StateFlow<StoredSession?>

    /**
     * Who this account is to the household, from `GET /api/users/me`. Null until the gate has resolved
     * it, because a GoTrue session proves identity and not membership.
     */
    val member: StateFlow<CurrentMember?>

    suspend fun load(): StoredSession?

    suspend fun adopt(session: StoredSession)

    fun setMember(member: CurrentMember?)

    /** A token good for the next minute, refreshing first if it isn't. */
    suspend fun ensureFresh(): SessionState

    /**
     * Refreshes even if the stored token still looks valid, unless someone else replaced
     * [spentAccessToken] while this caller waited. That check is what keeps a burst of 401s from
     * spending the rotating refresh token more than once.
     */
    suspend fun refresh(spentAccessToken: String): SessionState

    /** Tells GoTrue, then forgets the session locally whether or not that worked. */
    suspend fun signOut()

    /** Forgets the session locally without telling anyone. */
    suspend fun clear()
}

/** Where the session lives between launches. */
interface SessionStore {
    suspend fun load(): StoredSession?

    suspend fun save(session: StoredSession)

    suspend fun clear()
}

/** Which server this app talks to. Empty until someone has typed one and it has been verified. */
interface ServerSettings {
    val address: StateFlow<String?>

    suspend fun load(): String?

    suspend fun save(address: String)
}
