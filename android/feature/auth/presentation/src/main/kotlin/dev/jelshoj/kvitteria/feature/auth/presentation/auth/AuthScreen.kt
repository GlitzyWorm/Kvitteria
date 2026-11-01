/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.presentation.auth

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.tooling.preview.Preview
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCallout
import dev.jelshoj.kvitteria.core.designsystem.KvField
import dev.jelshoj.kvitteria.core.designsystem.KvSegmented
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.KvitteriaTheme
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.feature.auth.domain.AuthError
import dev.jelshoj.kvitteria.feature.auth.domain.AuthRepository
import dev.jelshoj.kvitteria.feature.auth.domain.CredentialRules
import dev.jelshoj.kvitteria.feature.auth.presentation.AuthMode
import dev.jelshoj.kvitteria.feature.auth.presentation.AuthScaffold
import dev.jelshoj.kvitteria.feature.auth.presentation.R
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel
import org.koin.core.parameter.parametersOf

data class AuthState(
    val mode: AuthMode = AuthMode.SignIn,
    val email: String = "",
    val password: String = "",
    val isBusy: Boolean = false,
    val error: UiText? = null,
    /** The address the household refused, in the rejected state. */
    val rejectedEmail: String = "",
)

sealed interface AuthAction {
    data class OnEmailChange(val email: String) : AuthAction
    data class OnPasswordChange(val password: String) : AuthAction
    data class OnModeChange(val mode: AuthMode) : AuthAction
    data object OnSubmit : AuthAction
    data object OnSignOut : AuthAction
    data object OnChangeServer : AuthAction
}

sealed interface AuthEvent {
    /** A session exists now, or stopped existing, so the gate decides what comes next. */
    data object GoToGate : AuthEvent
    data object GoToServer : AuthEvent
}

fun AuthError.toUiText(): UiText = UiText.StringResource(
    when (this) {
        AuthError.EMAIL_REQUIRED -> R.string.auth_error_email_required
        AuthError.PASSWORD_TOO_SHORT -> R.string.auth_error_password_too_short
        AuthError.INVALID_CREDENTIALS -> R.string.auth_error_invalid_credentials
        AuthError.USER_ALREADY_EXISTS -> R.string.auth_error_user_already_exists
        AuthError.WEAK_PASSWORD -> R.string.auth_error_weak_password
        AuthError.EMAIL_INVALID -> R.string.auth_error_email_invalid
        AuthError.EMAIL_NOT_CONFIRMED -> R.string.auth_error_email_not_confirmed
        AuthError.RATE_LIMITED -> R.string.auth_error_rate_limited
        AuthError.SIGNUP_DISABLED -> R.string.auth_error_signup_disabled
        AuthError.SESSION_EXPIRED -> R.string.auth_error_session_expired
        AuthError.CONFIRMATION_REQUIRED -> R.string.auth_error_confirmation_required
        AuthError.SERVER -> R.string.auth_error_server
        AuthError.NETWORK -> R.string.auth_error_network
        AuthError.GENERIC -> R.string.auth_error_generic
    },
)

class AuthViewModel(
    mode: AuthMode,
    private val auth: AuthRepository,
    private val sessions: SessionManager,
) : ViewModel() {
    private val _state = MutableStateFlow(
        AuthState(mode = mode, rejectedEmail = sessions.session.value?.email.orEmpty()),
    )
    val state = _state.asStateFlow()

    private val _events = Channel<AuthEvent>()
    val events = _events.receiveAsFlow()

    fun onAction(action: AuthAction) {
        when (action) {
            is AuthAction.OnEmailChange -> _state.update { it.copy(email = action.email, error = null) }
            is AuthAction.OnPasswordChange -> _state.update { it.copy(password = action.password, error = null) }
            is AuthAction.OnModeChange -> _state.update { it.copy(mode = action.mode, error = null) }
            AuthAction.OnSubmit -> submit()
            AuthAction.OnSignOut -> viewModelScope.launch {
                sessions.signOut()
                _events.send(AuthEvent.GoToGate)
            }
            AuthAction.OnChangeServer -> viewModelScope.launch { _events.send(AuthEvent.GoToServer) }
        }
    }

    private fun submit() {
        val current = _state.value
        if (current.isBusy) return

        val valid = CredentialRules.validate(current.email, current.password)
        if (valid is Result.Error) {
            _state.update { it.copy(error = valid.error.toUiText()) }
            return
        }

        _state.update { it.copy(isBusy = true, error = null) }
        viewModelScope.launch {
            val result = if (current.mode == AuthMode.SignUp) {
                auth.signUp(current.email, current.password)
            } else {
                auth.signIn(current.email, current.password)
            }
            when (result) {
                is Result.Error -> _state.update { it.copy(isBusy = false, error = result.error.toUiText()) }
                is Result.Success -> {
                    sessions.adopt(result.data)
                    _state.update { it.copy(isBusy = false, password = "") }
                    _events.send(AuthEvent.GoToGate)
                }
            }
        }
    }
}

@Composable
fun AuthRoot(
    mode: AuthMode,
    onGate: () -> Unit,
    onServer: () -> Unit,
    viewModel: AuthViewModel = koinViewModel { parametersOf(mode) },
) {
    val state by viewModel.state.collectAsStateWithLifecycle()

    ObserveAsEvents(viewModel.events) { event ->
        when (event) {
            AuthEvent.GoToGate -> onGate()
            AuthEvent.GoToServer -> onServer()
        }
    }

    AuthScreen(state = state, onAction = viewModel::onAction)
}

@Composable
fun AuthScreen(
    state: AuthState,
    onAction: (AuthAction) -> Unit,
) {
    if (state.mode == AuthMode.Rejected) {
        RejectedContent(state, onAction)
    } else {
        CredentialsContent(state, onAction)
    }
}

@Composable
private fun RejectedContent(state: AuthState, onAction: (AuthAction) -> Unit) {
    AuthScaffold(
        kicker = stringResource(R.string.no_access),
        title = stringResource(R.string.rejected),
        blurb = stringResource(R.string.rejected_help),
    ) {
        BasicText(stringResource(R.string.signed_in_as), style = KvType.label)
        KvCallout(state.rejectedEmail)
        KvButton(
            text = stringResource(R.string.sign_out_other),
            onClick = { onAction(AuthAction.OnSignOut) },
            glyph = PhosphorIcons.SignOut,
            fill = true,
        )
    }
}

@Composable
private fun CredentialsContent(state: AuthState, onAction: (AuthAction) -> Unit) {
    val signUp = state.mode == AuthMode.SignUp
    AuthScaffold(
        kicker = stringResource(if (signUp) R.string.sign_up else R.string.sign_in),
        title = stringResource(if (signUp) R.string.create_account else R.string.welcome_back),
        blurb = stringResource(if (signUp) R.string.sign_up_blurb else R.string.sign_in_blurb),
        foot = if (signUp) null else stringResource(R.string.sign_in_foot),
    ) {
        KvSegmented(
            options = listOf(stringResource(R.string.sign_in), stringResource(R.string.sign_up)),
            selected = if (signUp) 1 else 0,
            onSelect = { onAction(AuthAction.OnModeChange(if (it == 1) AuthMode.SignUp else AuthMode.SignIn)) },
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.email),
            value = state.email,
            onValueChange = { onAction(AuthAction.OnEmailChange(it)) },
            placeholder = stringResource(R.string.email_placeholder),
            keyboardOptions = KeyboardOptions(
                keyboardType = KeyboardType.Email,
                imeAction = ImeAction.Next,
                autoCorrectEnabled = false,
            ),
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.password),
            value = state.password,
            onValueChange = { onAction(AuthAction.OnPasswordChange(it)) },
            hint = stringResource(if (signUp) R.string.password_rule else R.string.password_hint),
            visualTransformation = PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Go),
            keyboardActions = KeyboardActions(onGo = { onAction(AuthAction.OnSubmit) }),
            modifier = Modifier.fillMaxWidth(),
        )
        state.error?.let { KvCallout(it.asString()) }
        KvButton(
            text = stringResource(if (signUp) R.string.sign_up else R.string.sign_in),
            onClick = { onAction(AuthAction.OnSubmit) },
            style = KvButtonStyle.Primary,
            busy = state.isBusy,
            fill = true,
        )
        KvButton(
            text = stringResource(R.string.change_server),
            onClick = { onAction(AuthAction.OnChangeServer) },
            style = KvButtonStyle.Ghost,
            fill = true,
        )
    }
}

@Preview
@Composable
private fun AuthScreenPreview() {
    KvitteriaTheme { AuthScreen(AuthState(email = "dig@husstand.dk"), onAction = {}) }
}

@Preview
@Composable
private fun RejectedPreview() {
    KvitteriaTheme {
        AuthScreen(AuthState(mode = AuthMode.Rejected, rejectedEmail = "dig@husstand.dk"), onAction = {})
    }
}
