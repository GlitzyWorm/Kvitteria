/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.presentation.mailboxes

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.designsystem.KvAvatar
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCallout
import dev.jelshoj.kvitteria.core.designsystem.KvCard
import dev.jelshoj.kvitteria.core.designsystem.KvCheckbox
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvDialog
import dev.jelshoj.kvitteria.core.designsystem.KvEyebrow
import dev.jelshoj.kvitteria.core.designsystem.KvField
import dev.jelshoj.kvitteria.core.designsystem.KvIcon
import dev.jelshoj.kvitteria.core.designsystem.KvNote
import dev.jelshoj.kvitteria.core.designsystem.KvSelectField
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvTag
import dev.jelshoj.kvitteria.core.designsystem.KvTagStyle
import dev.jelshoj.kvitteria.core.designsystem.KvToastBox
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.rememberKvToastState
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.core.domain.member.Initials
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.Loadable
import dev.jelshoj.kvitteria.core.presentation.LoadableContent
import dev.jelshoj.kvitteria.core.presentation.Loader
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.PickerRow
import dev.jelshoj.kvitteria.core.presentation.PickerSheet
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.refreshIfForbidden
import dev.jelshoj.kvitteria.core.presentation.toUiText
import dev.jelshoj.kvitteria.feature.settings.domain.Mailbox
import dev.jelshoj.kvitteria.feature.settings.domain.MailboxDraft
import dev.jelshoj.kvitteria.feature.settings.domain.MailboxDraftError
import dev.jelshoj.kvitteria.feature.settings.domain.MailboxDraftValidator
import dev.jelshoj.kvitteria.feature.settings.domain.MailboxRepository
import dev.jelshoj.kvitteria.feature.settings.presentation.R
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel

data class MailboxesData(val mailboxes: List<Mailbox>, val members: List<Member>)

data class MailboxForm(
    val draft: MailboxDraft,
    val isBusy: Boolean = false,
    val pickingOwner: Boolean = false,
    val error: UiText? = null,
)

data class MailboxesState(
    val data: Loadable<MailboxesData> = Loadable(),
    val meId: String = "",
    val canManage: Boolean = false,
    val form: MailboxForm? = null,
    val removing: Mailbox? = null,
)

sealed interface MailboxesAction {
    data object OnAdd : MailboxesAction
    data class OnEdit(val mailbox: Mailbox) : MailboxesAction
    data class OnDraftChange(val draft: MailboxDraft) : MailboxesAction
    data class OnPickingOwner(val picking: Boolean) : MailboxesAction
    data object OnDismissForm : MailboxesAction
    data object OnSave : MailboxesAction
    data class OnToggleEnabled(val mailbox: Mailbox) : MailboxesAction
    data class OnAskRemove(val mailbox: Mailbox?) : MailboxesAction
    data object OnRemove : MailboxesAction
}

class MailboxesViewModel(
    private val mailboxes: MailboxRepository,
    private val members: MemberRepository,
    sessions: SessionManager,
) : ViewModel() {
    private val loader = Loader(viewModelScope) {
        when (val list = mailboxes.mailboxes()) {
            is Result.Error -> list
            is Result.Success -> members.members().map { MailboxesData(list.data, it) }
        }
    }
    private val form = MutableStateFlow<MailboxForm?>(null)
    private val removing = MutableStateFlow<Mailbox?>(null)

    val state = combine(loader.state, sessions.member, form, removing) { data, member, currentForm, toRemove ->
        MailboxesState(data, member?.id.orEmpty(), member?.can(Permission.ManageMailboxes) == true, currentForm, toRemove)
    }.stateIn(viewModelScope, SharingStarted.Eagerly, MailboxesState())

    private val notices = Channel<UiText>(Channel.BUFFERED)
    val messages = merge(loader.failures, notices.receiveAsFlow())

    fun refresh() = loader.refresh()

    fun onAction(action: MailboxesAction) {
        when (action) {
            MailboxesAction.OnAdd -> form.value = MailboxForm(MailboxDraft(ownerId = state.value.meId))
            is MailboxesAction.OnEdit -> form.value = MailboxForm(action.mailbox.toDraft())
            is MailboxesAction.OnDraftChange -> form.update { it?.copy(draft = action.draft, error = null) }
            is MailboxesAction.OnPickingOwner -> form.update { it?.copy(pickingOwner = action.picking) }
            MailboxesAction.OnDismissForm -> form.value = null
            MailboxesAction.OnSave -> save()
            is MailboxesAction.OnToggleEnabled -> toggle(action.mailbox)
            is MailboxesAction.OnAskRemove -> removing.value = action.mailbox
            MailboxesAction.OnRemove -> remove()
        }
    }

    private fun Mailbox.toDraft() = MailboxDraft(
        id = id,
        ownerId = ownerId,
        host = host,
        port = port.toString(),
        useSsl = useSsl,
        folder = folder,
        username = username,
        isEnabled = isEnabled,
    )

    private fun MailboxDraftError.toUiText() = UiText.StringResource(
        when (this) {
            MailboxDraftError.REQUIRED_FIELDS -> R.string.mailbox_error_required_fields
            MailboxDraftError.PORT -> R.string.mailbox_error_port
            MailboxDraftError.PASSWORD_REQUIRED -> R.string.mailbox_error_password_required
            MailboxDraftError.NO_OWNER -> R.string.mailbox_error_no_owner
        },
    )

    private fun save() {
        val current = form.value ?: return
        if (current.isBusy) return
        val valid = MailboxDraftValidator.validate(current.draft)
        if (valid is Result.Error) {
            form.update { it?.copy(error = valid.error.toUiText()) }
            return
        }

        form.update { it?.copy(isBusy = true, error = null) }
        viewModelScope.launch {
            val result = if (current.draft.isEdit) mailboxes.update(current.draft) else mailboxes.create(current.draft)
            when (result) {
                is Result.Error -> {
                    // The server's own words, naming the folders that do exist. The form keeps its values.
                    form.update { it?.copy(isBusy = false, error = result.error.toUiText()) }
                    members.refreshIfForbidden(result.error)
                }
                is Result.Success -> {
                    form.value = null
                    notices.send(UiText.StringResource(R.string.mailbox_saved))
                    loader.refresh()
                }
            }
        }
    }

    private fun toggle(mailbox: Mailbox) {
        viewModelScope.launch {
            when (val result = mailboxes.setEnabled(mailbox, !mailbox.isEnabled)) {
                is Result.Error -> {
                    notices.send(result.error.toUiText())
                    members.refreshIfForbidden(result.error)
                }
                is Result.Success -> loader.refresh()
            }
        }
    }

    private fun remove() {
        val mailbox = removing.value ?: return
        removing.value = null
        viewModelScope.launch {
            when (val result = mailboxes.remove(mailbox.id)) {
                is Result.Error -> {
                    notices.send(result.error.toUiText())
                    members.refreshIfForbidden(result.error)
                }
                is Result.Success -> {
                    notices.send(UiText.StringResource(R.string.mailbox_removed))
                    loader.refresh()
                }
            }
        }
    }
}

@Composable
fun MailboxesRoot(viewModel: MailboxesViewModel = koinViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.messages) { toast.show(it.asString(context)) }

    KvToastBox(toast) {
        MailboxesSection(state, viewModel::refresh, viewModel::onAction)
    }
}

@Composable
private fun MailboxRow(mailbox: Mailbox, mine: Boolean, canManage: Boolean, onAction: (MailboxesAction) -> Unit) {
    KvCard(Modifier.fillMaxWidth()) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
            if (mine) {
                KvIcon(PhosphorIcons.EnvelopeSimple, contentDescription = null, size = 22.dp, tint = KvColors.accent)
            } else {
                KvAvatar(Initials.from(mailbox.ownerName))
            }
            Column(Modifier.weight(1f)) {
                BasicText(
                    if (mine) mailbox.folder else mailbox.ownerName,
                    style = KvType.control,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
                BasicText(
                    if (mine) mailbox.host else mailbox.folder,
                    style = KvType.mono,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
            KvTag(
                stringResource(if (mailbox.isEnabled) R.string.mailbox_active else R.string.mailbox_inactive),
                style = if (mailbox.isEnabled) KvTagStyle.Outline else KvTagStyle.Neutral,
            )
        }
        BasicText(
            mailbox.lastPolledAt?.let { stringResource(R.string.mailbox_last_polled, DanishTime.dateTime(it)) }
                ?: stringResource(R.string.mailbox_never_polled),
            style = KvType.hint,
        )
        // A mailbox whose password expired would otherwise look identical to one that works.
        mailbox.lastError?.let { KvCallout(it) }
        if (canManage) {
            Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s2), verticalAlignment = Alignment.CenterVertically) {
                KvButton(
                    stringResource(CoreR.string.manage),
                    { onAction(MailboxesAction.OnEdit(mailbox)) },
                    glyph = PhosphorIcons.PencilSimple,
                )
                KvButton(
                    stringResource(if (mailbox.isEnabled) R.string.mailbox_disable else R.string.mailbox_enable),
                    { onAction(MailboxesAction.OnToggleEnabled(mailbox)) },
                    style = KvButtonStyle.Ghost,
                )
                KvButton(
                    stringResource(CoreR.string.remove),
                    { onAction(MailboxesAction.OnAskRemove(mailbox)) },
                    style = KvButtonStyle.Ghost,
                    glyph = PhosphorIcons.Trash,
                )
            }
        }
    }
}

@Composable
fun MailboxesSection(
    state: MailboxesState,
    onRefresh: () -> Unit,
    onAction: (MailboxesAction) -> Unit,
) {
    LoadableContent(state.data, onRefresh) { data ->
        val (mine, others) = data.mailboxes.partition { it.ownerId == state.meId }
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
            verticalArrangement = Arrangement.spacedBy(KvSpace.s4),
        ) {
            KvEyebrow(stringResource(R.string.my_mailbox))
            if (mine.isEmpty()) {
                BasicText(stringResource(R.string.no_mailbox_yet), style = KvType.muted)
            }
            mine.forEach { MailboxRow(it, mine = true, state.canManage, onAction) }
            if (state.canManage) {
                KvButton(
                    stringResource(R.string.add_mailbox),
                    { onAction(MailboxesAction.OnAdd) },
                    style = KvButtonStyle.Primary,
                    glyph = PhosphorIcons.Plus,
                )
            }
            KvEyebrow(stringResource(R.string.household_mailboxes), Modifier.padding(top = KvSpace.s4))
            if (others.isEmpty()) {
                BasicText(stringResource(R.string.no_other_mailboxes), style = KvType.muted)
            }
            others.forEach { MailboxRow(it, mine = false, state.canManage, onAction) }
            if (!state.canManage) KvNote(stringResource(R.string.mailboxes_read_only))
        }

        state.form?.let { MailboxDialog(it, data.members, onAction) }
    }

    state.removing?.let { mailbox ->
        KvDialog(
            title = stringResource(R.string.confirm_remove_mailbox_title),
            onDismiss = { onAction(MailboxesAction.OnAskRemove(null)) },
            actions = {
                KvButton(
                    stringResource(CoreR.string.cancel),
                    { onAction(MailboxesAction.OnAskRemove(null)) },
                    style = KvButtonStyle.Ghost,
                )
                KvButton(
                    stringResource(CoreR.string.remove),
                    { onAction(MailboxesAction.OnRemove) },
                    style = KvButtonStyle.Primary,
                )
            },
        ) {
            BasicText(mailbox.folder, style = KvType.mono)
            BasicText(stringResource(R.string.confirm_remove_mailbox_message), style = KvType.small)
        }
    }
}

@Composable
private fun MailboxDialog(form: MailboxForm, members: List<Member>, onAction: (MailboxesAction) -> Unit) {
    val draft = form.draft
    val change: (MailboxDraft) -> Unit = { onAction(MailboxesAction.OnDraftChange(it)) }
    val owner = members.firstOrNull { it.id == draft.ownerId }?.displayName
        ?: stringResource(CoreR.string.unknown_member)

    KvDialog(
        title = stringResource(if (draft.isEdit) R.string.mailbox_edit else R.string.mailbox_new),
        onDismiss = { onAction(MailboxesAction.OnDismissForm) },
        actions = {
            KvButton(
                stringResource(CoreR.string.cancel),
                { onAction(MailboxesAction.OnDismissForm) },
                style = KvButtonStyle.Ghost,
            )
            KvButton(
                stringResource(CoreR.string.save),
                { onAction(MailboxesAction.OnSave) },
                style = KvButtonStyle.Primary,
                busy = form.isBusy,
            )
        },
    ) {
        KvSelectField(
            label = stringResource(R.string.belongs_to),
            value = owner,
            onClick = { onAction(MailboxesAction.OnPickingOwner(true)) },
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.host),
            value = draft.host,
            onValueChange = { change(draft.copy(host = it)) },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri, autoCorrectEnabled = false),
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.port),
            value = draft.port,
            onValueChange = { change(draft.copy(port = it)) },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            modifier = Modifier.fillMaxWidth(),
        )
        KvCheckbox(draft.useSsl, { change(draft.copy(useSsl = it)) }) {
            BasicText(stringResource(R.string.tls_label), style = KvType.small)
        }
        KvField(
            label = stringResource(R.string.folder),
            value = draft.folder,
            onValueChange = { change(draft.copy(folder = it)) },
            keyboardOptions = KeyboardOptions(autoCorrectEnabled = false),
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.username),
            value = draft.username,
            onValueChange = { change(draft.copy(username = it)) },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, autoCorrectEnabled = false),
            modifier = Modifier.fillMaxWidth(),
        )
        KvField(
            label = stringResource(R.string.password),
            value = draft.password,
            onValueChange = { change(draft.copy(password = it)) },
            hint = if (draft.isEdit) stringResource(R.string.password_hint_edit) else null,
            visualTransformation = PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
            modifier = Modifier.fillMaxWidth(),
        )
        KvCheckbox(draft.isEnabled, { change(draft.copy(isEnabled = it)) }) {
            BasicText(stringResource(R.string.enabled_label), style = KvType.small)
        }
        form.error?.let { KvCallout(it.asString()) }
    }

    if (form.pickingOwner) {
        PickerSheet(
            title = stringResource(R.string.belongs_to),
            rows = members.map { PickerRow.Option(it.id, it.displayName) },
            selected = draft.ownerId,
            onPick = {
                change(draft.copy(ownerId = it))
                onAction(MailboxesAction.OnPickingOwner(false))
            },
            onDismiss = { onAction(MailboxesAction.OnPickingOwner(false)) },
        )
    }
}
