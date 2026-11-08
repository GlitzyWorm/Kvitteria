/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.receipts.presentation

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.receipt.Page
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptRepository
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.toUiText
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.drop
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class ReceiptsState(
    val search: String = "",
    val unreconciledOnly: Boolean = false,
    val receipts: List<Receipt> = emptyList(),
    val isLoading: Boolean = true,
    val isRefreshing: Boolean = false,
    val isLoadingMore: Boolean = false,
    val hasMore: Boolean = false,
    val error: UiText? = null,
)

sealed interface ReceiptsAction {
    data class OnSearchChange(val search: String) : ReceiptsAction
    data class OnFilterChange(val unreconciledOnly: Boolean) : ReceiptsAction
    data object OnRefresh : ReceiptsAction
    data object OnLoadMore : ReceiptsAction
}

private const val PAGE_SIZE = 30
private const val SEARCH_DEBOUNCE_MS = 300L

@OptIn(FlowPreview::class, ExperimentalCoroutinesApi::class)
class ReceiptsViewModel(private val repository: ReceiptRepository) : ViewModel() {
    private val _state = MutableStateFlow(ReceiptsState())
    val state = _state.asStateFlow()

    private val _messages = Channel<UiText>(Channel.BUFFERED)
    val messages = _messages.receiveAsFlow()

    private val search = MutableStateFlow("")
    private val unreconciledOnly = MutableStateFlow(false)
    private val refreshes = MutableStateFlow(0)
    private var page = 1
    private var loadingMore: Job? = null

    init {
        // A pause in typing sends one request, and flatMapLatest is what discards an older search that
        // answers after a newer one.
        val settledSearch = merge(flowOf(""), search.drop(1).debounce(SEARCH_DEBOUNCE_MS))
        viewModelScope.launch {
            combine(settledSearch, unreconciledOnly, refreshes) { term, filter, _ -> term to filter }
                .flatMapLatest { (term, filter) ->
                    flow {
                        loadingMore?.cancel()
                        _state.update { it.copy(isLoading = it.receipts.isEmpty(), error = null) }
                        emit(repository.receipts(1, PAGE_SIZE, term, filter))
                    }
                }
                .collect(::onFirstPage)
        }
        viewModelScope.launch {
            repository.deleted.collect { id ->
                _state.update { state -> state.copy(receipts = state.receipts.filterNot { it.id == id }) }
                refreshes.update { it + 1 }
            }
        }
    }

    private suspend fun onFirstPage(result: Result<Page<Receipt>, dev.jelshoj.kvitteria.core.domain.ApiError>) {
        when (result) {
            is Result.Success -> {
                page = 1
                _state.update {
                    it.copy(
                        receipts = result.data.items,
                        hasMore = result.data.hasMore,
                        isLoading = false,
                        isRefreshing = false,
                        error = null,
                    )
                }
            }
            is Result.Error -> {
                val message = result.error.toUiText()
                val hadContent = _state.value.receipts.isNotEmpty()
                _state.update {
                    it.copy(isLoading = false, isRefreshing = false, error = message.takeUnless { hadContent })
                }
                if (hadContent) _messages.send(message)
            }
        }
    }

    fun onAction(action: ReceiptsAction) {
        when (action) {
            is ReceiptsAction.OnSearchChange -> {
                search.value = action.search
                _state.update { it.copy(search = action.search) }
            }
            is ReceiptsAction.OnFilterChange -> {
                unreconciledOnly.value = action.unreconciledOnly
                _state.update { it.copy(unreconciledOnly = action.unreconciledOnly, receipts = emptyList()) }
            }
            ReceiptsAction.OnRefresh -> refresh(showIndicator = true)
            ReceiptsAction.OnLoadMore -> loadMore()
        }
    }

    /** Reloads when the screen comes back, without the pull indicator. */
    fun refresh(showIndicator: Boolean = false) {
        if (showIndicator) _state.update { it.copy(isRefreshing = true) }
        refreshes.update { it + 1 }
    }

    private fun loadMore() {
        val current = _state.value
        if (current.isLoadingMore || !current.hasMore || current.isLoading) return

        _state.update { it.copy(isLoadingMore = true) }
        loadingMore = viewModelScope.launch {
            val next = page + 1
            when (val result = repository.receipts(next, PAGE_SIZE, search.value, unreconciledOnly.value)) {
                is Result.Success -> {
                    page = next
                    _state.update {
                        it.copy(
                            receipts = (it.receipts + result.data.items).distinctBy { receipt -> receipt.id },
                            hasMore = result.data.hasMore,
                            isLoadingMore = false,
                        )
                    }
                }
                is Result.Error -> {
                    _state.update { it.copy(isLoadingMore = false) }
                    _messages.send(result.error.toUiText())
                }
            }
        }
        loadingMore?.invokeOnCompletion { _state.update { state -> state.copy(isLoadingMore = false) } }
    }
}
