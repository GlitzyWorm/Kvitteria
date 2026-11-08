/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.presentation

import androidx.navigation3.runtime.EntryProviderScope
import androidx.navigation3.runtime.NavKey
import kotlinx.serialization.Serializable
import org.koin.core.module.dsl.singleOf
import org.koin.core.module.dsl.viewModelOf
import org.koin.dsl.module

/** The confirmation of whatever is in [dev.jelshoj.kvitteria.feature.capture.domain.PendingCapture]. */
@Serializable data object ConfirmCaptureRoute : NavKey

fun EntryProviderScope<NavKey>.captureEntries(onClose: () -> Unit) {
    entry<ConfirmCaptureRoute> { ConfirmCaptureRoot(onClose = onClose) }
}

val capturePresentationModule = module {
    singleOf(::CaptureNotices)
    viewModelOf(::AddReceiptViewModel)
    viewModelOf(::ConfirmCaptureViewModel)
}
