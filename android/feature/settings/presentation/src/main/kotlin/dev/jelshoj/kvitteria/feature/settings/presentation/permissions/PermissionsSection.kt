/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.presentation.permissions

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.designsystem.KvCheckbox
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvNote
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvToastBox
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.rememberKvToastState
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.presentation.Loadable
import dev.jelshoj.kvitteria.core.presentation.LoadableContent
import dev.jelshoj.kvitteria.core.presentation.Loader
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.refreshIfForbidden
import dev.jelshoj.kvitteria.core.presentation.toUiText
import dev.jelshoj.kvitteria.feature.settings.domain.HouseholdRepository
import dev.jelshoj.kvitteria.feature.settings.domain.MemberPermissions
import dev.jelshoj.kvitteria.feature.settings.presentation.R
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel

@StringRes
fun Permission.labelRes(): Int = when (this) {
    Permission.CategorizeLines -> R.string.perm_categorize_lines
    Permission.ManageTaxonomy -> R.string.perm_manage_taxonomy
    Permission.EditReceipts -> R.string.perm_edit_receipts
    Permission.ManageUsers -> R.string.perm_manage_users
    Permission.ManageSettings -> R.string.perm_manage_settings
    Permission.ManageMailboxes -> R.string.perm_manage_mailboxes
    Permission.AddReceipts -> R.string.perm_add_receipts
}

@StringRes
fun Permission.blurbRes(): Int = when (this) {
    Permission.CategorizeLines -> R.string.perm_categorize_lines_blurb
    Permission.ManageTaxonomy -> R.string.perm_manage_taxonomy_blurb
    Permission.EditReceipts -> R.string.perm_edit_receipts_blurb
    Permission.ManageUsers -> R.string.perm_manage_users_blurb
    Permission.ManageSettings -> R.string.perm_manage_settings_blurb
    Permission.ManageMailboxes -> R.string.perm_manage_mailboxes_blurb
    Permission.AddReceipts -> R.string.perm_add_receipts_blurb
}

data class PermissionsState(
    val permissions: Loadable<MemberPermissions> = Loadable(),
    val canEdit: Boolean = false,
)

class PermissionsViewModel(
    private val household: HouseholdRepository,
    private val members: MemberRepository,
    sessions: SessionManager,
) : ViewModel() {
    private val loader = Loader(viewModelScope) { household.memberPermissions() }

    val state = combine(loader.state, sessions.member) { permissions, member ->
        PermissionsState(permissions, member?.can(Permission.ManageSettings) == true)
    }.stateIn(viewModelScope, SharingStarted.Eagerly, PermissionsState())

    private val saveFailures = Channel<UiText>(Channel.BUFFERED)
    val messages = merge(loader.failures, saveFailures.receiveAsFlow())

    fun refresh() = loader.refresh()

    /** Shows at once and saves the full set, because the endpoint replaces it. */
    fun toggle(permission: Permission, on: Boolean) {
        val previous = loader.state.value.data ?: return
        val next = if (on) previous.granted + permission else previous.granted - permission
        loader.update { it.copy(granted = next) }
        viewModelScope.launch {
            when (val result = household.saveMemberPermissions(next)) {
                is Result.Success -> loader.update { result.data }
                is Result.Error -> {
                    loader.update { it.copy(granted = previous.granted) }
                    saveFailures.send(result.error.toUiText())
                    members.refreshIfForbidden(result.error)
                }
            }
        }
    }
}

@Composable
fun PermissionsRoot(viewModel: PermissionsViewModel = koinViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.messages) { toast.show(it.asString(context)) }

    KvToastBox(toast) {
        LoadableContent(state.permissions, onRefresh = viewModel::refresh) { permissions ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
                verticalArrangement = Arrangement.spacedBy(KvSpace.s4),
            ) {
                BasicText(stringResource(R.string.perms_blurb), style = KvType.label.copy(color = KvColors.neutral400))
                Column {
                    permissions.available.forEach { permission ->
                        KvCheckbox(
                            checked = permission in permissions.granted,
                            onCheckedChange = { viewModel.toggle(permission, it) },
                            enabled = state.canEdit,
                        ) {
                            Column(verticalArrangement = Arrangement.spacedBy(3.dp), modifier = Modifier.fillMaxWidth()) {
                                BasicText(stringResource(permission.labelRes()), style = KvType.small)
                                BasicText(
                                    stringResource(permission.blurbRes()),
                                    style = KvType.label.copy(color = KvColors.neutral500),
                                )
                            }
                        }
                    }
                }
                if (!state.canEdit) KvNote(stringResource(R.string.perms_read_only))
            }
        }
    }
}
