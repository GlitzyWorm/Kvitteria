/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.receipts.presentation

import app.cash.turbine.test
import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.hasSize
import assertk.assertions.isEqualTo
import assertk.assertions.isNotNull
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.receipt.LineCorrection
import dev.jelshoj.kvitteria.core.domain.receipt.MealCoverage
import dev.jelshoj.kvitteria.core.domain.receipt.Page
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptLine
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptRepository
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptStatus
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import dev.jelshoj.kvitteria.core.domain.taxonomy.CanonicalItem
import dev.jelshoj.kvitteria.core.domain.taxonomy.Category
import dev.jelshoj.kvitteria.core.domain.taxonomy.Meal
import dev.jelshoj.kvitteria.core.domain.taxonomy.TaxonomyRepository
import dev.jelshoj.kvitteria.core.presentation.UiText
import java.math.BigDecimal
import java.time.Instant
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test

private fun line(id: String, mealId: String? = null, categoryId: String? = null) = ReceiptLine(
    id, "RAW $id", BigDecimal.ONE, BigDecimal.TEN, BigDecimal.TEN, null, null, mealId, null, categoryId, null,
)

private fun sampleReceipt(id: String = "r", lines: List<ReceiptLine> = listOf(line("l1")), coverage: List<MealCoverage> = emptyList()) =
    Receipt(
        id, "Netto", Instant.parse("2026-08-18T10:00:00Z"), BigDecimal.TEN, BigDecimal.ZERO, true,
        ReceiptStatus.Completed, lines, coverage, null, null, null,
    )

private class FakeReceipts : ReceiptRepository {
    var stored = sampleReceipt()
    var pages: suspend (Int, String?) -> Result<Page<Receipt>, ApiError> =
        { page, _ -> Result.Success(Page(listOf(sampleReceipt("r$page")), page, 30, 90)) }
    val searches = mutableListOf<String?>()
    var failLineWrites = false
    val coverageSent = mutableListOf<Map<String, Int>>()
    var fetches = 0

    override suspend fun receipts(page: Int, pageSize: Int, search: String?, unreconciledOnly: Boolean): Result<Page<Receipt>, ApiError> {
        searches += search
        return pages(page, search)
    }

    override suspend fun receipt(id: String): Result<Receipt, ApiError> {
        fetches++
        return Result.Success(stored)
    }

    override fun peek(id: String): Receipt? = stored

    private fun lineResult(update: (ReceiptLine) -> ReceiptLine, id: String): Result<ReceiptLine, ApiError> {
        if (failLineWrites) return Result.Error(ApiError(DataError.Network.NO_INTERNET))
        stored = stored.copy(lines = stored.lines.map { if (it.id == id) update(it) else it })
        return Result.Success(stored.lines.first { it.id == id })
    }

    override suspend fun setLineCategory(lineId: String, categoryId: String?) = lineResult({ it.copy(categoryId = categoryId) }, lineId)

    override suspend fun setLineMeal(lineId: String, mealId: String?) = lineResult({ it.copy(mealId = mealId) }, lineId)

    override suspend fun correctLine(lineId: String, correction: LineCorrection) =
        lineResult({ it.copy(lineTotal = correction.lineTotal) }, lineId)

    override suspend fun setCoverage(receiptId: String, days: Map<String, Int>): Result<List<MealCoverage>, ApiError> {
        coverageSent += days
        stored = stored.copy(coverage = days.map { MealCoverage(it.key, "", it.value) })
        return Result.Success(stored.coverage)
    }

    var deleteResult: EmptyResult<ApiError> = Result.Success(Unit)
    val deletedIds = mutableListOf<String>()
    var imageResult: Result<ByteArray, ApiError> = Result.Success(byteArrayOf(1, 2, 3))
    var imageFetches = 0

    val deletions = kotlinx.coroutines.flow.MutableSharedFlow<String>(extraBufferCapacity = 8)
    override val deleted = deletions

    override suspend fun delete(id: String): EmptyResult<ApiError> {
        this.deletedIds += id
        val result = deleteResult
        if (result is Result.Success || (result as? Result.Error)?.error?.kind == DataError.Network.NOT_FOUND) deletions.tryEmit(id)
        return result
    }

    override suspend fun image(id: String): Result<ByteArray, ApiError> {
        imageFetches++
        return imageResult
    }
}

private class FakeTaxonomy : TaxonomyRepository {
    override suspend fun categories(force: Boolean): Result<List<Category>, ApiError> = Result.Success(emptyList())

    override suspend fun meals(force: Boolean): Result<List<Meal>, ApiError> =
        Result.Success(listOf(Meal("dinner", "Aftensmad", 0, true), Meal("lunch", "Frokost", 1, false)))

    override suspend fun items(force: Boolean): Result<List<CanonicalItem>, ApiError> = Result.Success(emptyList())

    override suspend fun createCategory(name: String, parentId: String?): Result<Category, ApiError> = error("unused")

    override suspend fun updateCategory(id: String, name: String, parentId: String?): Result<Category, ApiError> = error("unused")

    override fun invalidateItems() = Unit
}

private class FakeMembers : MemberRepository {
    override suspend fun refreshCurrent(): Result<CurrentMember, ApiError> = error("unused")

    override suspend fun members(): Result<List<Member>, ApiError> = Result.Success(emptyList())
}

private class FakeSessions : SessionManager {
    override val session = MutableStateFlow<StoredSession?>(null)
    override val member = MutableStateFlow<CurrentMember?>(
        CurrentMember("u", "a@b.dk", "A", Role.Member, MemberStatus.Active, Permission.entries.toSet()),
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
class ReceiptsViewModelsTest {
    private val dispatcher = StandardTestDispatcher()
    private val repository = FakeReceipts()

    @BeforeEach
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @AfterEach
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun TestScope.list() = ReceiptsViewModel(repository).also { advanceUntilIdle() }

    private fun TestScope.detail(): ReceiptDetailViewModel {
        val viewModel = ReceiptDetailViewModel("r", repository, FakeTaxonomy(), FakeMembers(), FakeSessions())
        advanceUntilIdle()
        return viewModel
    }

    @Test
    fun `opening the list shows the first page`() = runTest(dispatcher) {
        val viewModel = list()
        assertThat(viewModel.state.value.receipts.map { it.id }).containsExactly("r1")
        assertThat(viewModel.state.value.hasMore).isEqualTo(true)
    }

    @Test
    fun `scrolling appends the next page`() = runTest(dispatcher) {
        val viewModel = list()
        viewModel.onAction(ReceiptsAction.OnLoadMore)
        advanceUntilIdle()
        assertThat(viewModel.state.value.receipts.map { it.id }).containsExactly("r1", "r2")
    }

    @Test
    fun `typing sends one request after a 300 ms pause`() = runTest(dispatcher) {
        val viewModel = list()
        repository.searches.clear()

        "net".forEachIndexed { index, _ ->
            viewModel.onAction(ReceiptsAction.OnSearchChange("net".take(index + 1)))
            advanceTimeBy(100)
        }
        advanceTimeBy(198)
        runCurrent()
        assertThat(repository.searches).hasSize(0)

        advanceTimeBy(3)
        runCurrent()
        assertThat(repository.searches).containsExactly("net")
    }

    @Test
    fun `an older search answering after a newer one is discarded`() = runTest(dispatcher) {
        val slow = CompletableDeferred<Unit>()
        repository.pages = { page, search ->
            if (search == "old") slow.await()
            Result.Success(Page(listOf(sampleReceipt(search.orEmpty().ifEmpty { "start" })), page, 30, 1))
        }
        val viewModel = list()

        viewModel.onAction(ReceiptsAction.OnSearchChange("old"))
        advanceTimeBy(301)
        runCurrent()
        viewModel.onAction(ReceiptsAction.OnSearchChange("new"))
        advanceTimeBy(301)
        runCurrent()
        slow.complete(Unit)
        advanceUntilIdle()

        assertThat(viewModel.state.value.receipts.map { it.id }).containsExactly("new")
    }

    @Test
    fun `a failed refresh keeps the receipts and reports it`() = runTest(dispatcher) {
        val viewModel = list()
        repository.pages = { _, _ -> Result.Error(ApiError(DataError.Network.NO_INTERNET)) }

        viewModel.messages.test {
            viewModel.onAction(ReceiptsAction.OnRefresh)
            advanceUntilIdle()
            assertThat(awaitItem()).isNotNull()
        }
        assertThat(viewModel.state.value.receipts).hasSize(1)
        assertThat(viewModel.state.value.error).isNull()
    }

    @Test
    fun `detail paints at once from the cached copy`() = runTest(dispatcher) {
        val viewModel = ReceiptDetailViewModel("r", repository, FakeTaxonomy(), FakeMembers(), FakeSessions())
        assertThat(viewModel.state.value.receipt).isEqualTo(repository.stored)
        assertThat(viewModel.state.value.isLoading).isEqualTo(false)
    }

    @Test
    fun `a line change shows before the answer and rolls back on failure`() = runTest(dispatcher) {
        val viewModel = detail()
        repository.failLineWrites = true

        viewModel.messages.test {
            viewModel.onAction(ReceiptDetailAction.OnOpenPicker("l1", LinePicker.Meal))
            viewModel.onAction(ReceiptDetailAction.OnPickMeal("dinner"))
            assertThat(viewModel.state.value.receipt?.lines?.single()?.mealId).isEqualTo("dinner")

            advanceUntilIdle()
            assertThat(viewModel.state.value.receipt?.lines?.single()?.mealId).isNull()
            assertThat(awaitItem()).isNotNull()
        }
    }

    @Test
    fun `clearing the last line of a meal refreshes the coverage`() = runTest(dispatcher) {
        repository.stored = sampleReceipt(lines = listOf(line("l1", mealId = "dinner")), coverage = listOf(MealCoverage("dinner", "", 3)))
        val viewModel = detail()
        val fetchesBefore = repository.fetches

        viewModel.onAction(ReceiptDetailAction.OnOpenPicker("l1", LinePicker.Meal))
        viewModel.onAction(ReceiptDetailAction.OnPickMeal(null))
        advanceUntilIdle()

        assertThat(repository.fetches).isEqualTo(fetchesBefore + 1)
    }

    @Test
    fun `rapid taps on a stepper send one final request with the full set`() = runTest(dispatcher) {
        repository.stored = sampleReceipt(lines = listOf(line("l1", mealId = "dinner")))
        val viewModel = detail()

        repeat(4) {
            viewModel.onAction(ReceiptDetailAction.OnStepCoverage("dinner", 1))
            advanceTimeBy(100)
        }
        assertThat(viewModel.state.value.coverage["dinner"]).isEqualTo(4)
        advanceUntilIdle()

        assertThat(repository.coverageSent).containsExactly(mapOf("dinner" to 4))
    }

    @Test
    fun `days stay between 0 and 31`() = runTest(dispatcher) {
        val viewModel = detail()
        viewModel.onAction(ReceiptDetailAction.OnStepCoverage("dinner", -1))
        assertThat(viewModel.state.value.coverage["dinner"]).isEqualTo(0)
        repeat(40) { viewModel.onAction(ReceiptDetailAction.OnStepCoverage("dinner", 1)) }
        assertThat(viewModel.state.value.coverage["dinner"]).isEqualTo(31)
    }

    @Test
    fun `a tracking meal on no line is blocked`() = runTest(dispatcher) {
        val viewModel = detail()
        assertThat(viewModel.state.value.coverageMeals).hasSize(0)
        assertThat(viewModel.state.value.blockedMeals.map { it.id }).containsExactly("dinner")
    }

    @Test
    fun `an invalid figure marks its field and sends nothing`() = runTest(dispatcher) {
        val viewModel = detail()
        viewModel.onAction(ReceiptDetailAction.OnOpenEditor(repository.stored.lines.single()))
        val editor = viewModel.state.value.editor!!
        viewModel.onAction(ReceiptDetailAction.OnEditorChange(editor.copy(lineTotal = "abc")))
        viewModel.onAction(ReceiptDetailAction.OnSaveEditor)
        advanceUntilIdle()

        assertThat(viewModel.state.value.editor?.lineTotalInvalid).isEqualTo(true)
        assertThat(repository.stored.lines.single().lineTotal).isEqualTo(BigDecimal.TEN)
    }

    @Test
    fun `a corrected figure with a decimal comma is saved exactly`() = runTest(dispatcher) {
        val viewModel = detail()
        viewModel.onAction(ReceiptDetailAction.OnOpenEditor(repository.stored.lines.single()))
        val editor = viewModel.state.value.editor!!
        viewModel.onAction(ReceiptDetailAction.OnEditorChange(editor.copy(lineTotal = "12,50")))
        viewModel.onAction(ReceiptDetailAction.OnSaveEditor)
        advanceUntilIdle()

        assertThat(repository.stored.lines.single().lineTotal).isEqualTo(BigDecimal("12.50"))
        assertThat(viewModel.state.value.editor).isNull()
    }

    @Test
    fun `a confirmed delete closes detail and the list drops the receipt`() = runTest(dispatcher) {
        val listed = list()
        repository.stored = sampleReceipt("r1")
        val viewModel = ReceiptDetailViewModel("r1", repository, FakeTaxonomy(), FakeMembers(), FakeSessions())
        advanceUntilIdle()

        // The server no longer lists it once it is gone.
        repository.pages = { page, _ -> Result.Success(Page(emptyList(), page, 30, 0)) }
        viewModel.events.test {
            viewModel.onAction(ReceiptDetailAction.OnAskDelete)
            assertThat(viewModel.state.value.isConfirmingDelete).isEqualTo(true)
            viewModel.onAction(ReceiptDetailAction.OnConfirmDelete)
            advanceUntilIdle()
            assertThat(awaitItem()).isEqualTo(ReceiptDetailEvent.Close)
        }
        assertThat(repository.deletedIds).containsExactly("r1")
        assertThat(listed.state.value.receipts).isEqualTo(emptyList())
    }

    @Test
    fun `a delete answered 404 shows the server's message and returns to the reloaded list`() = runTest(dispatcher) {
        val listed = list()
        val fetchesBefore = repository.searches.size
        repository.deleteResult = Result.Error(ApiError(DataError.Network.NOT_FOUND, "Receipt with id r not found"))
        val viewModel = detail()

        viewModel.messages.test {
            viewModel.events.test {
                viewModel.onAction(ReceiptDetailAction.OnAskDelete)
                viewModel.onAction(ReceiptDetailAction.OnConfirmDelete)
                advanceUntilIdle()
                assertThat(awaitItem()).isEqualTo(ReceiptDetailEvent.Close)
            }
            assertThat(awaitItem()).isEqualTo(UiText.DynamicString("Receipt with id r not found"))
        }
        assertThat(repository.searches.size).isEqualTo(fetchesBefore + 1)
        assertThat(listed.state.value.isLoading).isEqualTo(false)
    }

    @Test
    fun `delete is not offered without EditReceipts`() = runTest(dispatcher) {
        val sessions = FakeSessions().apply {
            member.value = member.value!!.copy(permissions = setOf(Permission.CategorizeLines))
        }
        val viewModel = ReceiptDetailViewModel("r", repository, FakeTaxonomy(), FakeMembers(), sessions)
        advanceUntilIdle()

        assertThat(viewModel.state.value.canDelete).isEqualTo(false)
        viewModel.onAction(ReceiptDetailAction.OnAskDelete)
        assertThat(viewModel.state.value.isConfirmingDelete).isEqualTo(false)
    }

    @Test
    fun `an image that fails to load offers a retry and leaves the rest alone`() = runTest(dispatcher) {
        repository.stored = sampleReceipt().copy(hasImage = true)
        repository.imageResult = Result.Error(ApiError(DataError.Network.NO_INTERNET))
        val viewModel = detail()

        assertThat(viewModel.state.value.image).isEqualTo(ReceiptImage.Failed)
        assertThat(viewModel.state.value.receipt).isNotNull()
        assertThat(viewModel.state.value.error).isNull()

        repository.imageResult = Result.Success(byteArrayOf(7))
        viewModel.onAction(ReceiptDetailAction.OnRetryImage)
        advanceUntilIdle()
        assertThat(viewModel.state.value.image is ReceiptImage.Loaded).isEqualTo(true)
    }

    @Test
    fun `a receipt from mail fetches no image`() = runTest(dispatcher) {
        val viewModel = detail()

        assertThat(viewModel.state.value.image).isEqualTo(ReceiptImage.None)
        assertThat(repository.imageFetches).isEqualTo(0)
    }

    @Test
    fun `the duplicate mark opens the matching receipt`() = runTest(dispatcher) {
        repository.stored = sampleReceipt().copy(possibleDuplicateOfId = "r0")
        val viewModel = detail()

        viewModel.events.test {
            viewModel.onAction(ReceiptDetailAction.OnOpenDuplicate)
            assertThat(awaitItem()).isEqualTo(ReceiptDetailEvent.OpenReceipt("r0"))
        }
    }
}
