/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.presentation.gate

import app.cash.turbine.test
import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isNotNull
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.session.HouseholdState
import dev.jelshoj.kvitteria.core.domain.session.ProbeError
import dev.jelshoj.kvitteria.core.domain.session.ServerProbe
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.ServerVersion
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import dev.jelshoj.kvitteria.core.domain.session.PublicUpdateStatus
import dev.jelshoj.kvitteria.core.domain.session.UpdateRepository
import dev.jelshoj.kvitteria.core.domain.session.UpdateState
import dev.jelshoj.kvitteria.core.domain.session.UpdateStatus
import java.time.Clock
import java.time.Instant
import java.time.ZoneId
import java.time.ZoneOffset
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.test.TestCoroutineScheduler
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test

private class FakeSettings(private val stored: String?) : ServerSettings {
    override val address: StateFlow<String?> = MutableStateFlow(stored)

    override suspend fun load(): String? = stored

    override suspend fun save(address: String) = Unit
}

private class FakeProbe(
    var version: Result<ServerVersion, ProbeError> = Result.Success(ServerVersion("1.0.0", "abc")),
    var household: HouseholdState = HouseholdState.Claimed,
) : ServerProbe {
    override suspend fun version(origin: String?) = version

    override suspend fun household() = household
}

private class FakeSessions(
    var stored: StoredSession? = null,
    var fresh: SessionState = SessionState.Ended,
) : SessionManager {
    override val session = MutableStateFlow(stored)
    override val member = MutableStateFlow<CurrentMember?>(null)
    var cleared = false

    override suspend fun load() = stored

    override suspend fun adopt(session: StoredSession) = Unit

    override fun setMember(member: CurrentMember?) = Unit

    override suspend fun ensureFresh() = fresh

    override suspend fun refresh(spentAccessToken: String) = fresh

    override suspend fun signOut() = Unit

    override suspend fun clear() {
        cleared = true
        stored = null
    }
}

private class FakeMembers(var current: Result<CurrentMember, ApiError>) : MemberRepository {
    override suspend fun refreshCurrent() = current

    override suspend fun members(): Result<List<Member>, ApiError> = Result.Success(emptyList())
}

private class FakeUpdates(var public: PublicUpdateStatus? = null) : UpdateRepository {
    override suspend fun status(): Result<UpdateStatus, ApiError> = Result.Error(ApiError(DataError.Network.NOT_FOUND))

    override suspend fun start(): Result<Unit, ApiError> = Result.Success(Unit)

    override suspend fun publicStatus() = public
}

@OptIn(ExperimentalCoroutinesApi::class)
class GateViewModelTest {
    private val session = StoredSession("a", "r", Instant.parse("2026-08-26T12:00:00Z"), "you@household.dk")
    private val member = CurrentMember("u", "you@household.dk", "You", Role.Admin, MemberStatus.Active, emptySet())
    private val address = "http://10.0.2.2:8080"
    private val now = Instant.parse("2026-11-02T12:00:00Z")
    private val down = Result.Error(ProbeError.NOT_KVITTERIA)
    private val up = Result.Success(ServerVersion("1.2.0", "abc"))

    private fun status(state: UpdateState, updatedAt: Instant = now.minusSeconds(60)) =
        PublicUpdateStatus(state, "1.2.0", updatedAt)

    private val scheduler = TestCoroutineScheduler()

    /** Follows virtual time, so a wait left running at the end of a test goes stale and ends by itself. */
    private val clock = object : Clock() {
        override fun getZone(): ZoneId = ZoneOffset.UTC

        override fun withZone(zone: ZoneId): Clock = this

        override fun instant(): Instant = now.plusMillis(scheduler.currentTime)
    }

    @BeforeEach
    fun setUp() {
        Dispatchers.setMain(UnconfinedTestDispatcher(scheduler))
    }

    @AfterEach
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun gate(
        settings: ServerSettings = FakeSettings(address),
        probe: ServerProbe = FakeProbe(),
        sessions: SessionManager = FakeSessions(),
        members: MemberRepository = FakeMembers(Result.Success(member)),
        updates: UpdateRepository = FakeUpdates(),
    ) = GateViewModel(settings, sessions, probe, members, updates, clock)

    private suspend fun GateViewModel.nextEvent(): GateEvent {
        var event: GateEvent? = null
        events.test { event = awaitItem() }
        return event!!
    }

    @Test
    fun `no stored server goes to server entry`() = runTest {
        assertThat(gate(settings = FakeSettings(null)).nextEvent()).isEqualTo(GateEvent.GoToServer)
    }

    @Test
    fun `a failed version check stays on the gate and doesn't sign out`() = runTest {
        val sessions = FakeSessions(session, SessionState.Active(session))
        val viewModel = gate(probe = FakeProbe(version = Result.Error(ProbeError.UNREACHABLE)), sessions = sessions)

        assertThat(viewModel.state.value.unreachableAddress).isEqualTo(address)
        assertThat(sessions.cleared).isEqualTo(false)
    }

    @Test
    fun `no session on an unclaimed household goes to claim`() = runTest {
        val viewModel = gate(probe = FakeProbe(household = HouseholdState.Unclaimed))
        assertThat(viewModel.nextEvent()).isEqualTo(GateEvent.GoToClaim)
    }

    @Test
    fun `no session on a claimed household goes to sign-in`() = runTest {
        assertThat(gate().nextEvent()).isEqualTo(GateEvent.GoToSignIn)
    }

    @Test
    fun `an unreadable report goes to sign-in`() = runTest {
        assertThat(gate(probe = FakeProbe(household = HouseholdState.Unknown)).nextEvent()).isEqualTo(GateEvent.GoToSignIn)
    }

    @Test
    fun `a member opens the tabs`() = runTest {
        val viewModel = gate(sessions = FakeSessions(session, SessionState.Active(session)))
        assertThat(viewModel.nextEvent()).isEqualTo(GateEvent.GoToMain)
    }

    @Test
    fun `a rejected refresh goes back to the claim check`() = runTest {
        val viewModel = gate(
            sessions = FakeSessions(session, SessionState.Ended),
            probe = FakeProbe(household = HouseholdState.Unclaimed),
        )
        assertThat(viewModel.nextEvent()).isEqualTo(GateEvent.GoToClaim)
    }

    @Test
    fun `a refresh that can't reach the server keeps the session and offers a retry`() = runTest {
        val sessions = FakeSessions(session, SessionState.Unreachable)
        val viewModel = gate(sessions = sessions)

        assertThat(viewModel.state.value.unreachableAddress).isEqualTo(address)
        assertThat(sessions.cleared).isEqualTo(false)
    }

    @Test
    fun `401 clears the session and restarts at the claim check`() = runTest {
        val sessions = FakeSessions(session, SessionState.Active(session))
        val viewModel = gate(
            sessions = sessions,
            probe = FakeProbe(household = HouseholdState.Unclaimed),
            members = FakeMembers(Result.Error(ApiError(DataError.Network.UNAUTHORIZED))),
        )

        assertThat(viewModel.nextEvent()).isEqualTo(GateEvent.GoToClaim)
        assertThat(sessions.cleared).isEqualTo(true)
    }

    @Test
    fun `403 goes to rejected and keeps the session`() = runTest {
        val sessions = FakeSessions(session, SessionState.Active(session))
        val viewModel = gate(
            sessions = sessions,
            members = FakeMembers(Result.Error(ApiError(DataError.Network.FORBIDDEN))),
        )

        assertThat(viewModel.nextEvent()).isEqualTo(GateEvent.GoToRejected)
        assertThat(sessions.cleared).isEqualTo(false)
    }

    @Test
    fun `a member lookup that can't reach the server stays on the gate`() = runTest {
        val viewModel = gate(
            sessions = FakeSessions(session, SessionState.Active(session)),
            members = FakeMembers(Result.Error(ApiError(DataError.Network.NO_INTERNET))),
        )
        assertThat(viewModel.state.value.unreachableAddress).isNotNull()
    }

    @Test
    fun `no update status means the ordinary retry`() = runTest {
        val viewModel = gate(probe = FakeProbe(version = down), updates = FakeUpdates(null))

        assertThat(viewModel.state.value.unreachableAddress).isEqualTo(address)
        assertThat(viewModel.state.value.updating).isNull()
        assertThat(viewModel.state.value.restoreRequired).isEqualTo(false)
    }

    @Test
    fun `a running update is waited out rather than offered a retry`() = runTest {
        val viewModel = gate(probe = FakeProbe(version = down), updates = FakeUpdates(status(UpdateState.Running)))

        assertThat(viewModel.state.value.updating).isEqualTo(GateUpdating("1.2.0"))
        assertThat(viewModel.state.value.unreachableAddress).isNull()
    }

    @Test
    fun `the wait ends by itself and keeps the session`() = runTest {
        val probe = FakeProbe(version = down)
        val sessions = FakeSessions(session, SessionState.Active(session))
        val viewModel = gate(probe = probe, sessions = sessions, updates = FakeUpdates(status(UpdateState.Running)))

        probe.version = up
        advanceTimeBy(UPDATE_RECHECK_MS + 1)

        assertThat(viewModel.nextEvent()).isEqualTo(GateEvent.GoToMain)
        assertThat(viewModel.state.value.updating).isNull()
        assertThat(sessions.cleared).isEqualTo(false)
    }

    @Test
    fun `the wait keeps going through a status that can't be read`() = runTest {
        val updates = FakeUpdates(status(UpdateState.Running))
        val viewModel = gate(probe = FakeProbe(version = down), updates = updates)

        updates.public = null
        advanceTimeBy(UPDATE_RECHECK_MS + 1)

        assertThat(viewModel.state.value.updating).isEqualTo(GateUpdating("1.2.0"))
    }

    @Test
    fun `an update that finishes badly during the wait stops the wait`() = runTest {
        val updates = FakeUpdates(status(UpdateState.Running))
        val viewModel = gate(probe = FakeProbe(version = down), updates = updates)

        updates.public = status(UpdateState.RestoreRequired)
        advanceTimeBy(UPDATE_RECHECK_MS + 1)

        assertThat(viewModel.state.value.updating).isNull()
        assertThat(viewModel.state.value.restoreRequired).isEqualTo(true)
    }

    @Test
    fun `a wait that outlives the half hour falls back to the retry`() = runTest {
        val viewModel = gate(probe = FakeProbe(version = down), updates = FakeUpdates(status(UpdateState.Running)))

        advanceTimeBy(31 * 60_000L)

        assertThat(viewModel.state.value.updating).isNull()
        assertThat(viewModel.state.value.unreachableAddress).isEqualTo(address)
    }

    @Test
    fun `a finished update with the server still down is the ordinary retry`() = runTest {
        val viewModel = gate(probe = FakeProbe(version = down), updates = FakeUpdates(status(UpdateState.Succeeded)))

        assertThat(viewModel.state.value.unreachableAddress).isEqualTo(address)
        assertThat(viewModel.state.value.updating).isNull()
    }

    @Test
    fun `a running state over half an hour old is the ordinary retry`() = runTest {
        val stale = status(UpdateState.Running, updatedAt = now.minusSeconds(31 * 60))
        val viewModel = gate(probe = FakeProbe(version = down), updates = FakeUpdates(stale))

        assertThat(viewModel.state.value.unreachableAddress).isEqualTo(address)
        assertThat(viewModel.state.value.updating).isNull()
    }

    @Test
    fun `a needed restore is explained and keeps the session`() = runTest {
        val sessions = FakeSessions(session, SessionState.Active(session))
        val viewModel = gate(
            probe = FakeProbe(version = down),
            sessions = sessions,
            updates = FakeUpdates(status(UpdateState.RestoreRequired)),
        )

        assertThat(viewModel.state.value.restoreRequired).isEqualTo(true)
        assertThat(viewModel.state.value.unreachableAddress).isNull()
        assertThat(sessions.cleared).isEqualTo(false)
    }

    @Test
    fun `retry after a restore carries on once the server answers`() = runTest {
        val probe = FakeProbe(version = down)
        val viewModel = gate(probe = probe, updates = FakeUpdates(status(UpdateState.RestoreRequired)))

        probe.version = up
        viewModel.onAction(GateAction.OnRetry)

        assertThat(viewModel.state.value.restoreRequired).isEqualTo(false)
        assertThat(viewModel.nextEvent()).isEqualTo(GateEvent.GoToSignIn)
    }

    @Test
    fun `an answering server is not asked about updates`() = runTest {
        val viewModel = gate(updates = FakeUpdates(status(UpdateState.RestoreRequired)))

        assertThat(viewModel.nextEvent()).isEqualTo(GateEvent.GoToSignIn)
        assertThat(viewModel.state.value.restoreRequired).isEqualTo(false)
    }

    @Test
    fun `retry resolves again`() = runTest {
        val probe = FakeProbe(version = Result.Error(ProbeError.UNREACHABLE))
        val viewModel = gate(probe = probe)
        assertThat(viewModel.state.value.unreachableAddress).isNotNull()

        probe.version = Result.Success(ServerVersion("1.0.0", "abc"))
        viewModel.onAction(GateAction.OnRetry)

        assertThat(viewModel.state.value.unreachableAddress).isNull()
        assertThat(viewModel.nextEvent()).isEqualTo(GateEvent.GoToSignIn)
    }
}
