/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

@file:OptIn(ExperimentalLayoutApi::class)

package dev.jelshoj.kvitteria.feature.settings.presentation.account

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.designsystem.KvAvatar
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvDialog
import dev.jelshoj.kvitteria.core.designsystem.KvElevation
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvSegmented
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvSpinner
import dev.jelshoj.kvitteria.core.designsystem.KvTag
import dev.jelshoj.kvitteria.core.designsystem.KvTagStyle
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.kvElevation
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.member.Initials
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.session.ServerProbe
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.UPDATE_COMMAND
import dev.jelshoj.kvitteria.core.domain.session.UpdateRepository
import dev.jelshoj.kvitteria.core.domain.session.UpdateState
import dev.jelshoj.kvitteria.core.domain.session.UpdateStatus
import dev.jelshoj.kvitteria.core.presentation.AppLanguage
import dev.jelshoj.kvitteria.core.presentation.CommandLine
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.labelRes
import dev.jelshoj.kvitteria.core.presentation.toUiText
import dev.jelshoj.kvitteria.feature.settings.presentation.R
import dev.jelshoj.kvitteria.feature.settings.presentation.permissions.labelRes
import java.time.Clock
import java.time.Instant
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel

enum class AccountConfirm { SignOut, ChangeServer, Update }

/** An update started from here, from the confirmation to its outcome. */
sealed interface UpdateRun {
    data class Following(val targetVersion: String) : UpdateRun
    data class Finished(val version: String) : UpdateRun
    data class Failed(val detail: String?) : UpdateRun
    data object NotStarted : UpdateRun
}

data class AccountState(
    val serverAddress: String = "",
    val serverVersion: String? = null,
    /** Null hides the row, which covers a failed load and a server too old to have the endpoint. */
    val update: UpdateStatus? = null,
    val updateRun: UpdateRun? = null,
    val updateError: UiText? = null,
    val confirm: AccountConfirm? = null,
)

sealed interface AccountAction {
    data class OnConfirm(val confirm: AccountConfirm?) : AccountAction
    data object OnSignOut : AccountAction
    data object OnStartUpdate : AccountAction
}

sealed interface AccountEvent {
    data object GoToGate : AccountEvent
}

internal const val UPDATE_POLL_MS = 3_000L

/** The agent answers a request within seconds, so a minute of silence means it isn't there. */
internal const val UPDATE_START_LIMIT_MS = 60_000L

/** Past anything the script takes, so a server that vanishes doesn't leave this polling for ever. */
internal const val UPDATE_FOLLOW_LIMIT_MS = 35 * 60_000L

class AccountViewModel(
    private val sessions: SessionManager,
    private val settings: ServerSettings,
    private val probe: ServerProbe,
    private val members: MemberRepository,
    private val updates: UpdateRepository,
    private val clock: Clock,
) : ViewModel() {
    val member = sessions.member

    private val _state = MutableStateFlow(AccountState(serverAddress = settings.address.value.orEmpty()))
    val state = _state.asStateFlow()

    private val _events = Channel<AccountEvent>()
    val events = _events.receiveAsFlow()

    fun refresh() {
        viewModelScope.launch {
            members.refreshCurrent()
            val version = (probe.version() as? Result.Success)?.data?.version
            val update = (updates.status() as? Result.Success)?.data
            _state.update {
                it.copy(serverAddress = settings.load().orEmpty(), serverVersion = version, update = update)
            }
        }
    }

    fun onAction(action: AccountAction) {
        when (action) {
            is AccountAction.OnConfirm -> _state.update { it.copy(confirm = action.confirm) }
            AccountAction.OnSignOut -> viewModelScope.launch {
                _state.update { it.copy(confirm = null) }
                // Clearing the session is what returns the app to the gate.
                sessions.signOut()
            }
            AccountAction.OnStartUpdate -> viewModelScope.launch { startUpdate() }
        }
    }

    private suspend fun startUpdate() {
        val target = _state.value.update?.latestVersion
        _state.update { it.copy(confirm = null, updateError = null, updateRun = null) }
        if (target == null) return

        // A status left by an earlier try at this version would otherwise read as this one's outcome.
        val before = updates.publicStatus()?.updatedAt

        when (val result = updates.start()) {
            is Result.Error -> _state.update { it.copy(updateError = result.error.toUiText()) }
            is Result.Success -> {
                _state.update { it.copy(updateRun = UpdateRun.Following(target)) }
                follow(target, before)
            }
        }
    }

    /**
     * Stays here rather than handing over to the gate, because the old server keeps answering `/version`
     * while the image downloads: the gate would open the tabs and the server would vanish from under them.
     */
    private suspend fun follow(target: String, before: Instant?) {
        var waited = 0L
        var started = false

        while (waited < UPDATE_FOLLOW_LIMIT_MS) {
            delay(UPDATE_POLL_MS)
            waited += UPDATE_POLL_MS

            if ((probe.version() as? Result.Success)?.data?.version == target) {
                _state.update { it.copy(updateRun = UpdateRun.Finished(target), serverVersion = target) }
                refresh()
                return
            }

            val status = updates.publicStatus()?.takeIf { it.updatedAt != before }

            when (status?.stateAt(clock.instant())) {
                UpdateState.RestoreRequired -> {
                    _state.update { it.copy(updateRun = null) }
                    _events.send(AccountEvent.GoToGate)
                    return
                }
                UpdateState.RolledBack, UpdateState.Failed, UpdateState.Interrupted -> {
                    finishFailed()
                    return
                }
                UpdateState.Running, UpdateState.Succeeded -> started = true
                UpdateState.Unknown, null -> if (!started && waited >= UPDATE_START_LIMIT_MS) {
                    _state.update { it.copy(updateRun = UpdateRun.NotStarted) }
                    return
                }
            }
        }

        finishFailed()
    }

    /** The reason is only in the signed-in status, because `/update-status` is anonymous. */
    private suspend fun finishFailed() {
        val update = (updates.status() as? Result.Success)?.data
        _state.update { it.copy(update = update ?: it.update, updateRun = UpdateRun.Failed(update?.detail)) }
    }
}

@Composable
fun AccountRoot(
    onChangeServer: () -> Unit,
    onOpenLicenses: () -> Unit,
    onGate: () -> Unit,
    viewModel: AccountViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val member by viewModel.member.collectAsStateWithLifecycle()

    OnStart(viewModel::refresh)

    ObserveAsEvents(viewModel.events) { event ->
        when (event) {
            AccountEvent.GoToGate -> onGate()
        }
    }

    AccountSection(
        state = state,
        member = member,
        language = AppLanguage.current(),
        onLanguage = AppLanguage::set,
        onAction = viewModel::onAction,
        onChangeServer = onChangeServer,
        onOpenLicenses = onOpenLicenses,
    )
}

@Composable
private fun Labelled(label: String, content: @Composable () -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        BasicText(label, style = KvType.small)
        content()
    }
}

/**
 * What the server can do about a newer release. Everyone sees that one exists, and what follows depends on
 * who's asking and whether the server has an agent. Nothing shows when there's nothing to say.
 */
@Composable
private fun UpdateRow(state: AccountState, canManage: Boolean, onAction: (AccountAction) -> Unit) {
    val update = state.update
    val run = state.updateRun
    val rolledBack = update?.state == UpdateState.RolledBack && run == null
    if (update?.updateAvailable != true && run == null && !rolledBack && state.updateError == null) return

    val note = KvType.label.copy(color = KvColors.neutral500)

    Labelled(stringResource(R.string.update_section)) {
        when (run) {
            is UpdateRun.Following -> Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(KvSpace.s3),
            ) {
                KvSpinner()
                BasicText(stringResource(R.string.update_running, run.targetVersion), style = KvType.small)
            }
            is UpdateRun.Finished -> BasicText(stringResource(R.string.update_finished, run.version), style = KvType.small)
            is UpdateRun.Failed -> BasicText(
                run.detail?.let { stringResource(R.string.update_failed_reason, it) } ?: stringResource(R.string.update_failed),
                style = KvType.small,
            )
            UpdateRun.NotStarted -> BasicText(stringResource(R.string.update_not_started), style = KvType.small)
            null -> if (rolledBack) {
                BasicText(stringResource(R.string.update_rolled_back, update?.detail.orEmpty()), style = KvType.small)
            }
        }

        if (update?.updateAvailable == true && run !is UpdateRun.Following) {
            val uriHandler = LocalUriHandler.current
            BasicText(stringResource(R.string.update_available, update.latestVersion.orEmpty()), style = KvType.control)
            update.releaseUrl?.let { url ->
                KvButton(stringResource(R.string.update_release_notes), { uriHandler.openUri(url) }, style = KvButtonStyle.Ghost)
            }
            when {
                !canManage -> BasicText(stringResource(R.string.update_no_permission), style = note)
                update.canStart -> KvButton(
                    text = stringResource(R.string.update_start),
                    onClick = { onAction(AccountAction.OnConfirm(AccountConfirm.Update)) },
                    style = KvButtonStyle.Primary,
                    glyph = PhosphorIcons.ArrowClockwise,
                )
                else -> {
                    BasicText(stringResource(R.string.update_console_hint), style = note)
                    CommandLine(UPDATE_COMMAND)
                }
            }
        }

        state.updateError?.let { BasicText(it.asString(), style = KvType.small) }
    }
}

@Composable
fun AccountSection(
    state: AccountState,
    member: CurrentMember?,
    language: String,
    onLanguage: (String) -> Unit,
    onAction: (AccountAction) -> Unit,
    onChangeServer: () -> Unit,
    onOpenLicenses: () -> Unit,
) {
    val context = LocalContext.current
    val appVersion = context.packageManager.getPackageInfo(context.packageName, 0).versionName.orEmpty()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
        verticalArrangement = Arrangement.spacedBy(KvSpace.s8),
    ) {
        if (member != null) {
            val shape = RoundedCornerShape(KvRadius.md)
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .kvElevation(KvElevation.Small, shape)
                    .background(KvColors.surface, shape)
                    .padding(14.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(13.dp),
            ) {
                KvAvatar(Initials.from(member.displayName), size = 42.dp)
                Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    BasicText(member.displayName, style = KvType.control)
                    BasicText(
                        member.email,
                        style = KvType.label.copy(color = KvColors.neutral500),
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
                KvTag(
                    stringResource(member.role.labelRes()),
                    style = if (member.role == Role.Admin) KvTagStyle.Accent else KvTagStyle.Neutral,
                )
            }

            Labelled(stringResource(R.string.my_permissions)) {
                FlowRow(
                    horizontalArrangement = Arrangement.spacedBy(7.dp),
                    verticalArrangement = Arrangement.spacedBy(7.dp),
                ) {
                    member.permissions.sortedBy { it.ordinal }.forEach {
                        KvTag(stringResource(it.labelRes()), style = KvTagStyle.Outline)
                    }
                }
                BasicText(
                    stringResource(
                        when {
                            member.role == Role.Admin -> R.string.admin_permissions_note
                            member.permissions.isEmpty() -> R.string.no_permissions
                            else -> R.string.perms_blurb
                        },
                    ),
                    style = KvType.label.copy(color = KvColors.neutral500),
                )
            }
        }

        Labelled(stringResource(R.string.language)) {
            KvSegmented(
                options = listOf("Dansk", "English"),
                selected = if (language == AppLanguage.ENGLISH) 1 else 0,
                onSelect = { onLanguage(if (it == 1) AppLanguage.ENGLISH else AppLanguage.DANISH) },
                modifier = Modifier.fillMaxWidth(),
            )
            BasicText(stringResource(R.string.language_note), style = KvType.label.copy(color = KvColors.neutral500))
        }

        Labelled(stringResource(R.string.server_section)) {
            BasicText(state.serverAddress, style = KvType.mono)
            KvButton(
                text = stringResource(R.string.confirm_change_server_title).trimEnd('?'),
                onClick = { onAction(AccountAction.OnConfirm(AccountConfirm.ChangeServer)) },
                glyph = PhosphorIcons.SlidersHorizontal,
            )
        }

        UpdateRow(state, canManage = member?.can(Permission.ManageSettings) == true, onAction = onAction)

        Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s1)) {
            BasicText(
                "${stringResource(R.string.app_version)}: $appVersion",
                style = KvType.label.copy(color = KvColors.neutral500),
            )
            BasicText(
                "${stringResource(R.string.server_version)}: ${state.serverVersion ?: KrFormat.MISSING}",
                style = KvType.label.copy(color = KvColors.neutral500),
            )
        }

        Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
            KvButton(
                text = stringResource(R.string.sign_out),
                onClick = { onAction(AccountAction.OnConfirm(AccountConfirm.SignOut)) },
                glyph = PhosphorIcons.SignOut,
            )
            KvButton(stringResource(R.string.licenses), onOpenLicenses, style = KvButtonStyle.Ghost)
        }
    }

    if (state.confirm == AccountConfirm.Update) {
        KvDialog(
            title = stringResource(R.string.update_confirm_title),
            onDismiss = { onAction(AccountAction.OnConfirm(null)) },
            actions = {
                KvButton(
                    stringResource(CoreR.string.cancel),
                    { onAction(AccountAction.OnConfirm(null)) },
                    style = KvButtonStyle.Ghost,
                )
                KvButton(
                    text = stringResource(R.string.update_start),
                    onClick = { onAction(AccountAction.OnStartUpdate) },
                    style = KvButtonStyle.Primary,
                )
            },
        ) {
            BasicText(stringResource(R.string.update_confirm_message), style = KvType.small)
        }
    }

    state.confirm?.takeIf { it != AccountConfirm.Update }?.let { confirm ->
        val signOut = confirm == AccountConfirm.SignOut
        KvDialog(
            title = stringResource(if (signOut) R.string.confirm_sign_out_title else R.string.confirm_change_server_title),
            onDismiss = { onAction(AccountAction.OnConfirm(null)) },
            actions = {
                KvButton(
                    stringResource(CoreR.string.cancel),
                    { onAction(AccountAction.OnConfirm(null)) },
                    style = KvButtonStyle.Ghost,
                )
                KvButton(
                    text = stringResource(if (signOut) R.string.sign_out else R.string.confirm_change_server_title).trimEnd('?'),
                    onClick = {
                        if (signOut) {
                            onAction(AccountAction.OnSignOut)
                        } else {
                            onAction(AccountAction.OnConfirm(null))
                            onChangeServer()
                        }
                    },
                    style = KvButtonStyle.Primary,
                )
            },
        ) {
            BasicText(
                stringResource(if (signOut) R.string.confirm_sign_out_message else R.string.confirm_change_server_message),
                style = KvType.small,
            )
        }
    }
}
