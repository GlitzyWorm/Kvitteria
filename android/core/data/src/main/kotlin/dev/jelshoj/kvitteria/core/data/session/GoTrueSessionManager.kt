/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.session

import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import dev.jelshoj.kvitteria.core.domain.session.SessionStore
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import java.time.Clock
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

class GoTrueSessionManager(
    private val store: SessionStore,
    private val tokens: TokenClient,
    private val clock: Clock,
) : SessionManager {
    /**
     * GoTrue rotates the refresh token on every use and honours the previous one for only ten seconds.
     * Two refreshes racing don't merely duplicate work: the loser can spend a token the winner has
     * already replaced and log the household out. One at a time, always.
     */
    private val refreshLock = Mutex()

    private val current = MutableStateFlow<StoredSession?>(null)
    override val session: StateFlow<StoredSession?> = current.asStateFlow()

    private val currentMember = MutableStateFlow<CurrentMember?>(null)
    override val member: StateFlow<CurrentMember?> = currentMember.asStateFlow()

    override suspend fun load(): StoredSession? {
        current.value = store.load()
        return current.value
    }

    override suspend fun adopt(session: StoredSession) {
        store.save(session)
        current.value = session
    }

    override fun setMember(member: CurrentMember?) {
        currentMember.value = member
    }

    override suspend fun ensureFresh(): SessionState {
        val session = current.value ?: return SessionState.Ended
        return if (session.isExpiring(clock.instant())) refresh(session.accessToken) else SessionState.Active(session)
    }

    override suspend fun refresh(spentAccessToken: String): SessionState = refreshLock.withLock {
        val session = current.value ?: return SessionState.Ended

        // Someone refreshed while this caller queued. Their result is the answer, and asking again would
        // spend a token that is now current.
        if (session.accessToken != spentAccessToken) return SessionState.Active(session)

        when (val outcome = tokens.refresh(session)) {
            is RefreshOutcome.Refreshed -> {
                adopt(outcome.session)
                SessionState.Active(outcome.session)
            }
            RefreshOutcome.Unreachable -> SessionState.Unreachable
            RefreshOutcome.Rejected -> {
                clear()
                SessionState.Ended
            }
        }
    }

    override suspend fun signOut() {
        current.value?.let { tokens.signOut(it) }
        clear()
    }

    override suspend fun clear() {
        store.clear()
        currentMember.value = null
        current.value = null
    }
}
