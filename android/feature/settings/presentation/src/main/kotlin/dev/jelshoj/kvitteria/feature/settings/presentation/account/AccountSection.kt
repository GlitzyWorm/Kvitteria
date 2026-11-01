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
import dev.jelshoj.kvitteria.core.designsystem.KvTag
import dev.jelshoj.kvitteria.core.designsystem.KvTagStyle
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.kvElevation
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.member.Initials
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.session.ServerProbe
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.presentation.AppLanguage
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import dev.jelshoj.kvitteria.core.presentation.labelRes
import dev.jelshoj.kvitteria.feature.settings.presentation.R
import dev.jelshoj.kvitteria.feature.settings.presentation.permissions.labelRes
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel

enum class AccountConfirm { SignOut, ChangeServer }

data class AccountState(
    val serverAddress: String = "",
    val serverVersion: String? = null,
    val confirm: AccountConfirm? = null,
)

sealed interface AccountAction {
    data class OnConfirm(val confirm: AccountConfirm?) : AccountAction
    data object OnSignOut : AccountAction
}

class AccountViewModel(
    private val sessions: SessionManager,
    private val settings: ServerSettings,
    private val probe: ServerProbe,
    private val members: MemberRepository,
) : ViewModel() {
    val member = sessions.member

    private val _state = MutableStateFlow(AccountState(serverAddress = settings.address.value.orEmpty()))
    val state = _state.asStateFlow()

    fun refresh() {
        viewModelScope.launch {
            members.refreshCurrent()
            val version = (probe.version() as? Result.Success)?.data?.version
            _state.update { it.copy(serverAddress = settings.load().orEmpty(), serverVersion = version) }
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
        }
    }
}

@Composable
fun AccountRoot(
    onChangeServer: () -> Unit,
    onOpenLicenses: () -> Unit,
    viewModel: AccountViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val member by viewModel.member.collectAsStateWithLifecycle()

    OnStart(viewModel::refresh)

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

    state.confirm?.let { confirm ->
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
