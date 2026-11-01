/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import androidx.annotation.StringRes
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Role
import java.util.Locale

/** A 403 means the member's permissions changed under them, so what the screen shows is out of date. */
suspend fun MemberRepository.refreshIfForbidden(error: ApiError) {
    if (error.kind == DataError.Network.FORBIDDEN) refreshCurrent()
}

@StringRes
fun Role.labelRes(): Int = if (this == Role.Admin) R.string.role_admin else R.string.role_member

/**
 * The language choice, kept by AppCompat. Setting it restarts the activity, so every string updates with
 * no language plumbing in composables, and it's stored across restarts.
 */
object AppLanguage {
    const val DANISH = "da"
    const val ENGLISH = "en"

    fun current(): String {
        val chosen = AppCompatDelegate.getApplicationLocales()[0] ?: Locale.getDefault()
        return if (chosen.language == ENGLISH) ENGLISH else DANISH
    }

    fun set(tag: String) {
        AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags(tag))
    }
}
