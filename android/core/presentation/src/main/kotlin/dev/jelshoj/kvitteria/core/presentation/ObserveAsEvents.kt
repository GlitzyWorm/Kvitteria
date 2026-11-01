/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.rememberUpdatedState
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.repeatOnLifecycle
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.withContext

/** Collects one-time events while the screen is started, on the immediate dispatcher so none is lost. */
@Composable
fun <T> ObserveAsEvents(flow: Flow<T>, onEvent: suspend (T) -> Unit) {
    val lifecycleOwner = LocalLifecycleOwner.current
    val latest = rememberUpdatedState(onEvent)
    LaunchedEffect(flow, lifecycleOwner) {
        lifecycleOwner.lifecycle.repeatOnLifecycle(Lifecycle.State.STARTED) {
            withContext(Dispatchers.Main.immediate) { flow.collect { latest.value(it) } }
        }
    }
}

/** Runs [onStart] each time the screen comes to the foreground, which is how data screens reload. */
@Composable
fun OnStart(onStart: () -> Unit) {
    val lifecycleOwner = LocalLifecycleOwner.current
    val latest = rememberUpdatedState(onStart)
    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_START) latest.value()
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }
}
