/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.presentation

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvIconButton
import dev.jelshoj.kvitteria.core.designsystem.KvScrollTabs
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import dev.jelshoj.kvitteria.feature.settings.presentation.account.AccountRoot
import dev.jelshoj.kvitteria.feature.settings.presentation.household.HouseholdRoot
import dev.jelshoj.kvitteria.feature.settings.presentation.mailboxes.MailboxesRoot
import dev.jelshoj.kvitteria.feature.settings.presentation.permissions.PermissionsRoot
import dev.jelshoj.kvitteria.feature.settings.presentation.pipeline.PipelineSettingsRoot

/** Title row with a back button, shared by Settings and Licences. */
@Composable
internal fun SettingsTopBar(title: String, onBack: () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(end = KvSpace.gutter),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        KvIconButton(PhosphorIcons.CaretLeft, stringResource(CoreR.string.back), onBack)
        KvHeading(title)
    }
}

/**
 * Five sections, each with its own ViewModel. A section reloads itself when it starts, which covers
 * both opening Settings and changing section.
 */
@Composable
fun SettingsRoot(
    onBack: () -> Unit,
    onChangeServer: () -> Unit,
    onOpenLicenses: () -> Unit,
    onGate: () -> Unit,
) {
    var section by rememberSaveable { androidx.compose.runtime.mutableIntStateOf(0) }

    Column(
        modifier = Modifier.fillMaxSize().background(KvColors.bg).safeDrawingPadding().imePadding(),
        verticalArrangement = Arrangement.spacedBy(KvSpace.s3),
    ) {
        SettingsTopBar(stringResource(R.string.settings_title), onBack)
        KvScrollTabs(
            options = listOf(
                stringResource(R.string.account),
                stringResource(R.string.section_household),
                stringResource(R.string.section_permissions),
                stringResource(R.string.section_mailboxes),
                stringResource(R.string.pipeline_kicker),
            ),
            selected = section,
            onSelect = { section = it },
            modifier = Modifier.padding(horizontal = KvSpace.gutter),
        )
        when (section) {
            0 -> AccountRoot(onChangeServer = onChangeServer, onOpenLicenses = onOpenLicenses, onGate = onGate)
            1 -> HouseholdRoot()
            2 -> PermissionsRoot()
            3 -> MailboxesRoot()
            else -> PipelineSettingsRoot()
        }
    }
}
