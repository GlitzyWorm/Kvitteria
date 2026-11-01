/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.navigation

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Modifier
import androidx.lifecycle.viewmodel.navigation3.rememberViewModelStoreNavEntryDecorator
import androidx.navigation3.runtime.NavBackStack
import androidx.navigation3.runtime.NavKey
import androidx.navigation3.runtime.entryProvider
import androidx.navigation3.runtime.rememberNavBackStack
import androidx.navigation3.runtime.rememberSaveableStateHolderNavEntryDecorator
import androidx.navigation3.ui.NavDisplay
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.feature.auth.presentation.AuthRoute
import dev.jelshoj.kvitteria.feature.auth.presentation.ClaimRoute
import dev.jelshoj.kvitteria.feature.auth.presentation.GateRoute
import dev.jelshoj.kvitteria.feature.auth.presentation.ServerRoute
import dev.jelshoj.kvitteria.feature.auth.presentation.authEntries
import dev.jelshoj.kvitteria.feature.settings.presentation.SettingsRoute
import dev.jelshoj.kvitteria.feature.settings.presentation.settingsEntries
import kotlinx.coroutines.flow.drop
import kotlinx.serialization.Serializable
import org.koin.compose.koinInject

/** The tabs. They live inside one root entry, so Settings above them has no tab bar. */
@Serializable data object MainRoute : NavKey

private fun NavBackStack<NavKey>.replaceAll(route: NavKey) {
    add(route)
    while (size > 1) removeAt(0)
}

private fun NavKey.isSignedOutRoute() =
    this == GateRoute || this == ServerRoute || this == ClaimRoute || this is AuthRoute

/**
 * The application's states. Moving between them replaces the whole stack, so back can't reach a
 * signed-in screen after sign-out, and the root is what notices a session ending: no screen handles it.
 */
@Composable
fun KvitteriaRoot(sessions: SessionManager = koinInject()) {
    val backStack = rememberNavBackStack(GateRoute)

    LaunchedEffect(Unit) {
        // Restored after process death with nobody resolved. Only the gate can answer that.
        if (sessions.member.value == null && !backStack.last().isSignedOutRoute()) backStack.replaceAll(GateRoute)
        sessions.session.drop(1).collect { session ->
            if (session == null && !backStack.last().isSignedOutRoute()) backStack.replaceAll(GateRoute)
        }
    }

    NavDisplay(
        backStack = backStack,
        modifier = Modifier.fillMaxSize().background(KvColors.bg),
        onBack = { if (backStack.size > 1) backStack.removeLastOrNull() },
        entryDecorators = listOf(
            rememberSaveableStateHolderNavEntryDecorator(),
            rememberViewModelStoreNavEntryDecorator(),
        ),
        entryProvider = entryProvider {
            authEntries(
                onReplace = backStack::replaceAll,
                onPush = { backStack.add(it) },
                onMain = { backStack.replaceAll(MainRoute) },
            )
            entry<MainRoute> { MainShell(onOpenSettings = { backStack.add(SettingsRoute) }) }
            settingsEntries(
                onPush = { backStack.add(it) },
                onBack = { backStack.removeLastOrNull() },
                onChangeServer = { backStack.add(ServerRoute) },
            )
        },
    )
}
