/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.insights.CategoryBreakdown
import dev.jelshoj.kvitteria.core.domain.insights.InsightsRepository
import dev.jelshoj.kvitteria.core.domain.insights.MealCoverageStats
import dev.jelshoj.kvitteria.core.domain.insights.PriceHistory
import dev.jelshoj.kvitteria.core.domain.insights.PriceMonth
import dev.jelshoj.kvitteria.core.domain.insights.PricedItem
import dev.jelshoj.kvitteria.core.domain.insights.PricedName
import dev.jelshoj.kvitteria.core.domain.insights.SpendingSummary
import dev.jelshoj.kvitteria.core.domain.insights.TrendMonth
import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.receipt.Page
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import dev.jelshoj.kvitteria.core.domain.taxonomy.CanonicalItem
import dev.jelshoj.kvitteria.core.domain.taxonomy.Category
import dev.jelshoj.kvitteria.core.domain.taxonomy.Meal
import dev.jelshoj.kvitteria.core.domain.taxonomy.TaxonomyRepository
import java.math.BigDecimal
import java.time.Clock
import java.time.Instant
import java.time.YearMonth
import java.time.ZoneOffset
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test

private val arla = PricedName("arla letmælk 1l", "ARLA LETMÆLK 1L", 3)
private val mini = PricedName("minimælk", "Minimælk", 2)

private fun item(id: String, vararg names: PricedName) = PricedItem(id, id, names.sumOf { it.purchaseCount }, names.toList())

private class FakeInsights : InsightsRepository {
    var items = listOf(item("milk", arla, mini), item("butter", PricedName("smør", "SMØR", 1)))
    val pageSizes = mutableListOf<Int>()
    val histories = mutableListOf<Pair<String, String?>>()

    override suspend fun pricedItems(page: Int, pageSize: Int): Result<Page<PricedItem>, ApiError> {
        // The server pages by `pageSize`; the fake serves one item per page to prove the loader keeps asking.
        pageSizes += pageSize
        return Result.Success(Page(items.drop(page - 1).take(1), page, 1, items.size))
    }

    override suspend fun priceHistory(itemId: String, months: Int, name: String?): Result<PriceHistory, ApiError> {
        histories += itemId to name
        val label = name?.let { key -> items.firstOrNull { it.id == itemId }?.names?.firstOrNull { it.key == key }?.label }
        return Result.Success(
            PriceHistory(
                itemId, itemId, listOf(PriceMonth(YearMonth.of(2026, 8), BigDecimal.TEN, 1)),
                BigDecimal.TEN, BigDecimal.TEN, null, name, label,
            ),
        )
    }

    override suspend fun summary(month: YearMonth): Result<SpendingSummary, ApiError> = error("unused")

    override suspend fun categories(month: YearMonth, compareTo: YearMonth?, parentId: String?): Result<CategoryBreakdown, ApiError> =
        error("unused")

    override suspend fun trend(endMonth: YearMonth, months: Int): Result<List<TrendMonth>, ApiError> = error("unused")

    override suspend fun mealCoverage(endMonth: YearMonth, months: Int): Result<List<MealCoverageStats>, ApiError> =
        error("unused")
}

private class UnusedTaxonomy : TaxonomyRepository {
    override suspend fun categories(force: Boolean): Result<List<Category>, ApiError> = Result.Success(emptyList())

    override suspend fun meals(force: Boolean): Result<List<Meal>, ApiError> = Result.Success(emptyList())

    override suspend fun items(force: Boolean): Result<List<CanonicalItem>, ApiError> = Result.Success(emptyList())

    override suspend fun createCategory(name: String, parentId: String?): Result<Category, ApiError> = error("unused")

    override suspend fun updateCategory(id: String, name: String, parentId: String?): Result<Category, ApiError> =
        error("unused")

    override fun invalidateItems() = Unit
}

private class UnusedMembers : MemberRepository {
    override suspend fun refreshCurrent(): Result<CurrentMember, ApiError> = error("unused")

    override suspend fun members(): Result<List<Member>, ApiError> = Result.Success(emptyList())
}

private class MemberSessions : SessionManager {
    override val session = MutableStateFlow<StoredSession?>(null)
    override val member = MutableStateFlow<CurrentMember?>(
        CurrentMember("u", "a@b.dk", "A", Role.Member, MemberStatus.Active, emptySet()),
    )

    override suspend fun load() = null

    override suspend fun adopt(session: StoredSession) = Unit

    override fun setMember(member: CurrentMember?) = Unit

    override suspend fun ensureFresh() = SessionState.Ended

    override suspend fun refresh(spentAccessToken: String) = SessionState.Ended

    override suspend fun signOut() = Unit

    override suspend fun clear() = Unit
}

@OptIn(ExperimentalCoroutinesApi::class)
class PriceSelectionTest {
    private val insights = FakeInsights()

    @BeforeEach
    fun setUp() {
        Dispatchers.setMain(UnconfinedTestDispatcher())
    }

    @AfterEach
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun viewModel(): InsightsViewModel {
        val clock = Clock.fixed(Instant.parse("2026-08-20T10:00:00Z"), ZoneOffset.UTC)
        return InsightsViewModel(insights, UnusedTaxonomy(), UnusedMembers(), clock, MemberSessions()).also {
            it.onAction(InsightsAction.OnView(InsightsView.Prices))
        }
    }

    @Test
    fun `every page of items is loaded and the first item is chosen`() {
        val viewModel = viewModel()

        assertThat(viewModel.state.value.priceItems.data?.map { it.id }).isEqualTo(listOf("milk", "butter"))
        assertThat(viewModel.state.value.selections.price).isEqualTo(PriceSelection("milk"))
        assertThat(insights.histories.last()).isEqualTo("milk" to null)
    }

    @Test
    fun `picking a raw name asks the server for that name`() {
        val viewModel = viewModel()
        viewModel.onAction(InsightsAction.OnPrice(PriceSelection("milk", arla.key)))

        assertThat(insights.histories.last()).isEqualTo("milk" to arla.key)
        assertThat(viewModel.state.value.history.data?.nameLabel).isEqualTo(arla.label)
    }

    @Test
    fun `tapping a month selects it and tapping it again clears it`() {
        val viewModel = viewModel()
        val august = YearMonth.of(2026, 8)

        viewModel.onAction(InsightsAction.OnPriceMonth(august))
        assertThat(viewModel.state.value.selections.priceMonth).isEqualTo(august)

        viewModel.onAction(InsightsAction.OnPriceMonth(august))
        assertThat(viewModel.state.value.selections.priceMonth).isNull()
    }

    @Test
    fun `picking another item or name clears the selected month`() {
        val viewModel = viewModel()
        viewModel.onAction(InsightsAction.OnPriceMonth(YearMonth.of(2026, 8)))
        viewModel.onAction(InsightsAction.OnPrice(PriceSelection("butter")))

        assertThat(viewModel.state.value.selections.priceMonth).isNull()
    }

    @Test
    fun `a reload keeps a name that still exists`() {
        val viewModel = viewModel()
        viewModel.onAction(InsightsAction.OnPrice(PriceSelection("milk", mini.key)))
        viewModel.onAction(InsightsAction.OnPriceMonth(YearMonth.of(2026, 8)))
        viewModel.refresh()

        assertThat(viewModel.state.value.selections.price).isEqualTo(PriceSelection("milk", mini.key))
        assertThat(viewModel.state.value.selections.priceMonth).isEqualTo(YearMonth.of(2026, 8))
    }

    @Test
    fun `a reload falls back to the item when its name is gone`() {
        val viewModel = viewModel()
        viewModel.onAction(InsightsAction.OnPrice(PriceSelection("milk", mini.key)))
        insights.items = listOf(item("milk", arla), item("butter", PricedName("smør", "SMØR", 1)))
        viewModel.refresh()

        assertThat(viewModel.state.value.selections.price).isEqualTo(PriceSelection("milk"))
    }

    @Test
    fun `a reload falls back to the first item when the item is gone`() {
        val viewModel = viewModel()
        viewModel.onAction(InsightsAction.OnPrice(PriceSelection("butter")))
        insights.items = listOf(item("milk", arla, mini))
        viewModel.refresh()

        assertThat(viewModel.state.value.selections.price).isEqualTo(PriceSelection("milk"))
        assertThat(viewModel.state.value.selections.priceMonth).isNull()
    }

    @Test
    fun `closing the sheet clears the search`() {
        val viewModel = viewModel()
        viewModel.onAction(InsightsAction.OnPicker(InsightsPicker.PriceItem))
        viewModel.onAction(InsightsAction.OnPriceSearch("arla"))
        assertThat(viewModel.state.value.selections.priceSearch).isEqualTo("arla")

        viewModel.onAction(InsightsAction.OnPicker(null))
        assertThat(viewModel.state.value.selections.priceSearch).isEqualTo("")
    }

    @Test
    fun `the labels of the raw names reach the sheet in order`() {
        val viewModel = viewModel()
        val milk = viewModel.state.value.priceItems.data!!.first()

        assertThat(milk.names.map { it.label }).containsExactly(arla.label, mini.label)
    }
}
