/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.MutableState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSerializable
import androidx.compose.runtime.setValue
import androidx.lifecycle.viewmodel.navigation3.rememberViewModelStoreNavEntryDecorator
import androidx.navigation3.runtime.NavBackStack
import androidx.navigation3.runtime.NavEntry
import androidx.navigation3.runtime.NavKey
import androidx.navigation3.runtime.rememberDecoratedNavEntries
import androidx.navigation3.runtime.rememberNavBackStack
import androidx.navigation3.runtime.rememberSaveableStateHolderNavEntryDecorator
import androidx.navigation3.runtime.serialization.NavKeySerializer
import androidx.savedstate.compose.serialization.serializers.MutableStateSerializer

/**
 * One back stack per tab. Each keeps its own decorators, so a tab that isn't showing keeps its screen
 * state and its ViewModels. People leave the app through the first tab: a tab other than that one sits
 * on top of it, and back from its root goes there.
 */
class TabNavigation(
    val start: NavKey,
    current: MutableState<NavKey>,
    val stacks: Map<NavKey, NavBackStack<NavKey>>,
) {
    var current: NavKey by current

    fun selectTab(tab: NavKey) {
        current = tab
    }

    fun push(route: NavKey) {
        stacks.getValue(current).add(route)
    }

    /** Opens [route] on another tab, on top of whatever that tab already shows. */
    fun pushOn(tab: NavKey, route: NavKey) {
        current = tab
        stacks.getValue(tab).add(route)
    }

    fun back() {
        val stack = stacks.getValue(current)
        if (stack.size > 1) {
            stack.removeLastOrNull()
        } else {
            current = start
        }
    }

    @Composable
    fun decoratedEntries(entryProvider: (NavKey) -> NavEntry<NavKey>): List<NavEntry<NavKey>> {
        val decorated = stacks.mapValues { (_, stack) ->
            rememberDecoratedNavEntries(
                backStack = stack,
                entryDecorators = listOf(
                    rememberSaveableStateHolderNavEntryDecorator<NavKey>(),
                    rememberViewModelStoreNavEntryDecorator(),
                ),
                entryProvider = entryProvider,
            )
        }
        val inUse = if (current == start) listOf(start) else listOf(start, current)
        return inUse.flatMap { decorated[it].orEmpty() }
    }
}

@Composable
fun rememberTabNavigation(tabs: List<NavKey>): TabNavigation {
    val start = tabs.first()
    val current = rememberSerializable(serializer = MutableStateSerializer(NavKeySerializer())) {
        mutableStateOf(start)
    }
    val stacks = tabs.associateWith { rememberNavBackStack(it) }
    return remember { TabNavigation(start, current, stacks) }
}
