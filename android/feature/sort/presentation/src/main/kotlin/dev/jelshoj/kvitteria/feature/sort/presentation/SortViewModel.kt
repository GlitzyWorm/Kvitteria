/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.sort.presentation

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.WorkCounts
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.taxonomy.CanonicalItem
import dev.jelshoj.kvitteria.core.domain.taxonomy.Category
import dev.jelshoj.kvitteria.core.domain.taxonomy.Meal
import dev.jelshoj.kvitteria.core.domain.taxonomy.TaxonomyRepository
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.refreshIfForbidden
import dev.jelshoj.kvitteria.core.presentation.toUiText
import dev.jelshoj.kvitteria.feature.sort.domain.NameTidier
import dev.jelshoj.kvitteria.feature.sort.domain.SkippedLines
import dev.jelshoj.kvitteria.feature.sort.domain.SortRepository
import dev.jelshoj.kvitteria.feature.sort.domain.SortRequest
import dev.jelshoj.kvitteria.feature.sort.domain.Suggestion
import dev.jelshoj.kvitteria.feature.sort.domain.SuggestionRanker
import dev.jelshoj.kvitteria.feature.sort.domain.UnassignedLine
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/** What a save will send. An [itemId] of null with [isNew] means a new item called [name]. */
data class SortDraft(
    val itemId: String?,
    val name: String,
    val isNew: Boolean,
    val categoryId: String? = null,
    val mealId: String? = null,
    val rememberAlias: Boolean = true,
    val setDefaults: Boolean = false,
)

data class NewCategoryDraft(
    val name: String = "",
    val parentId: String? = null,
    val pickingParent: Boolean = false,
    val isBusy: Boolean = false,
    val error: UiText? = null,
)

enum class SortPicker { Category, Meal }

data class SortState(
    val isLoading: Boolean = true,
    val error: UiText? = null,
    val queue: List<UnassignedLine> = emptyList(),
    val skippedCount: Int = 0,
    val doneCount: Int = 0,
    val items: List<CanonicalItem> = emptyList(),
    val categories: List<Category> = emptyList(),
    val meals: List<Meal> = emptyList(),
    val itemSearch: String = "",
    val draft: SortDraft? = null,
    val picking: SortPicker? = null,
    val newCategory: NewCategoryDraft? = null,
    val learned: Int? = null,
    val isSaving: Boolean = false,
    val canCategorize: Boolean = false,
    val canManageTaxonomy: Boolean = false,
) {
    val current: UnassignedLine? get() = queue.firstOrNull()
    val upNext: List<UnassignedLine> get() = queue.drop(1).take(UP_NEXT)
    val suggestions: List<Suggestion> get() = current?.let { SuggestionRanker.rank(it.rawName, items) }.orEmpty()
    val searchHits: List<CanonicalItem> get() = SuggestionRanker.search(itemSearch, items)
    val total: Int get() = doneCount + queue.size + skippedCount

    private companion object {
        const val UP_NEXT = 4
    }
}

sealed interface SortAction {
    data object OnRefresh : SortAction
    data class OnSearchChange(val term: String) : SortAction
    data class OnPickItem(val item: CanonicalItem) : SortAction
    data object OnCreateItem : SortAction
    data class OnNewNameChange(val name: String) : SortAction
    data class OnOpenPicker(val picker: SortPicker?) : SortAction
    data class OnPickCategory(val categoryId: String?) : SortAction
    data class OnPickMeal(val mealId: String?) : SortAction
    data class OnRememberAlias(val on: Boolean) : SortAction
    data class OnSetDefaults(val on: Boolean) : SortAction
    data object OnSave : SortAction
    data object OnSkip : SortAction
    data object OnShowSkipped : SortAction
    data object OnNewCategory : SortAction
    data class OnNewCategoryChange(val draft: NewCategoryDraft?) : SortAction
    data object OnSaveNewCategory : SortAction
}

class SortViewModel(
    private val sorting: SortRepository,
    private val skipped: SkippedLines,
    private val taxonomy: TaxonomyRepository,
    private val counts: WorkCounts,
    private val members: MemberRepository,
    sessions: SessionManager,
) : ViewModel() {
    private val _state = MutableStateFlow(SortState())
    val state = _state.asStateFlow()

    private val _messages = Channel<UiText>(Channel.BUFFERED)
    val messages = _messages.receiveAsFlow()

    init {
        viewModelScope.launch {
            sessions.member.collect { member ->
                _state.update {
                    it.copy(
                        canCategorize = member?.can(Permission.CategorizeLines) == true,
                        canManageTaxonomy = member?.can(Permission.ManageTaxonomy) == true,
                    )
                }
            }
        }
    }

    fun refresh() {
        viewModelScope.launch {
            loadQueue()
            val items = (taxonomy.items() as? Result.Success)?.data
            val categories = (taxonomy.categories() as? Result.Success)?.data
            val meals = (taxonomy.meals() as? Result.Success)?.data
            _state.update {
                it.copy(
                    items = items ?: it.items,
                    categories = categories ?: it.categories,
                    meals = meals ?: it.meals,
                )
            }
        }
    }

    private suspend fun loadQueue() {
        when (val result = sorting.queue()) {
            is Result.Success -> {
                val skippedIds = skipped.ids
                val queue = result.data.filter { it.id !in skippedIds }
                _state.update { state ->
                    val sameLine = state.current?.id == queue.firstOrNull()?.id
                    state.copy(
                        queue = queue,
                        skippedCount = result.data.size - queue.size,
                        isLoading = false,
                        error = null,
                        draft = state.draft.takeIf { sameLine },
                    )
                }
            }
            is Result.Error -> {
                val message = result.error.toUiText()
                val hadContent = _state.value.queue.isNotEmpty()
                _state.update { it.copy(isLoading = false, error = message.takeUnless { hadContent }) }
                if (hadContent) _messages.send(message)
            }
        }
    }

    fun onAction(action: SortAction) {
        when (action) {
            SortAction.OnRefresh -> refresh()
            is SortAction.OnSearchChange -> _state.update { it.copy(itemSearch = action.term) }
            is SortAction.OnPickItem -> _state.update {
                it.copy(
                    draft = SortDraft(
                        itemId = action.item.id,
                        name = action.item.name,
                        isNew = false,
                        categoryId = action.item.categoryId,
                        mealId = action.item.defaultMealId,
                    ),
                )
            }
            SortAction.OnCreateItem -> _state.update { state ->
                val line = state.current ?: return@update state
                state.copy(draft = SortDraft(null, NameTidier.tidy(line.rawName), isNew = true, setDefaults = true))
            }
            is SortAction.OnNewNameChange -> updateDraft { it.copy(name = action.name) }
            is SortAction.OnOpenPicker -> _state.update { it.copy(picking = action.picker) }
            is SortAction.OnPickCategory -> {
                updateDraft { it.copy(categoryId = action.categoryId) }
                _state.update { it.copy(picking = null) }
            }
            is SortAction.OnPickMeal -> {
                updateDraft { it.copy(mealId = action.mealId) }
                _state.update { it.copy(picking = null) }
            }
            is SortAction.OnRememberAlias -> updateDraft { it.copy(rememberAlias = action.on) }
            is SortAction.OnSetDefaults -> updateDraft { it.copy(setDefaults = action.on) }
            SortAction.OnSave -> save()
            SortAction.OnSkip -> skip()
            SortAction.OnShowSkipped -> {
                skipped.clear()
                viewModelScope.launch { loadQueue() }
            }
            SortAction.OnNewCategory -> _state.update { it.copy(picking = null, newCategory = NewCategoryDraft()) }
            is SortAction.OnNewCategoryChange -> _state.update { it.copy(newCategory = action.draft) }
            SortAction.OnSaveNewCategory -> saveCategory()
        }
    }

    private fun updateDraft(transform: (SortDraft) -> SortDraft) {
        _state.update { state -> state.copy(draft = state.draft?.let(transform)) }
    }

    private fun skip() {
        val line = _state.value.current ?: return
        skipped.skip(line.id)
        _state.update {
            it.copy(
                queue = it.queue.drop(1),
                skippedCount = it.skippedCount + 1,
                draft = null,
                learned = null,
                itemSearch = "",
            )
        }
    }

    private fun save() {
        val current = _state.value
        val line = current.current ?: return
        val draft = current.draft ?: return
        if (current.isSaving || !current.canCategorize) return
        if (draft.isNew && draft.name.isBlank()) return

        _state.update { it.copy(isSaving = true) }
        viewModelScope.launch {
            val request = SortRequest(
                canonicalItemId = draft.itemId,
                newItemName = draft.name.trim().takeIf { draft.isNew },
                categoryId = draft.categoryId,
                mealId = draft.mealId,
                rememberAlias = draft.rememberAlias,
                setAsDefaults = draft.setDefaults,
            )
            when (val result = sorting.sort(line.id, request)) {
                is Result.Success -> {
                    counts.setUnassignedLines(result.data.remainingCount)
                    if (draft.isNew || draft.setDefaults) taxonomy.invalidateItems()
                    _state.update {
                        it.copy(
                            queue = it.queue.drop(1),
                            doneCount = it.doneCount + 1 + result.data.otherLinesAssigned,
                            draft = null,
                            itemSearch = "",
                            isSaving = false,
                            learned = result.data.otherLinesAssigned.takeIf { assigned -> assigned > 0 },
                        )
                    }
                    _messages.send(UiText.StringResource(R.string.saved_item, listOf(draft.name)))
                    // Lines the alias just taught have left the queue on the server.
                    if (result.data.otherLinesAssigned > 0 || draft.isNew) refresh()
                }
                is Result.Error -> {
                    _state.update { it.copy(isSaving = false) }
                    if (result.error.kind == DataError.Network.CONFLICT) {
                        // Someone sorted this line meanwhile, or the new item's name is taken. Only the
                        // first takes the line out of the queue, so the queue itself tells them apart.
                        loadQueue()
                        val stillHere = _state.value.current?.id == line.id
                        if (stillHere) _messages.send(result.error.toUiText())
                    } else {
                        _messages.send(result.error.toUiText())
                        members.refreshIfForbidden(result.error)
                    }
                }
            }
        }
    }

    private fun saveCategory() {
        val draft = _state.value.newCategory ?: return
        if (draft.isBusy || draft.name.isBlank()) return

        _state.update { it.copy(newCategory = draft.copy(isBusy = true, error = null)) }
        viewModelScope.launch {
            when (val result = taxonomy.createCategory(draft.name.trim(), draft.parentId)) {
                is Result.Error -> {
                    _state.update {
                        it.copy(newCategory = it.newCategory?.copy(isBusy = false, error = result.error.toUiText()))
                    }
                    members.refreshIfForbidden(result.error)
                }
                is Result.Success -> {
                    val categories = (taxonomy.categories(force = true) as? Result.Success)?.data
                    updateDraft { it.copy(categoryId = result.data.id) }
                    _state.update { it.copy(newCategory = null, categories = categories ?: it.categories) }
                }
            }
        }
    }
}
