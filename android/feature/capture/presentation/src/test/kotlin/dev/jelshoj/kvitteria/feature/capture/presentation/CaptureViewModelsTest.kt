/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.presentation

import app.cash.turbine.test
import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isFalse
import assertk.assertions.isNotNull
import assertk.assertions.isNull
import assertk.assertions.isSameInstanceAs
import assertk.assertions.isTrue
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.feature.capture.domain.ImagePreparer
import dev.jelshoj.kvitteria.feature.capture.domain.InMemoryPendingCapture
import dev.jelshoj.kvitteria.feature.capture.domain.PrepareError
import dev.jelshoj.kvitteria.feature.capture.domain.PreparedImage
import dev.jelshoj.kvitteria.feature.capture.domain.ReceiptUploader
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

private class FakeUploader : ReceiptUploader {
    var result: Result<String, ApiError> = Result.Success("intake")
    var uploads = 0

    override suspend fun upload(image: PreparedImage): Result<String, ApiError> {
        uploads++
        return result
    }
}

private class FakePreparer : ImagePreparer {
    var result: Result<PreparedImage, PrepareError> = Result.Success(PreparedImage(byteArrayOf(9), 1, 1, null))

    override suspend fun prepare(uri: String) = result
}

private class FakeMembers : MemberRepository {
    override suspend fun refreshCurrent(): Result<CurrentMember, ApiError> = Result.Error(ApiError(DataError.Network.UNKNOWN))

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
class CaptureViewModelsTest {
    private val image = PreparedImage(byteArrayOf(1, 2, 3), 3, 1, null)
    private val pending = InMemoryPendingCapture().also { it.set(image) }
    private val uploader = FakeUploader()
    private val notices = CaptureNotices()

    @BeforeEach
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @AfterEach
    fun tearDown() = Dispatchers.resetMain()

    private fun confirm() = ConfirmCaptureViewModel(pending, uploader, notices, FakeMembers())

    @Test
    fun `an accepted upload says it is queued, clears the image and closes`() = runTest {
        val viewModel = confirm()

        viewModel.events.test {
            viewModel.onAction(ConfirmCaptureAction.OnUpload)
            assertThat(awaitItem()).isEqualTo(ConfirmCaptureEvent.Close)
        }
        assertThat(pending.image.value).isNull()
        notices.messages.test { assertThat(awaitItem()).isEqualTo(UiText.StringResource(R.string.capture_queued)) }
    }

    @Test
    fun `a 409 shows the server's message and keeps the image`() = runTest {
        uploader.result = Result.Error(ApiError(DataError.Network.CONFLICT, "This exact image has already been uploaded."))
        val viewModel = confirm()

        viewModel.onAction(ConfirmCaptureAction.OnUpload)

        assertThat(viewModel.state.value.error).isEqualTo(UiText.DynamicString("This exact image has already been uploaded."))
        assertThat(viewModel.state.value.image).isNotNull().isSameInstanceAs(image)
        assertThat(viewModel.state.value.isUploading).isFalse()
        assertThat(pending.image.value).isSameInstanceAs(image)
    }

    @Test
    fun `no connection keeps the image for another try`() = runTest {
        uploader.result = Result.Error(ApiError(DataError.Network.NO_INTERNET))
        val viewModel = confirm()

        viewModel.onAction(ConfirmCaptureAction.OnUpload)
        assertThat(viewModel.state.value.error).isNotNull()
        assertThat(pending.image.value).isSameInstanceAs(image)

        uploader.result = Result.Success("intake")
        viewModel.onAction(ConfirmCaptureAction.OnUpload)
        assertThat(uploader.uploads).isEqualTo(2)
        assertThat(pending.image.value).isNull()
    }

    @Test
    fun `discard clears the image and closes without uploading`() = runTest {
        val viewModel = confirm()

        viewModel.events.test {
            viewModel.onAction(ConfirmCaptureAction.OnDiscard)
            assertThat(awaitItem()).isEqualTo(ConfirmCaptureEvent.Close)
        }
        assertThat(pending.image.value).isNull()
        assertThat(uploader.uploads).isEqualTo(0)
    }

    @Test
    fun `with nothing pending the confirmation closes at once`() = runTest {
        pending.clear()
        confirm().events.test { assertThat(awaitItem()).isEqualTo(ConfirmCaptureEvent.Close) }
    }

    @Test
    fun `a picked image is prepared, held and opened for confirmation`() = runTest {
        pending.clear()
        val preparer = FakePreparer()
        val viewModel = AddReceiptViewModel(preparer, pending, notices, FakeSessions(setOf(Permission.AddReceipts)))

        viewModel.events.test {
            viewModel.onAction(AddReceiptAction.OnOpen)
            viewModel.onAction(AddReceiptAction.OnPicked("content://image"))
            assertThat(awaitItem()).isEqualTo(AddReceiptEvent.OpenConfirm)
        }
        assertThat(pending.image.value).isNotNull()
        assertThat(viewModel.state.value.isSheetOpen).isFalse()
    }

    @Test
    fun `an unreadable image says so and opens nothing`() = runTest {
        pending.clear()
        val preparer = FakePreparer().apply { result = Result.Error(PrepareError.Unreadable) }
        val viewModel = AddReceiptViewModel(preparer, pending, notices, FakeSessions(setOf(Permission.AddReceipts)))

        viewModel.onAction(AddReceiptAction.OnPicked("content://broken"))

        assertThat(pending.image.value).isNull()
        notices.messages.test { assertThat(awaitItem()).isEqualTo(UiText.StringResource(R.string.capture_unreadable)) }
    }

    @Test
    fun `the action is offered only with AddReceipts, and scan only when the scanner exists`() = runTest {
        assertThat(AddReceiptViewModel(FakePreparer(), pending, notices, FakeSessions(emptySet())).state.value.canAdd).isFalse()

        val viewModel = AddReceiptViewModel(FakePreparer(), pending, notices, FakeSessions(setOf(Permission.AddReceipts)))
        assertThat(viewModel.state.value.canAdd).isTrue()
        viewModel.onAction(AddReceiptAction.OnScanSupport(false))
        assertThat(viewModel.state.value.canScan).isFalse()
    }
}
