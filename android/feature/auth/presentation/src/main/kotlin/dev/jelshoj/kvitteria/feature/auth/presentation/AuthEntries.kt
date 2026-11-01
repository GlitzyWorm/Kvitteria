/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.presentation

import androidx.navigation3.runtime.EntryProviderScope
import androidx.navigation3.runtime.NavKey
import dev.jelshoj.kvitteria.feature.auth.presentation.auth.AuthRoot
import dev.jelshoj.kvitteria.feature.auth.presentation.auth.AuthViewModel
import dev.jelshoj.kvitteria.feature.auth.presentation.claim.ClaimScreen
import dev.jelshoj.kvitteria.feature.auth.presentation.gate.GateRoot
import dev.jelshoj.kvitteria.feature.auth.presentation.gate.GateViewModel
import dev.jelshoj.kvitteria.feature.auth.presentation.server.ServerRoot
import dev.jelshoj.kvitteria.feature.auth.presentation.server.ServerViewModel
import kotlinx.serialization.Serializable
import org.koin.core.module.dsl.viewModel
import org.koin.core.module.dsl.viewModelOf
import org.koin.dsl.module

@Serializable
enum class AuthMode { SignIn, SignUp, Rejected }

@Serializable data object GateRoute : NavKey

@Serializable data object ServerRoute : NavKey

@Serializable data object ClaimRoute : NavKey

@Serializable data class AuthRoute(val mode: AuthMode) : NavKey

/**
 * These are states of the application rather than sections of it, so moving between them replaces the
 * whole stack through [onReplace], and back can't reach a signed-in screen after sign-out. Server entry
 * pushes through [onPush], because it can be cancelled.
 */
fun EntryProviderScope<NavKey>.authEntries(
    onReplace: (NavKey) -> Unit,
    onPush: (NavKey) -> Unit,
    onMain: () -> Unit,
) {
    entry<GateRoute> {
        GateRoot(
            onServer = { onReplace(ServerRoute) },
            onClaim = { onReplace(ClaimRoute) },
            onSignIn = { onReplace(AuthRoute(AuthMode.SignIn)) },
            onMain = onMain,
            onRejected = { onReplace(AuthRoute(AuthMode.Rejected)) },
        )
    }
    entry<ServerRoute> {
        ServerRoot(onConnected = { onReplace(GateRoute) })
    }
    entry<ClaimRoute> {
        ClaimScreen(
            onContinue = { onReplace(AuthRoute(AuthMode.SignUp)) },
            onChangeServer = { onPush(ServerRoute) },
        )
    }
    entry<AuthRoute> { route ->
        AuthRoot(
            mode = route.mode,
            onGate = { onReplace(GateRoute) },
            onServer = { onPush(ServerRoute) },
        )
    }
}

val authPresentationModule = module {
    viewModelOf(::GateViewModel)
    viewModelOf(::ServerViewModel)
    viewModel { params -> AuthViewModel(params.get(), get(), get()) }
}
