/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.receipts.presentation

import dev.jelshoj.kvitteria.core.presentation.detailOrUiText
import dev.jelshoj.kvitteria.core.domain.DataError
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.receipt.LineCorrection
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptLine
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptRepository
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.taxonomy.Category
import dev.jelshoj.kvitteria.core.domain.taxonomy.Meal
import dev.jelshoj.kvitteria.core.domain.taxonomy.TaxonomyRepository
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.refreshIfForbidden
import dev.jelshoj.kvitteria.core.presentation.toUiText
import java.math.BigDecimal
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

enum class LinePicker { Category, Meal }

/** The line editor. Figures are text, because a half-typed number isn't one. */
data class LineEditor(
    val lineId: String,
    val rawName: String,
    val quantity: String,
    val unitPrice: String,
    val lineTotal: String,
    val quantityInvalid: Boolean = false,
    val unitPriceInvalid: Boolean = false,
    val lineTotalInvalid: Boolean = false,
    val isBusy: Boolean = false,
    val error: UiText? = null,
)

/** The receipt's image, fetched only for a receipt that has one. */
sealed interface ReceiptImage {
    data object None : ReceiptImage
    data object Loading : ReceiptImage
    class Loaded(val bytes: ByteArray) : ReceiptImage
    data object Failed : ReceiptImage
}

data class ReceiptDetailState(
    val receipt: Receipt? = null,
    val isLoading: Boolean = true,
    val isRefreshing: Boolean = false,
    val error: UiText? = null,
    val categories: List<Category> = emptyList(),
    val meals: List<Meal> = emptyList(),
    /** What the steppers show, which leads the server by up to the debounce. */
    val coverage: Map<String, Int> = emptyMap(),
    val picking: Pair<String, LinePicker>? = null,
    val editor: LineEditor? = null,
    val canCategorize: Boolean = false,
    val canEdit: Boolean = false,
    val image: ReceiptImage = ReceiptImage.None,
    val isViewerOpen: Boolean = false,
    val isConfirmingDelete: Boolean = false,
    val isDeleting: Boolean = false,
) {
    /** Deleting is correction work, so it goes with the permission to correct figures. */
    val canDelete: Boolean get() = canEdit

    /** Tracking meals that appear on a line get a stepper. */
    val coverageMeals: List<Meal>
        get() = meals.filter { meal -> meal.tracksDayCoverage && receipt?.lines?.any { it.mealId == meal.id } == true }

    /** Tracking meals with no line can't be covered, and say so. */
    val blockedMeals: List<Meal>
        get() = meals.filter { it.tracksDayCoverage && it !in coverageMeals }

    fun spendOn(mealId: String): BigDecimal =
        receipt?.lines.orEmpty().filter { it.mealId == mealId }.fold(BigDecimal.ZERO) { sum, line -> sum + line.lineTotal }
}

sealed interface ReceiptDetailAction {
    data object OnRefresh : ReceiptDetailAction
    data class OnOpenPicker(val lineId: String, val picker: LinePicker) : ReceiptDetailAction
    data object OnDismissPicker : ReceiptDetailAction
    data class OnPickCategory(val categoryId: String?) : ReceiptDetailAction
    data class OnPickMeal(val mealId: String?) : ReceiptDetailAction
    data class OnStepCoverage(val mealId: String, val delta: Int) : ReceiptDetailAction
    data class OnOpenEditor(val line: ReceiptLine) : ReceiptDetailAction
    data class OnEditorChange(val editor: LineEditor) : ReceiptDetailAction
    data object OnDismissEditor : ReceiptDetailAction
    data object OnSaveEditor : ReceiptDetailAction
    data object OnRetryImage : ReceiptDetailAction
    data object OnOpenImage : ReceiptDetailAction
    data object OnCloseImage : ReceiptDetailAction
    data object OnAskDelete : ReceiptDetailAction
    data object OnDismissDelete : ReceiptDetailAction
    data object OnConfirmDelete : ReceiptDetailAction
    data object OnOpenDuplicate : ReceiptDetailAction
}

sealed interface ReceiptDetailEvent {
    /** Deleted, or found already gone: back to the list, which has dropped it. */
    data object Close : ReceiptDetailEvent
    data class OpenReceipt(val id: String) : ReceiptDetailEvent
}

private const val COVERAGE_DEBOUNCE_MS = 400L
private const val MAX_DAYS = 31

class ReceiptDetailViewModel(
    private val receiptId: String,
    private val receipts: ReceiptRepository,
    private val taxonomy: TaxonomyRepository,
    private val members: MemberRepository,
    private val sessions: SessionManager,
) : ViewModel() {
    private val _state = MutableStateFlow(
        // Paints at once from the list's copy, then the refresh replaces it.
        receipts.peek(receiptId).let { cached ->
            ReceiptDetailState(receipt = cached, isLoading = cached == null, coverage = cached.days())
        },
    )
    val state = _state.asStateFlow()

    private val _messages = Channel<UiText>(Channel.BUFFERED)
    val messages = _messages.receiveAsFlow()

    private val _events = Channel<ReceiptDetailEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    private var coverageSave: Job? = null

    init {
        viewModelScope.launch {
            sessions.member.collect { member ->
                _state.update {
                    it.copy(
                        canCategorize = member?.can(Permission.CategorizeLines) == true,
                        canEdit = member?.can(Permission.EditReceipts) == true,
                    )
                }
            }
        }
        viewModelScope.launch {
            val categories = (taxonomy.categories() as? Result.Success)?.data.orEmpty()
            val meals = (taxonomy.meals() as? Result.Success)?.data.orEmpty()
            _state.update { it.copy(categories = categories, meals = meals) }
        }
        loadImageIfNeeded()
    }

    /** Once per receipt: the image never changes, so a refresh doesn't fetch it again. */
    private fun loadImageIfNeeded() {
        val state = _state.value
        if (state.receipt?.hasImage != true || state.image is ReceiptImage.Loaded || state.image is ReceiptImage.Loading) return
        _state.update { it.copy(image = ReceiptImage.Loading) }
        viewModelScope.launch {
            val image = when (val result = receipts.image(receiptId)) {
                is Result.Success -> ReceiptImage.Loaded(result.data)
                is Result.Error -> ReceiptImage.Failed
            }
            _state.update { it.copy(image = image) }
        }
    }

    private fun delete() {
        if (_state.value.isDeleting) return
        _state.update { it.copy(isDeleting = true) }
        viewModelScope.launch {
            val result = receipts.delete(receiptId)
            _state.update { it.copy(isDeleting = false, isConfirmingDelete = false) }
            when (result) {
                is Result.Success -> _events.send(ReceiptDetailEvent.Close)
                is Result.Error -> {
                    _messages.send(result.error.detailOrUiText())
                    // Already gone is the outcome that was asked for: the list has dropped it and reloads.
                    if (result.error.kind == DataError.Network.NOT_FOUND) {
                        _events.send(ReceiptDetailEvent.Close)
                    } else {
                        members.refreshIfForbidden(result.error)
                    }
                }
            }
        }
    }

    private fun Receipt?.days(): Map<String, Int> = this?.coverage.orEmpty().associate { it.mealId to it.daysCovered }

    fun refresh() {
        // A stepper still waiting to save would otherwise be overwritten by what the server holds.
        if (coverageSave?.isActive == true) return
        viewModelScope.launch {
            when (val result = receipts.receipt(receiptId)) {
                is Result.Success -> {
                    _state.update {
                        it.copy(
                            receipt = result.data,
                            coverage = result.data.days(),
                            isLoading = false,
                            isRefreshing = false,
                            error = null,
                        )
                    }
                    loadImageIfNeeded()
                }
                is Result.Error -> {
                    val message = result.error.toUiText()
                    val hadContent = _state.value.receipt != null
                    _state.update {
                        it.copy(isLoading = false, isRefreshing = false, error = message.takeUnless { hadContent })
                    }
                    if (hadContent) _messages.send(message)
                }
            }
        }
    }

    fun onAction(action: ReceiptDetailAction) {
        when (action) {
            ReceiptDetailAction.OnRefresh -> {
                _state.update { it.copy(isRefreshing = true) }
                refresh()
            }
            is ReceiptDetailAction.OnOpenPicker -> _state.update { it.copy(picking = action.lineId to action.picker) }
            ReceiptDetailAction.OnDismissPicker -> _state.update { it.copy(picking = null) }
            is ReceiptDetailAction.OnPickCategory -> pickCategory(action.categoryId)
            is ReceiptDetailAction.OnPickMeal -> pickMeal(action.mealId)
            is ReceiptDetailAction.OnStepCoverage -> stepCoverage(action.mealId, action.delta)
            is ReceiptDetailAction.OnOpenEditor -> _state.update {
                it.copy(
                    editor = LineEditor(
                        lineId = action.line.id,
                        rawName = action.line.rawName,
                        quantity = KrFormat.quantity(action.line.quantity),
                        unitPrice = KrFormat.editable(action.line.unitPrice),
                        lineTotal = KrFormat.editable(action.line.lineTotal),
                    ),
                )
            }
            is ReceiptDetailAction.OnEditorChange -> _state.update { it.copy(editor = action.editor.copy(error = null)) }
            ReceiptDetailAction.OnDismissEditor -> _state.update { it.copy(editor = null) }
            ReceiptDetailAction.OnSaveEditor -> saveEditor()
            ReceiptDetailAction.OnRetryImage -> {
                _state.update { it.copy(image = ReceiptImage.None) }
                loadImageIfNeeded()
            }
            ReceiptDetailAction.OnOpenImage -> _state.update { it.copy(isViewerOpen = it.image is ReceiptImage.Loaded) }
            ReceiptDetailAction.OnCloseImage -> _state.update { it.copy(isViewerOpen = false) }
            ReceiptDetailAction.OnAskDelete -> _state.update { it.copy(isConfirmingDelete = it.canDelete) }
            ReceiptDetailAction.OnDismissDelete -> _state.update { it.copy(isConfirmingDelete = false) }
            ReceiptDetailAction.OnConfirmDelete -> delete()
            ReceiptDetailAction.OnOpenDuplicate -> _state.value.receipt?.possibleDuplicateOfId?.let {
                _events.trySend(ReceiptDetailEvent.OpenReceipt(it))
            }
        }
    }

    private fun replaceLine(id: String, transform: (ReceiptLine) -> ReceiptLine) {
        _state.update { state ->
            state.copy(
                receipt = state.receipt?.let { receipt ->
                    receipt.copy(lines = receipt.lines.map { if (it.id == id) transform(it) else it })
                },
            )
        }
    }

    private suspend fun fail(error: ApiError) {
        _messages.send(error.toUiText())
        members.refreshIfForbidden(error)
    }

    private fun pickCategory(categoryId: String?) {
        val lineId = _state.value.picking?.first ?: return
        val before = _state.value.receipt?.lines?.firstOrNull { it.id == lineId } ?: return
        val name = _state.value.categories.flatMap { listOf(it) + it.children }.firstOrNull { it.id == categoryId }?.name

        _state.update { it.copy(picking = null) }
        replaceLine(lineId) { it.copy(categoryId = categoryId, categoryName = name) }
        viewModelScope.launch {
            when (val result = receipts.setLineCategory(lineId, categoryId)) {
                is Result.Success -> replaceLine(lineId) { result.data }
                is Result.Error -> {
                    replaceLine(lineId) { before }
                    fail(result.error)
                }
            }
        }
    }

    private fun pickMeal(mealId: String?) {
        val lineId = _state.value.picking?.first ?: return
        val before = _state.value.receipt?.lines?.firstOrNull { it.id == lineId } ?: return
        val name = _state.value.meals.firstOrNull { it.id == mealId }?.name

        _state.update { it.copy(picking = null) }
        replaceLine(lineId) { it.copy(mealId = mealId, mealName = name) }
        viewModelScope.launch {
            when (val result = receipts.setLineMeal(lineId, mealId)) {
                is Result.Success -> {
                    replaceLine(lineId) { result.data }
                    // The last line carrying a meal took its coverage with it on the server.
                    val lost = before.mealId != null && before.mealId != mealId &&
                        _state.value.receipt?.lines?.none { it.mealId == before.mealId } == true
                    if (lost) refresh()
                }
                is Result.Error -> {
                    replaceLine(lineId) { before }
                    fail(result.error)
                }
            }
        }
    }

    /** The endpoint replaces the receipt's whole coverage, so a burst of taps ends in one full set. */
    private fun stepCoverage(mealId: String, delta: Int) {
        _state.update { state ->
            val days = ((state.coverage[mealId] ?: 0) + delta).coerceIn(0, MAX_DAYS)
            state.copy(coverage = state.coverage + (mealId to days))
        }
        coverageSave?.cancel()
        coverageSave = viewModelScope.launch {
            delay(COVERAGE_DEBOUNCE_MS)
            val days = _state.value.coverage.filterValues { it > 0 }
            val result = receipts.setCoverage(receiptId, days)
            coverageSave = null
            if (result is Result.Error) fail(result.error)
            refresh()
        }
    }

    private fun saveEditor() {
        val editor = _state.value.editor ?: return
        if (editor.isBusy) return

        val quantity = KrFormat.parse(editor.quantity)
        val unitPrice = KrFormat.parse(editor.unitPrice)
        val lineTotal = KrFormat.parse(editor.lineTotal)
        if (quantity == null || unitPrice == null || lineTotal == null) {
            _state.update {
                it.copy(
                    editor = editor.copy(
                        quantityInvalid = quantity == null,
                        unitPriceInvalid = unitPrice == null,
                        lineTotalInvalid = lineTotal == null,
                    ),
                )
            }
            return
        }

        _state.update { it.copy(editor = editor.copy(isBusy = true)) }
        viewModelScope.launch {
            val correction = LineCorrection(editor.rawName.trim(), quantity, unitPrice, lineTotal)
            when (val result = receipts.correctLine(editor.lineId, correction)) {
                is Result.Success -> {
                    replaceLine(editor.lineId) { result.data }
                    _state.update { it.copy(editor = null) }
                    refresh()
                }
                is Result.Error -> {
                    _state.update { it.copy(editor = it.editor?.copy(isBusy = false, error = result.error.toUiText())) }
                    members.refreshIfForbidden(result.error)
                }
            }
        }
    }
}
