/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.presentation

import androidx.navigation3.runtime.EntryProviderScope
import androidx.navigation3.runtime.NavKey
import dev.jelshoj.kvitteria.feature.settings.presentation.account.AccountViewModel
import dev.jelshoj.kvitteria.feature.settings.presentation.household.HouseholdViewModel
import dev.jelshoj.kvitteria.feature.settings.presentation.licenses.LicensesRoot
import dev.jelshoj.kvitteria.feature.settings.presentation.mailboxes.MailboxesViewModel
import dev.jelshoj.kvitteria.feature.settings.presentation.permissions.PermissionsViewModel
import dev.jelshoj.kvitteria.feature.settings.presentation.pipeline.PipelineSettingsViewModel
import kotlinx.serialization.Serializable
import org.koin.core.module.dsl.viewModelOf
import org.koin.dsl.module

@Serializable data object SettingsRoute : NavKey

@Serializable data object LicensesRoute : NavKey

fun EntryProviderScope<NavKey>.settingsEntries(
    onPush: (NavKey) -> Unit,
    onBack: () -> Unit,
    onChangeServer: () -> Unit,
) {
    entry<SettingsRoute> {
        SettingsRoot(
            onBack = onBack,
            onChangeServer = onChangeServer,
            onOpenLicenses = { onPush(LicensesRoute) },
        )
    }
    entry<LicensesRoute> { LicensesRoot(onBack = onBack) }
}

val settingsPresentationModule = module {
    viewModelOf(::AccountViewModel)
    viewModelOf(::HouseholdViewModel)
    viewModelOf(::PermissionsViewModel)
    viewModelOf(::MailboxesViewModel)
    viewModelOf(::PipelineSettingsViewModel)
}
