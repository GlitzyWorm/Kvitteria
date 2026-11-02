/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.presentation.account

import app.cash.turbine.test
import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.session.ProbeError
import dev.jelshoj.kvitteria.core.domain.session.PublicUpdateStatus
import dev.jelshoj.kvitteria.core.domain.session.HouseholdState
import dev.jelshoj.kvitteria.core.domain.session.ServerProbe
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.ServerVersion
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import dev.jelshoj.kvitteria.core.domain.session.UpdateRepository
import dev.jelshoj.kvitteria.core.domain.session.UpdateState
import dev.jelshoj.kvitteria.core.domain.session.UpdateStatus
import dev.jelshoj.kvitteria.core.presentation.UiText
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test

private class FakeSettings : ServerSettings {
    override val address: StateFlow<String?> = MutableStateFlow("http://server.test")

    override suspend fun load(): String? = address.value

    override suspend fun save(address: String) = Unit
}

private class FakeProbe(var version: Result<ServerVersion, ProbeError>) : ServerProbe {
    override suspend fun version(origin: String?) = version

    override suspend fun household() = HouseholdState.Claimed
}

private class FakeSessions : SessionManager {
    override val session = MutableStateFlow<StoredSession?>(null)
    override val member = MutableStateFlow<CurrentMember?>(null)

    override suspend fun load(): StoredSession? = null

    override suspend fun adopt(session: StoredSession) = Unit

    override fun setMember(member: CurrentMember?) = Unit

    override suspend fun ensureFresh() = SessionState.Ended

    override suspend fun refresh(spentAccessToken: String) = SessionState.Ended

    override suspend fun signOut() = Unit

    override suspend fun clear() = Unit
}

private class FakeMembers : MemberRepository {
    override suspend fun refreshCurrent(): Result<CurrentMember, ApiError> = Result.Error(ApiError(DataError.Network.UNKNOWN))

    override suspend fun members(): Result<List<Member>, ApiError> = Result.Success(emptyList())
}

private class FakeUpdates(
    var status: Result<UpdateStatus, ApiError>,
    var public: PublicUpdateStatus? = null,
    var startResult: EmptyResult<ApiError> = Result.Success(Unit),
) : UpdateRepository {
    var starts = 0

    override suspend fun status() = status

    override suspend fun start(): EmptyResult<ApiError> {
        starts++
        return startResult
    }

    override suspend fun publicStatus() = public
}

@OptIn(ExperimentalCoroutinesApi::class)
class AccountViewModelTest {
    private val now = Instant.parse("2026-11-02T12:00:00Z")
    private val available = UpdateStatus("1.2.0", true, "https://x/1.2.0", canStart = true, state = null, detail = null)
    private val old = Result.Success(ServerVersion("1.1.0", "abc"))
    private val new = Result.Success(ServerVersion("1.2.0", "abc"))
    private val down = Result.Error(ProbeError.NOT_KVITTERIA)

    private lateinit var probe: FakeProbe
    private lateinit var updates: FakeUpdates

    private fun public(state: UpdateState, updatedAt: Instant = now) = PublicUpdateStatus(state, "1.2.0", updatedAt)

    @BeforeEach
    fun setUp() {
        Dispatchers.setMain(UnconfinedTestDispatcher())
        probe = FakeProbe(old)
        updates = FakeUpdates(Result.Success(available))
    }

    @AfterEach
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun viewModel() = AccountViewModel(
        FakeSessions(),
        FakeSettings(),
        probe,
        FakeMembers(),
        updates,
        Clock.fixed(now, ZoneOffset.UTC),
    ).apply { refresh() }

    private fun AccountViewModel.start() = onAction(AccountAction.OnStartUpdate)

    @Test
    fun `an available update is loaded`() = runTest {
        assertThat(viewModel().state.value.update).isEqualTo(available)
    }

    @Test
    fun `a status that can't be loaded hides the row and keeps the rest`() = runTest {
        updates.status = Result.Error(ApiError(DataError.Network.NOT_FOUND))
        val viewModel = viewModel()

        assertThat(viewModel.state.value.update).isNull()
        assertThat(viewModel.state.value.serverVersion).isEqualTo("1.1.0")
    }

    @Test
    fun `the update action asks first and starts nothing by itself`() = runTest {
        val viewModel = viewModel()
        viewModel.onAction(AccountAction.OnConfirm(AccountConfirm.Update))

        assertThat(viewModel.state.value.confirm).isEqualTo(AccountConfirm.Update)
        assertThat(updates.starts).isEqualTo(0)
    }

    @Test
    fun `confirming starts the update and follows it`() = runTest {
        val viewModel = viewModel()
        viewModel.onAction(AccountAction.OnConfirm(AccountConfirm.Update))
        viewModel.start()

        assertThat(updates.starts).isEqualTo(1)
        assertThat(viewModel.state.value.confirm).isNull()
        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Following("1.2.0"))
    }

    @Test
    fun `a refusal shows the server's message and changes nothing else`() = runTest {
        updates.startResult = Result.Error(ApiError(DataError.Network.CONFLICT, "An update is already running."))
        val viewModel = viewModel()
        viewModel.start()

        assertThat(viewModel.state.value.updateError).isEqualTo(UiText.DynamicString("An update is already running."))
        assertThat(viewModel.state.value.updateRun).isNull()
        assertThat(viewModel.state.value.update).isEqualTo(available)
    }

    @Test
    fun `the old server answering isn't an outcome`() = runTest {
        val viewModel = viewModel()
        viewModel.start()
        updates.public = public(UpdateState.Running)

        advanceTimeBy(UPDATE_POLL_MS * 5)

        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Following("1.2.0"))
    }

    @Test
    fun `the target version answering finishes it`() = runTest {
        val viewModel = viewModel()
        viewModel.start()
        updates.public = public(UpdateState.Running)
        advanceTimeBy(UPDATE_POLL_MS + 1)

        probe.version = down
        advanceTimeBy(UPDATE_POLL_MS)
        probe.version = new
        updates.status = Result.Success(available.copy(updateAvailable = false))
        advanceTimeBy(UPDATE_POLL_MS)

        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Finished("1.2.0"))
        assertThat(viewModel.state.value.serverVersion).isEqualTo("1.2.0")
        assertThat(viewModel.state.value.update?.updateAvailable).isEqualTo(false)
    }

    @Test
    fun `a rollback ends progress with the reason`() = runTest {
        val viewModel = viewModel()
        viewModel.start()
        updates.status = Result.Success(available.copy(state = UpdateState.RolledBack, detail = "1.2.0 did not become healthy"))
        updates.public = public(UpdateState.RolledBack)

        advanceTimeBy(UPDATE_POLL_MS + 1)

        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Failed("1.2.0 did not become healthy"))
    }

    @Test
    fun `a failure ends progress`() = runTest {
        val viewModel = viewModel()
        viewModel.start()
        updates.status = Result.Error(ApiError(DataError.Network.NO_INTERNET))
        updates.public = public(UpdateState.Failed)

        advanceTimeBy(UPDATE_POLL_MS + 1)

        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Failed(null))
    }

    @Test
    fun `an outcome left by an earlier try is not this one's`() = runTest {
        updates.public = public(UpdateState.RolledBack, updatedAt = now.minusSeconds(3_600))
        val viewModel = viewModel()
        viewModel.start()

        advanceTimeBy(UPDATE_POLL_MS * 3)

        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Following("1.2.0"))
    }

    @Test
    fun `a needed restore goes to the gate`() = runTest {
        val viewModel = viewModel()
        viewModel.events.test {
            viewModel.start()
            updates.public = public(UpdateState.RestoreRequired)
            advanceTimeBy(UPDATE_POLL_MS + 1)

            assertThat(awaitItem()).isEqualTo(AccountEvent.GoToGate)
        }
        assertThat(viewModel.state.value.updateRun).isNull()
    }

    @Test
    fun `an agent that never starts is reported after a minute`() = runTest {
        val viewModel = viewModel()
        viewModel.start()

        advanceTimeBy(UPDATE_START_LIMIT_MS - UPDATE_POLL_MS)
        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Following("1.2.0"))

        advanceTimeBy(UPDATE_POLL_MS + 1)
        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.NotStarted)
    }

    @Test
    fun `a slow update that has started is not timed out at a minute`() = runTest {
        val viewModel = viewModel()
        viewModel.start()
        updates.public = public(UpdateState.Running)

        advanceTimeBy(UPDATE_START_LIMIT_MS * 3)

        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Following("1.2.0"))
    }

    @Test
    fun `a server that vanishes doesn't leave it following for ever`() = runTest {
        val viewModel = viewModel()
        viewModel.start()
        updates.public = public(UpdateState.Running)
        advanceTimeBy(UPDATE_POLL_MS + 1)

        probe.version = down
        updates.public = null
        advanceTimeBy(UPDATE_FOLLOW_LIMIT_MS)

        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Failed(null))
    }

    @Test
    fun `a run in an unreadable state keeps following`() = runTest {
        val viewModel = viewModel()
        viewModel.start()
        updates.public = public(UpdateState.Running)
        advanceTimeBy(UPDATE_POLL_MS + 1)

        updates.public = public(UpdateState.Unknown, updatedAt = now.plusSeconds(10))
        advanceTimeBy(UPDATE_START_LIMIT_MS * 2)

        assertThat(viewModel.state.value.updateRun).isEqualTo(UpdateRun.Following("1.2.0"))
    }
}
