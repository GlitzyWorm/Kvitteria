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
import java.time.Instant
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.test.UnconfinedTestDispatcher
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

@OptIn(ExperimentalCoroutinesApi::class)
class GateViewModelTest {
    private val session = StoredSession("a", "r", Instant.parse("2026-08-26T12:00:00Z"), "you@household.dk")
    private val member = CurrentMember("u", "you@household.dk", "You", Role.Admin, MemberStatus.Active, emptySet())
    private val address = "http://10.0.2.2:8080"

    @BeforeEach
    fun setUp() {
        Dispatchers.setMain(UnconfinedTestDispatcher())
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
    ) = GateViewModel(settings, sessions, probe, members)

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
