/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.sort.presentation

import app.cash.turbine.test
import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.isEqualTo
import assertk.assertions.isNotNull
import assertk.assertions.isNull
import assertk.assertions.isTrue
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.WorkCounts
import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import dev.jelshoj.kvitteria.core.domain.taxonomy.CanonicalItem
import dev.jelshoj.kvitteria.core.domain.taxonomy.Category
import dev.jelshoj.kvitteria.core.domain.taxonomy.Meal
import dev.jelshoj.kvitteria.core.domain.taxonomy.TaxonomyRepository
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.feature.sort.domain.SkippedLines
import dev.jelshoj.kvitteria.feature.sort.domain.SortRepository
import dev.jelshoj.kvitteria.feature.sort.domain.SortRequest
import dev.jelshoj.kvitteria.feature.sort.domain.SortResult
import dev.jelshoj.kvitteria.feature.sort.domain.UnassignedLine
import java.math.BigDecimal
import java.time.Instant
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test

private fun line(id: String, raw: String = "HAKKET OKSEKØD 8-12%") = UnassignedLine(
    id, raw, BigDecimal.ONE, BigDecimal.TEN, BigDecimal.TEN, "r", "Netto", Instant.parse("2026-08-18T10:00:00Z"), null,
)

private val beef = CanonicalItem("beef", "Hakket oksekød 8-12%", "meat", "dinner")

private class FakeSorting : SortRepository {
    var waiting = listOf(line("a"), line("b"), line("c"))
    var nextSort: Result<SortResult, ApiError>? = null
    var sent: SortRequest? = null

    override suspend fun queue(): Result<List<UnassignedLine>, ApiError> = Result.Success(waiting)

    override suspend fun sort(lineId: String, request: SortRequest): Result<SortResult, ApiError> {
        sent = request
        nextSort?.let { return it }
        waiting = waiting.filter { it.id != lineId }
        return Result.Success(SortResult(otherLinesAssigned = 0, remainingCount = waiting.size))
    }
}

private class FakeSkipped : SkippedLines {
    override val ids = mutableSetOf<String>()

    override fun skip(id: String) {
        ids += id
    }

    override fun clear() = ids.clear()
}

private class FakeTaxonomy : TaxonomyRepository {
    var categoryFailure: ApiError? = null
    val categories = mutableListOf(Category("meat", "Kød", null, emptyList(), 0))

    override suspend fun categories(force: Boolean): Result<List<Category>, ApiError> = Result.Success(categories.toList())

    override suspend fun meals(force: Boolean): Result<List<Meal>, ApiError> = Result.Success(emptyList())

    override suspend fun items(force: Boolean): Result<List<CanonicalItem>, ApiError> = Result.Success(listOf(beef))

    override suspend fun createCategory(name: String, parentId: String?): Result<Category, ApiError> {
        categoryFailure?.let { return Result.Error(it) }
        val created = Category("new", name, parentId, emptyList(), 1)
        categories += created
        return Result.Success(created)
    }

    override suspend fun updateCategory(id: String, name: String, parentId: String?): Result<Category, ApiError> =
        error("unused")

    override fun invalidateItems() = Unit
}

private class FakeCounts : WorkCounts {
    override val unassignedLines = MutableStateFlow(0)
    override val failedMail = MutableStateFlow(0)

    override suspend fun refresh() = Unit

    override fun setUnassignedLines(count: Int) {
        unassignedLines.value = count
    }

    override fun setFailedMail(count: Int) = Unit
}

private class FakeMembers : MemberRepository {
    override suspend fun refreshCurrent(): Result<CurrentMember, ApiError> = error("unused")

    override suspend fun members(): Result<List<Member>, ApiError> = Result.Success(emptyList())
}

private class FakeSessions(permissions: Set<Permission>) : SessionManager {
    override val session = MutableStateFlow<StoredSession?>(null)
    override val member = MutableStateFlow<CurrentMember?>(
        CurrentMember("u", "a@b.dk", "A", Role.Member, MemberStatus.Active, permissions),
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
class SortViewModelTest {
    private val sorting = FakeSorting()
    private val skipped = FakeSkipped()
    private val taxonomy = FakeTaxonomy()
    private val counts = FakeCounts()

    @BeforeEach
    fun setUp() {
        Dispatchers.setMain(UnconfinedTestDispatcher())
    }

    @AfterEach
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun viewModel(permissions: Set<Permission> = Permission.entries.toSet()) =
        SortViewModel(sorting, skipped, taxonomy, counts, FakeMembers(), FakeSessions(permissions)).also { it.refresh() }

    private fun SortViewModel.pick() = onAction(SortAction.OnPickItem(state.value.suggestions.first().item))

    @Test
    fun `the first line is shown with the rest up next`() {
        val state = viewModel().state.value
        assertThat(state.current?.id).isEqualTo("a")
        assertThat(state.upNext.map { it.id }).containsExactly("b", "c")
    }

    @Test
    fun `picking an item fills its defaults, remembers the alias and leaves defaults off`() {
        val viewModel = viewModel()
        viewModel.pick()

        val draft = viewModel.state.value.draft!!
        assertThat(draft.categoryId).isEqualTo("meat")
        assertThat(draft.mealId).isEqualTo("dinner")
        assertThat(draft.rememberAlias).isEqualTo(true)
        assertThat(draft.setDefaults).isEqualTo(false)
    }

    @Test
    fun `creating an item prefills a tidy name and turns both on`() {
        sorting.waiting = listOf(line("a", "RØDVINSEDDIKE 500ML"))
        val viewModel = viewModel()
        viewModel.onAction(SortAction.OnCreateItem)

        val draft = viewModel.state.value.draft!!
        assertThat(draft.name).isEqualTo("Rødvinseddike 500ml")
        assertThat(draft.isNew).isTrue()
        assertThat(draft.rememberAlias && draft.setDefaults).isTrue()
    }

    @Test
    fun `a save sends one request, advances and updates the badge`() {
        val viewModel = viewModel()
        viewModel.pick()
        viewModel.onAction(SortAction.OnSave)

        assertThat(sorting.sent?.canonicalItemId).isEqualTo("beef")
        assertThat(viewModel.state.value.current?.id).isEqualTo("b")
        assertThat(viewModel.state.value.draft).isNull()
        assertThat(counts.unassignedLines.value).isEqualTo(2)
    }

    @Test
    fun `an alias that teaches other lines says how many and reloads the queue`() {
        val viewModel = viewModel()
        viewModel.pick()
        sorting.nextSort = Result.Success(SortResult(otherLinesAssigned = 2, remainingCount = 0))
        sorting.waiting = emptyList()
        viewModel.onAction(SortAction.OnSave)

        assertThat(viewModel.state.value.learned).isEqualTo(2)
        assertThat(viewModel.state.value.queue.size).isEqualTo(0)
    }

    @Test
    fun `a line someone else sorted reloads and shows the next one`() {
        val viewModel = viewModel()
        viewModel.pick()
        sorting.waiting = listOf(line("b"), line("c"))
        sorting.nextSort = Result.Error(ApiError(DataError.Network.CONFLICT, "already has a canonical item"))
        viewModel.onAction(SortAction.OnSave)

        assertThat(viewModel.state.value.current?.id).isEqualTo("b")
        assertThat(viewModel.state.value.draft).isNull()
    }

    @Test
    fun `a taken item name shows the server's message and keeps the draft`() = runTest {
        val viewModel = viewModel()
        viewModel.onAction(SortAction.OnCreateItem)
        sorting.nextSort = Result.Error(ApiError(DataError.Network.CONFLICT, "A canonical item named 'X' already exists."))

        viewModel.messages.test {
            viewModel.onAction(SortAction.OnSave)
            assertThat(awaitItem()).isEqualTo(UiText.DynamicString("A canonical item named 'X' already exists."))
        }
        assertThat(viewModel.state.value.draft).isNotNull()
        assertThat(viewModel.state.value.current?.id).isEqualTo("a")
    }

    @Test
    fun `a failed save keeps the draft and reports it`() = runTest {
        val viewModel = viewModel()
        viewModel.pick()
        sorting.nextSort = Result.Error(ApiError(DataError.Network.NO_INTERNET))

        viewModel.messages.test {
            viewModel.onAction(SortAction.OnSave)
            assertThat(awaitItem()).isNotNull()
        }
        assertThat(viewModel.state.value.draft).isNotNull()
        assertThat(viewModel.state.value.isSaving).isEqualTo(false)
    }

    @Test
    fun `skipping moves on without a request, and survives a reload`() {
        val viewModel = viewModel()
        viewModel.onAction(SortAction.OnSkip)

        assertThat(viewModel.state.value.current?.id).isEqualTo("b")
        assertThat(sorting.sent).isNull()

        viewModel.refresh()
        assertThat(viewModel.state.value.current?.id).isEqualTo("b")
    }

    @Test
    fun `everything skipped offers them again`() {
        val viewModel = viewModel()
        repeat(3) { viewModel.onAction(SortAction.OnSkip) }
        assertThat(viewModel.state.value.current).isNull()
        assertThat(viewModel.state.value.skippedCount).isEqualTo(3)

        viewModel.onAction(SortAction.OnShowSkipped)
        assertThat(viewModel.state.value.current?.id).isEqualTo("a")
    }

    @Test
    fun `a category created in place is selected and the draft is otherwise unchanged`() {
        val viewModel = viewModel()
        viewModel.pick()
        viewModel.onAction(SortAction.OnNewCategory)
        viewModel.onAction(SortAction.OnNewCategoryChange(NewCategoryDraft(name = "Frost")))
        viewModel.onAction(SortAction.OnSaveNewCategory)

        val state = viewModel.state.value
        assertThat(state.draft?.categoryId).isEqualTo("new")
        assertThat(state.draft?.mealId).isEqualTo("dinner")
        assertThat(state.newCategory).isNull()
        assertThat(state.categories.map { it.id }).containsExactly("meat", "new")
    }

    @Test
    fun `a taken category name stays in the dialog`() {
        val viewModel = viewModel()
        viewModel.pick()
        taxonomy.categoryFailure = ApiError(DataError.Network.CONFLICT, "taken")
        viewModel.onAction(SortAction.OnNewCategoryChange(NewCategoryDraft(name = "Kød")))
        viewModel.onAction(SortAction.OnSaveNewCategory)

        assertThat(viewModel.state.value.newCategory?.error).isEqualTo(UiText.DynamicString("taken"))
    }

    @Test
    fun `a member without the permission can't save`() {
        val viewModel = viewModel(permissions = emptySet())
        viewModel.pick()
        viewModel.onAction(SortAction.OnSave)
        assertThat(sorting.sent).isNull()
    }
}
