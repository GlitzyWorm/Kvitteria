/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.insights.CategoryAmount
import dev.jelshoj.kvitteria.core.domain.insights.CategoryBreakdown
import dev.jelshoj.kvitteria.core.domain.insights.InsightsRepository
import dev.jelshoj.kvitteria.core.domain.insights.MealCoverageStats
import dev.jelshoj.kvitteria.core.domain.insights.PriceHistory
import dev.jelshoj.kvitteria.core.domain.insights.PricedItem
import dev.jelshoj.kvitteria.core.domain.insights.TrendMonth
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.taxonomy.Category
import dev.jelshoj.kvitteria.core.domain.taxonomy.TaxonomyRepository
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.Loadable
import dev.jelshoj.kvitteria.core.presentation.Loader
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.refreshIfForbidden
import dev.jelshoj.kvitteria.core.presentation.toUiText
import java.math.BigDecimal
import java.time.Clock
import java.time.YearMonth
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

enum class InsightsView { Categories, Trend, Prices, Dinner }

enum class CategorySort { Size, Change }

enum class InsightsPicker { Month, Compare, PriceItem, Parent }

/** The biggest rise and fall among rows that moved by 1 % or more. */
data class Movers(val rise: CategoryAmount, val fall: CategoryAmount)

object CategoryComparison {
    private val MIN_CHANGE = BigDecimal.ONE

    fun sorted(rows: List<CategoryAmount>, sort: CategorySort): List<CategoryAmount> = when (sort) {
        CategorySort.Size -> rows.sortedByDescending { it.amount }
        CategorySort.Change -> rows.sortedWith(
            compareByDescending<CategoryAmount> { it.changePercent != null }.thenByDescending { it.changePercent },
        )
    }

    fun movers(rows: List<CategoryAmount>): Movers? {
        val moved = rows
            .filter { row -> row.changePercent?.abs()?.let { it >= MIN_CHANGE } == true }
            .sortedByDescending { it.changePercent }
        return if (moved.size < 2) null else Movers(moved.first(), moved.last())
    }
}

data class CategoryDialog(
    val id: String? = null,
    val name: String = "",
    val parentId: String? = null,
    val parentLocked: Boolean = false,
    val isBusy: Boolean = false,
    val error: UiText? = null,
)

/** What each view has chosen, kept while the app is open. */
data class InsightsSelections(
    val view: InsightsView = InsightsView.Categories,
    val month: YearMonth,
    val compareTo: YearMonth?,
    val sort: CategorySort = CategorySort.Size,
    val drill: CategoryAmount? = null,
    val trendMonth: YearMonth,
    val priceItemId: String? = null,
    val mealId: String? = null,
    val picker: InsightsPicker? = null,
    val dialog: CategoryDialog? = null,
)

data class InsightsState(
    val selections: InsightsSelections,
    val months: List<YearMonth>,
    val breakdown: Loadable<CategoryBreakdown> = Loadable(),
    val trend: Loadable<List<TrendMonth>> = Loadable(),
    val priceItems: Loadable<List<PricedItem>> = Loadable(),
    val history: Loadable<PriceHistory> = Loadable(),
    val dinner: Loadable<List<MealCoverageStats>> = Loadable(),
    val categories: List<Category> = emptyList(),
    val canManage: Boolean = false,
)

sealed interface InsightsAction {
    data object OnRefresh : InsightsAction
    data class OnView(val view: InsightsView) : InsightsAction
    data class OnPicker(val picker: InsightsPicker?) : InsightsAction
    data class OnMonth(val month: YearMonth) : InsightsAction
    data class OnCompare(val month: YearMonth?) : InsightsAction
    data class OnSort(val sort: CategorySort) : InsightsAction
    data class OnDrill(val row: CategoryAmount?) : InsightsAction
    data class OnTrendMonth(val month: YearMonth) : InsightsAction
    data class OnPriceItem(val id: String) : InsightsAction
    data class OnMeal(val id: String) : InsightsAction
    data object OnNewCategory : InsightsAction
    data class OnEditCategory(val categoryId: String) : InsightsAction
    data class OnDialogChange(val dialog: CategoryDialog?) : InsightsAction
    data object OnSaveDialog : InsightsAction
}

private const val MONTHS_OFFERED = 12L
private const val RUN = 6
private const val PRICED_ITEMS = 100

class InsightsViewModel(
    private val insights: InsightsRepository,
    private val taxonomy: TaxonomyRepository,
    private val members: MemberRepository,
    clock: Clock,
    sessions: SessionManager,
) : ViewModel() {
    private val current = DanishTime.currentMonth(clock)
    private val selections = MutableStateFlow(
        InsightsSelections(month = current, compareTo = current.minusMonths(1), trendMonth = current),
    )
    private val categories = MutableStateFlow<List<Category>>(emptyList())

    private val breakdown = Loader(viewModelScope) {
        val chosen = selections.value
        insights.categories(chosen.month, chosen.compareTo, chosen.drill?.categoryId)
    }
    private val trend = Loader(viewModelScope) { insights.trend(current, RUN) }
    private val priceItems = Loader(viewModelScope) { insights.pricedItems(1, PRICED_ITEMS).map { it.items } }
    private val history = Loader(viewModelScope) {
        val id = selections.value.priceItemId
        if (id == null) Result.Success(emptyHistory) else insights.priceHistory(id, RUN)
    }
    private val dinner = Loader(viewModelScope) { insights.mealCoverage(current, RUN) }

    private val months = (0 until MONTHS_OFFERED).map { current.minusMonths(it) }

    private val loaded = combine(breakdown.state, trend.state, priceItems.state, history.state, dinner.state) { a, b, c, d, e ->
        Loaded(a, b, c, d, e)
    }

    val state = combine(selections, loaded, categories, sessions.member) { chosen, data, tree, member ->
        InsightsState(
            selections = chosen,
            months = months,
            breakdown = data.breakdown,
            trend = data.trend,
            priceItems = data.priceItems,
            history = data.history,
            dinner = data.dinner,
            categories = tree,
            canManage = member?.can(Permission.ManageTaxonomy) == true,
        )
    }.stateIn(
        viewModelScope,
        SharingStarted.Eagerly,
        InsightsState(selections.value, months),
    )

    val messages = merge(breakdown.failures, trend.failures, priceItems.failures, history.failures, dinner.failures)

    /** Reloads what the shown view needs. Every view keeps what it has while another is showing. */
    fun refresh() {
        when (selections.value.view) {
            InsightsView.Categories -> {
                breakdown.refresh()
                loadCategories()
            }
            InsightsView.Trend -> trend.refresh()
            InsightsView.Prices -> loadPrices()
            InsightsView.Dinner -> dinner.refresh()
        }
    }

    private fun loadCategories(force: Boolean = false) {
        viewModelScope.launch {
            (taxonomy.categories(force) as? Result.Success)?.data?.let { categories.value = it }
        }
    }

    private fun loadPrices() {
        priceItems.refresh()
        viewModelScope.launch {
            val items = priceItems.state.first { !it.isLoading }.data ?: return@launch
            if (selections.value.priceItemId == null) {
                selections.update { it.copy(priceItemId = items.firstOrNull()?.id) }
            }
            history.refresh()
        }
    }

    fun onAction(action: InsightsAction) {
        when (action) {
            InsightsAction.OnRefresh -> refresh()
            is InsightsAction.OnView -> {
                selections.update { it.copy(view = action.view) }
                refresh()
            }
            is InsightsAction.OnPicker -> selections.update { it.copy(picker = action.picker) }
            is InsightsAction.OnMonth -> {
                selections.update {
                    it.copy(
                        month = action.month,
                        compareTo = it.compareTo.takeIf { month -> month != action.month },
                        picker = null,
                    )
                }
                breakdown.refresh()
            }
            is InsightsAction.OnCompare -> {
                selections.update {
                    it.copy(
                        compareTo = action.month,
                        // Sorting by change means nothing with nothing to change from.
                        sort = if (action.month == null) CategorySort.Size else it.sort,
                        picker = null,
                    )
                }
                breakdown.refresh()
            }
            is InsightsAction.OnSort -> selections.update { it.copy(sort = action.sort) }
            is InsightsAction.OnDrill -> {
                selections.update { it.copy(drill = action.row) }
                breakdown.refresh()
            }
            is InsightsAction.OnTrendMonth -> selections.update { it.copy(trendMonth = action.month) }
            is InsightsAction.OnPriceItem -> {
                selections.update { it.copy(priceItemId = action.id, picker = null) }
                history.refresh()
            }
            is InsightsAction.OnMeal -> selections.update { it.copy(mealId = action.id) }
            InsightsAction.OnNewCategory -> selections.update {
                it.copy(dialog = CategoryDialog(parentId = it.drill?.categoryId))
            }
            is InsightsAction.OnEditCategory -> openEditor(action.categoryId)
            is InsightsAction.OnDialogChange -> selections.update { it.copy(dialog = action.dialog) }
            InsightsAction.OnSaveDialog -> saveDialog()
        }
    }

    private fun openEditor(id: String) {
        val all = categories.value.flatMap { listOf(it) + it.children }
        val category = all.firstOrNull { it.id == id } ?: return
        selections.update {
            it.copy(
                dialog = CategoryDialog(
                    id = category.id,
                    name = category.name,
                    parentId = category.parentId,
                    parentLocked = category.children.isNotEmpty(),
                ),
            )
        }
    }

    private fun saveDialog() {
        val dialog = selections.value.dialog ?: return
        if (dialog.isBusy || dialog.name.isBlank()) return

        selections.update { it.copy(dialog = dialog.copy(isBusy = true, error = null)) }
        viewModelScope.launch {
            val name = dialog.name.trim()
            val result = if (dialog.id == null) {
                taxonomy.createCategory(name, dialog.parentId)
            } else {
                taxonomy.updateCategory(dialog.id, name, dialog.parentId)
            }
            when (result) {
                is Result.Error -> {
                    selections.update {
                        it.copy(dialog = it.dialog?.copy(isBusy = false, error = result.error.toUiText()))
                    }
                    members.refreshIfForbidden(result.error)
                }
                is Result.Success -> {
                    selections.update { it.copy(dialog = null) }
                    loadCategories(force = true)
                    breakdown.refresh()
                }
            }
        }
    }

    private data class Loaded(
        val breakdown: Loadable<CategoryBreakdown>,
        val trend: Loadable<List<TrendMonth>>,
        val priceItems: Loadable<List<PricedItem>>,
        val history: Loadable<PriceHistory>,
        val dinner: Loadable<List<MealCoverageStats>>,
    )

    private companion object {
        val emptyHistory = PriceHistory("", "", emptyList(), null, null, null)
    }
}
