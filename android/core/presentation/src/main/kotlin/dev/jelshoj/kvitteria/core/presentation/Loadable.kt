/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/**
 * The state every data screen has. No [data] and no [error] is loading, [error] with no [data] is the
 * error state, and a refresh that fails leaves [data] showing.
 */
data class Loadable<T>(
    val data: T? = null,
    val isLoading: Boolean = true,
    val error: UiText? = null,
) {
    val isRefreshing: Boolean get() = isLoading && data != null
}

/**
 * Loads one thing for a screen and keeps the rules in one place. A refresh supersedes the one before it,
 * and when it fails with content already showing, the content stays and [failures] reports the failure.
 */
class Loader<T>(
    private val scope: CoroutineScope,
    private val fetch: suspend () -> Result<T, ApiError>,
) {
    private val _state = MutableStateFlow(Loadable<T>())
    val state: StateFlow<Loadable<T>> = _state.asStateFlow()

    private val _failures = MutableSharedFlow<UiText>(extraBufferCapacity = 1)
    val failures: SharedFlow<UiText> = _failures.asSharedFlow()

    private var running: Job? = null

    fun refresh() {
        running?.cancel()
        _state.update { it.copy(isLoading = true) }
        running = scope.launch {
            when (val result = fetch()) {
                is Result.Success -> _state.value = Loadable(data = result.data, isLoading = false)
                is Result.Error -> {
                    val message = result.error.toUiText()
                    if (_state.value.data == null) {
                        _state.update { it.copy(isLoading = false, error = message) }
                    } else {
                        _state.update { it.copy(isLoading = false) }
                        _failures.tryEmit(message)
                    }
                }
            }
        }
    }

    /** Changes what's held without a request, for an optimistic edit or a save's own answer. */
    fun update(transform: (T) -> T) {
        _state.update { current -> current.copy(data = current.data?.let(transform)) }
    }
}
