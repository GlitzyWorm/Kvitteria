/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.pipeline.presentation

import dev.jelshoj.kvitteria.feature.pipeline.domain.FailureSource
import app.cash.turbine.test
import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.WorkCounts
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
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.feature.pipeline.domain.MailboxHealth
import dev.jelshoj.kvitteria.feature.pipeline.domain.MailboxState
import dev.jelshoj.kvitteria.feature.pipeline.domain.PipelineRepository
import dev.jelshoj.kvitteria.feature.pipeline.domain.PipelineStatus
import dev.jelshoj.kvitteria.feature.pipeline.domain.ReceiptFailure
import dev.jelshoj.kvitteria.feature.pipeline.domain.toState
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
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

private val instant = Instant.parse("2026-08-18T10:00:00Z")
private val failure = ReceiptFailure("f1", "Din kvittering", "netto@x.dk", instant, instant, "timeout", 3, "Sofie")

private class FakePipeline : PipelineRepository {
    var listed = listOf(failure)
    var retryResult: EmptyResult<ApiError> = Result.Success(Unit)
    var loads = 0

    override suspend fun status(): Result<PipelineStatus, ApiError> {
        loads++
        return Result.Success(PipelineStatus(2, 0, 0, listed.size, 0, instant, 300, emptyList()))
    }

    override suspend fun failures(): Result<List<ReceiptFailure>, ApiError> = Result.Success(listed)

    override suspend fun mailboxHealth(): Result<List<MailboxHealth>, ApiError> = Result.Success(emptyList())

    override suspend fun retry(id: String) = retryResult

    override suspend fun dismiss(id: String): EmptyResult<ApiError> = Result.Success(Unit)
}

private class NoReceipts : ReceiptRepository {
    override suspend fun receipts(page: Int, pageSize: Int, search: String?, unreconciledOnly: Boolean): Result<Page<Receipt>, ApiError> =
        Result.Success(Page(emptyList(), 1, pageSize, 0))

    override suspend fun receipt(id: String): Result<Receipt, ApiError> = error("unused")

    override fun peek(id: String): Receipt? = null

    override suspend fun setLineCategory(lineId: String, categoryId: String?): Result<ReceiptLine, ApiError> = error("unused")

    override suspend fun setLineMeal(lineId: String, mealId: String?): Result<ReceiptLine, ApiError> = error("unused")

    override suspend fun correctLine(lineId: String, correction: LineCorrection): Result<ReceiptLine, ApiError> = error("unused")

    override suspend fun setCoverage(receiptId: String, days: Map<String, Int>): Result<List<MealCoverage>, ApiError> = error("unused")

    override suspend fun delete(id: String): Result<Unit, ApiError> = error("unused")

    override suspend fun image(id: String): Result<ByteArray, ApiError> = error("unused")

    override val deleted: kotlinx.coroutines.flow.Flow<String> = kotlinx.coroutines.flow.emptyFlow()
}

private class FakeCounts : WorkCounts {
    override val unassignedLines = MutableStateFlow(0)
    override val failedMail = MutableStateFlow(0)

    override suspend fun refresh() = Unit

    override fun setUnassignedLines(count: Int) = Unit

    override fun setFailedMail(count: Int) {
        failedMail.value = count
    }
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
class PipelineViewModelTest {
    private val pipeline = FakePipeline()
    private val counts = FakeCounts()

    @BeforeEach
    fun setUp() {
        Dispatchers.setMain(UnconfinedTestDispatcher())
    }

    @AfterEach
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun viewModel(permissions: Set<Permission> = Permission.entries.toSet()) = PipelineViewModel(
        pipeline, NoReceipts(), counts, FakeMembers(), Clock.fixed(instant, ZoneOffset.UTC), FakeSessions(permissions),
    ).also { it.refresh() }

    private val PipelineViewModel.data get() = state.value.data.data!!

    @Test
    fun `a retry leaves the list, raises the queued count and updates the badge`() {
        val viewModel = viewModel()
        assertThat(counts.failedMail.value).isEqualTo(1)

        viewModel.onAction(PipelineAction.OnRetry(failure))

        assertThat(viewModel.data.failures.size).isEqualTo(0)
        assertThat(viewModel.data.status.queued).isEqualTo(3)
        assertThat(counts.failedMail.value).isEqualTo(0)
    }

    @Test
    fun `already handled shows the server's message and reloads, on 404 and 409`() = runTest {
        for (kind in listOf(DataError.Network.NOT_FOUND, DataError.Network.CONFLICT)) {
            val viewModel = viewModel()
            val loadsBefore = pipeline.loads
            pipeline.retryResult = Result.Error(ApiError(kind, "Already handled"))

            viewModel.messages.test {
                viewModel.onAction(PipelineAction.OnRetry(failure))
                assertThat(awaitItem()).isEqualTo(UiText.DynamicString("Already handled"))
            }
            assertThat(pipeline.loads).isEqualTo(loadsBefore + 1)
        }
    }

    @Test
    fun `a full queue leaves the failure and shows the message`() = runTest {
        val viewModel = viewModel()
        val loadsBefore = pipeline.loads
        pipeline.retryResult = Result.Error(ApiError(DataError.Network.SERVICE_UNAVAILABLE, "Queue is full"))

        viewModel.messages.test {
            viewModel.onAction(PipelineAction.OnRetry(failure))
            assertThat(awaitItem()).isEqualTo(UiText.DynamicString("Queue is full"))
        }
        assertThat(viewModel.data.failures.size).isEqualTo(1)
        assertThat(pipeline.loads).isEqualTo(loadsBefore)
    }

    @Test
    fun `dismiss asks first and removes on confirmation`() {
        val viewModel = viewModel()
        viewModel.onAction(PipelineAction.OnAskDismiss(failure))
        assertThat(viewModel.data.failures.size).isEqualTo(1)
        assertThat(viewModel.state.value.dismissing).isEqualTo(failure)

        viewModel.onAction(PipelineAction.OnDismiss)
        assertThat(viewModel.state.value.dismissing).isNull()
        assertThat(viewModel.data.failures.size).isEqualTo(0)
        assertThat(viewModel.data.status.queued).isEqualTo(2)
    }

    @Test
    fun `a member without EditReceipts can't act on failures`() {
        assertThat(viewModel(emptySet()).state.value.canEdit).isEqualTo(false)
    }

    @Test
    fun `a mailbox's state comes from its health`() {
        assertThat(MailboxHealth("m", true, null).toState()).isEqualTo(MailboxState.Connected)
        assertThat(MailboxHealth("m", true, "bad password").toState()).isEqualTo(MailboxState.Failing)
        assertThat(MailboxHealth("m", false, "bad password").toState()).isEqualTo(MailboxState.Paused)
    }

    @Test
    fun `a mixed list names mail by subject and an upload as an uploaded image`() = runTest {
        val upload = ReceiptFailure(
            id = "u1",
            subject = "",
            from = "",
            mailDate = Instant.parse("2026-10-26T12:07:00Z"),
            failedAt = Instant.parse("2026-11-08T19:36:48Z"),
            error = "No receipt was found in the image.",
            attempts = 0,
            memberName = "Sofie",
            source = FailureSource.Upload,
            receivedAt = Instant.parse("2026-11-08T19:36:43Z"),
        )

        val mail = failure.summary(java.util.Locale.forLanguageTag("da"))
        val uploaded = upload.summary(java.util.Locale.forLanguageTag("da"))

        assertThat(mail.title).isEqualTo(UiText.DynamicString("Din kvittering"))
        assertThat(mail.meta.first()).isEqualTo("netto@x.dk")
        assertThat(uploaded.title).isEqualTo(UiText.StringResource(R.string.failure_uploaded_image))
        assertThat(uploaded.meta.first()).isEqualTo("Sofie")
        // The upload time, not the empty subject or sender.
        assertThat(uploaded.meta.none { it.isBlank() }).isEqualTo(true)
        assertThat(uploaded.meta.size).isEqualTo(2)
    }
}
