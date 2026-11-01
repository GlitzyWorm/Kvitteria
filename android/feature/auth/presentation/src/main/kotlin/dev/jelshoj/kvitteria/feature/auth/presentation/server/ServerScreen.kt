/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.presentation.server

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.tooling.preview.Preview
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCallout
import dev.jelshoj.kvitteria.core.designsystem.KvField
import dev.jelshoj.kvitteria.core.designsystem.KvNote
import dev.jelshoj.kvitteria.core.designsystem.KvitteriaTheme
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.session.ProbeError
import dev.jelshoj.kvitteria.core.domain.session.ServerProbe
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.feature.auth.domain.ServerUrl
import dev.jelshoj.kvitteria.feature.auth.domain.ServerUrlError
import dev.jelshoj.kvitteria.feature.auth.presentation.AuthScaffold
import dev.jelshoj.kvitteria.feature.auth.presentation.R
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel

data class ServerState(
    val address: String = "",
    val isBusy: Boolean = false,
    val routeRemoved: Boolean = false,
    val error: UiText? = null,
)

sealed interface ServerAction {
    data class OnAddressChange(val address: String) : ServerAction
    data object OnConnect : ServerAction
}

sealed interface ServerEvent {
    data object Connected : ServerEvent
}

/**
 * Stores an address only once it has answered as a Kvitteria backend. A session belongs to one server,
 * so a new address also ends the current one.
 */
class ServerViewModel(
    private val settings: ServerSettings,
    private val sessions: SessionManager,
    private val probe: ServerProbe,
) : ViewModel() {
    private val _state = MutableStateFlow(ServerState())
    val state = _state.asStateFlow()

    private val _events = Channel<ServerEvent>()
    val events = _events.receiveAsFlow()

    init {
        viewModelScope.launch {
            // Reached from "change server", the field should show the address in use.
            val current = settings.load().orEmpty()
            _state.update { if (it.address.isEmpty()) it.copy(address = current) else it }
        }
    }

    fun onAction(action: ServerAction) {
        when (action) {
            is ServerAction.OnAddressChange -> _state.update {
                it.copy(address = action.address, error = null, routeRemoved = ServerUrl.looksLikeKnownRoute(action.address))
            }
            ServerAction.OnConnect -> connect()
        }
    }

    private fun connect() {
        if (_state.value.isBusy) return

        val origin = when (val normalized = ServerUrl.normalize(_state.value.address)) {
            is Result.Error -> {
                _state.update { it.copy(error = UiText.StringResource(normalized.error.message())) }
                return
            }
            is Result.Success -> normalized.data
        }

        _state.update { it.copy(address = origin, isBusy = true, error = null) }
        viewModelScope.launch {
            when (val result = probe.version(origin)) {
                is Result.Error -> _state.update {
                    it.copy(isBusy = false, error = UiText.StringResource(result.error.message()))
                }
                is Result.Success -> {
                    val changed = settings.load() != origin
                    settings.save(origin)
                    if (changed) sessions.clear()
                    _state.update { it.copy(isBusy = false) }
                    _events.send(ServerEvent.Connected)
                }
            }
        }
    }

    private fun ServerUrlError.message() = when (this) {
        ServerUrlError.EMPTY -> R.string.server_error_empty
        ServerUrlError.UNPARSEABLE -> R.string.server_error_unparseable
        ServerUrlError.UNSUPPORTED_SCHEME -> R.string.server_error_unsupported_scheme
    }

    private fun ProbeError.message() = when (this) {
        ProbeError.UNREACHABLE -> R.string.server_error_unreachable
        ProbeError.NOT_KVITTERIA -> R.string.server_error_not_kvitteria
    }
}

@Composable
fun ServerRoot(
    onConnected: () -> Unit,
    viewModel: ServerViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()

    ObserveAsEvents(viewModel.events) { event ->
        when (event) {
            ServerEvent.Connected -> onConnected()
        }
    }

    ServerScreen(state = state, onAction = viewModel::onAction)
}

@Composable
fun ServerScreen(
    state: ServerState,
    onAction: (ServerAction) -> Unit,
) {
    AuthScaffold(
        kicker = stringResource(R.string.server_kicker),
        title = stringResource(R.string.server_title),
        blurb = stringResource(R.string.server_blurb),
        foot = stringResource(R.string.server_foot),
    ) {
        KvField(
            label = stringResource(R.string.server_label),
            value = state.address,
            onValueChange = { onAction(ServerAction.OnAddressChange(it)) },
            placeholder = stringResource(R.string.server_placeholder),
            keyboardOptions = KeyboardOptions(
                keyboardType = KeyboardType.Uri,
                imeAction = ImeAction.Go,
                autoCorrectEnabled = false,
            ),
            keyboardActions = KeyboardActions(onGo = { onAction(ServerAction.OnConnect) }),
            modifier = Modifier.fillMaxWidth(),
        )
        if (state.routeRemoved) KvNote(stringResource(R.string.server_route_hint))
        state.error?.let { KvCallout(it.asString()) }
        KvButton(
            text = stringResource(R.string.server_continue),
            onClick = { onAction(ServerAction.OnConnect) },
            style = KvButtonStyle.Primary,
            busy = state.isBusy,
            fill = true,
        )
    }
}

@Preview
@Composable
private fun ServerScreenPreview() {
    KvitteriaTheme {
        ServerScreen(ServerState(address = "http://10.0.2.2:8080"), onAction = {})
    }
}
