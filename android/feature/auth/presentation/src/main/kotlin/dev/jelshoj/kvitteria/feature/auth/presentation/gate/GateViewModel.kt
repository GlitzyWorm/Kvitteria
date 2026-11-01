/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.presentation.gate

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.session.HouseholdState
import dev.jelshoj.kvitteria.core.domain.session.ServerProbe
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class GateState(
    /** The address that failed, so the retry screen names it. Null while resolving. */
    val unreachableAddress: String? = null,
)

sealed interface GateAction {
    data object OnRetry : GateAction
    data object OnChangeServer : GateAction
}

sealed interface GateEvent {
    data object GoToServer : GateEvent
    data object GoToClaim : GateEvent
    data object GoToSignIn : GateEvent
    data object GoToMain : GateEvent
    data object GoToRejected : GateEvent
}

/**
 * Decides where the app starts. The order is load-bearing: which server, whether it's a Kvitteria
 * backend, whether there's a session, whether it needs a refresh, and only then who this is to the
 * household. That last call is also what claims a fresh household, because the backend bootstraps the
 * first authenticated caller into the admin.
 */
class GateViewModel(
    private val settings: ServerSettings,
    private val sessions: SessionManager,
    private val probe: ServerProbe,
    private val members: MemberRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(GateState())
    val state = _state.asStateFlow()

    private val _events = Channel<GateEvent>()
    val events = _events.receiveAsFlow()

    init {
        resolve()
    }

    fun onAction(action: GateAction) {
        when (action) {
            GateAction.OnRetry -> resolve()
            GateAction.OnChangeServer -> viewModelScope.launch { _events.send(GateEvent.GoToServer) }
        }
    }

    private fun resolve() {
        _state.update { it.copy(unreachableAddress = null) }
        viewModelScope.launch {
            val address = settings.load()
            if (address == null) {
                _events.send(GateEvent.GoToServer)
                return@launch
            }

            // Fails fast, which is what turns "the app hangs" into "the server is off".
            if (probe.version() is Result.Error) {
                showUnreachable(address)
                return@launch
            }

            if (sessions.load() == null) {
                goToSignIn()
                return@launch
            }

            when (sessions.ensureFresh()) {
                SessionState.Ended -> goToSignIn()
                SessionState.Unreachable -> showUnreachable(address)
                is SessionState.Active -> resolveMember(address)
            }
        }
    }

    private suspend fun resolveMember(address: String) {
        when (val result = members.refreshCurrent()) {
            is Result.Success -> _events.send(GateEvent.GoToMain)
            is Result.Error -> when (result.error.kind) {
                // GoTrue knows this account and the household doesn't. The session is kept, so the
                // rejected state can name the address that was refused.
                DataError.Network.FORBIDDEN -> _events.send(GateEvent.GoToRejected)
                DataError.Network.UNAUTHORIZED -> {
                    sessions.clear()
                    goToSignIn()
                }
                else -> showUnreachable(address)
            }
        }
    }

    private suspend fun goToSignIn() {
        when (probe.household()) {
            HouseholdState.Unclaimed -> _events.send(GateEvent.GoToClaim)
            HouseholdState.Claimed, HouseholdState.Unknown -> _events.send(GateEvent.GoToSignIn)
        }
    }

    private fun showUnreachable(address: String) {
        _state.update { it.copy(unreachableAddress = address) }
    }
}
