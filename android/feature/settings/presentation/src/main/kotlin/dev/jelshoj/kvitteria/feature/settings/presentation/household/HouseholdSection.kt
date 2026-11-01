/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.presentation.household

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.designsystem.KvAvatar
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCallout
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvDialog
import dev.jelshoj.kvitteria.core.designsystem.KvElevation
import dev.jelshoj.kvitteria.core.designsystem.KvField
import dev.jelshoj.kvitteria.core.designsystem.KvNote
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvSegmented
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvTag
import dev.jelshoj.kvitteria.core.designsystem.KvTagStyle
import dev.jelshoj.kvitteria.core.designsystem.KvToastBox
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.kvElevation
import dev.jelshoj.kvitteria.core.designsystem.rememberKvToastState
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.Initials
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.Loadable
import dev.jelshoj.kvitteria.core.presentation.LoadableContent
import dev.jelshoj.kvitteria.core.presentation.Loader
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.labelRes
import dev.jelshoj.kvitteria.core.presentation.refreshIfForbidden
import dev.jelshoj.kvitteria.core.presentation.toUiText
import dev.jelshoj.kvitteria.feature.settings.domain.HouseholdRepository
import dev.jelshoj.kvitteria.feature.settings.presentation.R
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel

/** The invite and manage dialog. An [id] of null is an invite. */
data class MemberDraft(
    val id: String? = null,
    val email: String = "",
    val name: String = "",
    val role: Role = Role.Member,
    val status: MemberStatus = MemberStatus.Active,
    val wasPending: Boolean = false,
    val isBusy: Boolean = false,
    val error: UiText? = null,
)

data class HouseholdState(
    val members: Loadable<List<Member>> = Loadable(),
    val meId: String? = null,
    val canManage: Boolean = false,
    val draft: MemberDraft? = null,
)

sealed interface HouseholdAction {
    data object OnInvite : HouseholdAction
    data class OnManage(val member: Member) : HouseholdAction
    data class OnDraftChange(val draft: MemberDraft) : HouseholdAction
    data object OnDismissDraft : HouseholdAction
    data object OnSaveDraft : HouseholdAction
}

class HouseholdViewModel(
    private val household: HouseholdRepository,
    private val members: MemberRepository,
    sessions: SessionManager,
) : ViewModel() {
    private val loader = Loader(viewModelScope) { members.members() }
    private val draft = MutableStateFlow<MemberDraft?>(null)

    val state = combine(loader.state, sessions.member, draft) { list, member, currentDraft ->
        HouseholdState(list, member?.id, member?.can(Permission.ManageUsers) == true, currentDraft)
    }.stateIn(viewModelScope, SharingStarted.Eagerly, HouseholdState())

    private val notices = Channel<UiText>(Channel.BUFFERED)
    val messages = merge(loader.failures, notices.receiveAsFlow())

    fun refresh() = loader.refresh()

    fun onAction(action: HouseholdAction) {
        when (action) {
            HouseholdAction.OnInvite -> draft.value = MemberDraft()
            is HouseholdAction.OnManage -> draft.value = MemberDraft(
                id = action.member.id,
                email = action.member.email,
                name = action.member.displayName,
                role = action.member.role,
                status = if (action.member.status == MemberStatus.Disabled) MemberStatus.Disabled else MemberStatus.Active,
                wasPending = action.member.status == MemberStatus.Pending,
            )
            is HouseholdAction.OnDraftChange -> draft.value = action.draft.copy(error = null)
            HouseholdAction.OnDismissDraft -> draft.value = null
            HouseholdAction.OnSaveDraft -> save()
        }
    }

    private fun save() {
        val current = draft.value ?: return
        if (current.isBusy) return
        if (current.id == null && !EMAIL.matches(current.email.trim())) {
            draft.update { it?.copy(error = UiText.StringResource(R.string.invalid_email)) }
            return
        }

        draft.update { it?.copy(isBusy = true, error = null) }
        viewModelScope.launch {
            val result = if (current.id == null) {
                household.invite(current.email, current.name, current.role)
            } else {
                // A pending member stays pending unless disabled: they become active by signing in.
                val status = if (current.wasPending && current.status == MemberStatus.Active) null else current.status
                household.updateMember(current.id, current.name, current.role, status)
            }
            when (result) {
                is Result.Error -> {
                    // A taken address or the last admin: the server's message belongs in the dialog.
                    draft.update { it?.copy(isBusy = false, error = result.error.toUiText()) }
                    members.refreshIfForbidden(result.error)
                }
                is Result.Success -> {
                    draft.value = null
                    notices.send(
                        if (current.id == null) {
                            UiText.StringResource(R.string.invited_toast, listOf(result.data.email))
                        } else {
                            UiText.StringResource(R.string.updated_toast, listOf(result.data.displayName))
                        },
                    )
                    members.refreshCurrent()
                    loader.refresh()
                }
            }
        }
    }

    private companion object {
        val EMAIL = Regex("""^[^@\s]+@[^@\s]+\.[^@\s]+$""")
    }
}

@Composable
fun HouseholdRoot(viewModel: HouseholdViewModel = koinViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.messages) { toast.show(it.asString(context)) }

    KvToastBox(toast) {
        HouseholdSection(state, viewModel::refresh, viewModel::onAction)
    }
}

@Composable
private fun statusLabel(status: MemberStatus): String = stringResource(
    when (status) {
        MemberStatus.Pending -> R.string.status_pending
        MemberStatus.Disabled -> R.string.status_disabled
        else -> R.string.status_active
    },
)

@Composable
private fun MemberRow(member: Member, isMe: Boolean, canManage: Boolean, onManage: () -> Unit) {
    val shape = RoundedCornerShape(KvRadius.md)
    val active = member.status == MemberStatus.Active
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .kvElevation(KvElevation.Small, shape)
            .background(KvColors.surface, shape)
            .padding(KvSpace.s4),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(KvSpace.s4),
    ) {
        KvAvatar(Initials.from(member.displayName), accented = active)
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                BasicText(member.displayName, style = KvType.control)
                if (isMe) BasicText(stringResource(R.string.you).uppercase(), style = KvType.kicker)
            }
            BasicText(member.email, style = KvType.hint, maxLines = 1, overflow = TextOverflow.Ellipsis)
            BasicText(
                member.lastSeenAt?.let { stringResource(R.string.last_seen, DanishTime.date(it)) }
                    ?: stringResource(R.string.never_seen),
                style = KvType.hint.copy(color = KvColors.neutral600),
            )
            Row(horizontalArrangement = Arrangement.spacedBy(5.dp), modifier = Modifier.padding(top = KvSpace.s1)) {
                KvTag(
                    stringResource(member.role.labelRes()),
                    style = if (member.role == Role.Admin) KvTagStyle.Accent else KvTagStyle.Neutral,
                )
                KvTag(statusLabel(member.status), style = if (active) KvTagStyle.Outline else KvTagStyle.Neutral)
            }
        }
        if (canManage) {
            KvButton(
                stringResource(CoreR.string.manage),
                onManage,
                style = KvButtonStyle.Ghost,
                glyph = PhosphorIcons.SlidersHorizontal,
            )
        }
    }
}

@Composable
fun HouseholdSection(
    state: HouseholdState,
    onRefresh: () -> Unit,
    onAction: (HouseholdAction) -> Unit,
) {
    LoadableContent(state.members, onRefresh) { members ->
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = androidx.compose.foundation.layout.PaddingValues(
                horizontal = KvSpace.gutter,
                vertical = KvSpace.s3,
            ),
            verticalArrangement = Arrangement.spacedBy(9.dp),
        ) {
            item {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    BasicText(
                        if (members.size == 1) stringResource(R.string.members_summary_one) else stringResource(R.string.members_summary, members.size),
                        style = KvType.label.copy(color = KvColors.neutral400),
                    )
                    if (state.canManage) {
                        KvButton(
                            stringResource(R.string.invite),
                            { onAction(HouseholdAction.OnInvite) },
                            style = KvButtonStyle.Primary,
                            glyph = PhosphorIcons.UserPlus,
                        )
                    }
                }
            }
            items(members, key = { it.id }) { member ->
                MemberRow(member, member.id == state.meId, state.canManage) { onAction(HouseholdAction.OnManage(member)) }
            }
            item {
                BasicText(stringResource(R.string.invite_note), style = KvType.label.copy(color = KvColors.neutral500))
            }
        }
    }

    state.draft?.let { draft -> MemberDialog(draft, onAction) }
}

@Composable
private fun MemberDialog(draft: MemberDraft, onAction: (HouseholdAction) -> Unit) {
    val invite = draft.id == null
    KvDialog(
        title = stringResource(if (invite) R.string.invite else R.string.manage_member),
        onDismiss = { onAction(HouseholdAction.OnDismissDraft) },
        actions = {
            KvButton(
                stringResource(CoreR.string.cancel),
                { onAction(HouseholdAction.OnDismissDraft) },
                style = KvButtonStyle.Ghost,
            )
            KvButton(
                stringResource(if (invite) R.string.invite else CoreR.string.save),
                { onAction(HouseholdAction.OnSaveDraft) },
                style = KvButtonStyle.Primary,
                busy = draft.isBusy,
            )
        },
    ) {
        if (invite) {
            KvNote(stringResource(R.string.invite_explain))
            KvField(
                label = stringResource(R.string.member_email),
                value = draft.email,
                onValueChange = { onAction(HouseholdAction.OnDraftChange(draft.copy(email = it))) },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, autoCorrectEnabled = false),
                modifier = Modifier.fillMaxWidth(),
            )
        }
        KvField(
            label = stringResource(R.string.display_name),
            value = draft.name,
            onValueChange = { onAction(HouseholdAction.OnDraftChange(draft.copy(name = it))) },
            modifier = Modifier.fillMaxWidth(),
        )
        Column(verticalArrangement = Arrangement.spacedBy(5.dp)) {
            BasicText(stringResource(R.string.role), style = KvType.label)
            KvSegmented(
                options = listOf(stringResource(CoreR.string.role_admin), stringResource(CoreR.string.role_member)),
                selected = if (draft.role == Role.Admin) 0 else 1,
                onSelect = {
                    onAction(HouseholdAction.OnDraftChange(draft.copy(role = if (it == 0) Role.Admin else Role.Member)))
                },
                modifier = Modifier.fillMaxWidth(),
            )
        }
        if (!invite) {
            Column(verticalArrangement = Arrangement.spacedBy(5.dp)) {
                BasicText(stringResource(R.string.access), style = KvType.label)
                KvSegmented(
                    options = listOf(stringResource(R.string.status_active), stringResource(R.string.status_disabled)),
                    selected = if (draft.status == MemberStatus.Disabled) 1 else 0,
                    onSelect = {
                        val status = if (it == 1) MemberStatus.Disabled else MemberStatus.Active
                        onAction(HouseholdAction.OnDraftChange(draft.copy(status = status)))
                    },
                    modifier = Modifier.fillMaxWidth(),
                )
            }
        }
        draft.error?.let { KvCallout(it.asString()) }
    }
}
