/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.member

/** The one or two letters standing in for a member's face. */
object Initials {
    const val UNKNOWN = "?"

    /**
     * The first letter of the first word and of the last. One word gives one letter, because a name
     * isn't an abbreviation of itself. `uppercase()` is root-locale, so a Turkish device doesn't turn a
     * Danish member's `i` into `İ`.
     */
    fun from(displayName: String?): String {
        val words = displayName.orEmpty().split(Regex("""\s+""")).filter { it.isNotEmpty() }
        if (words.isEmpty()) return UNKNOWN
        val first = words.first().take(1)
        return if (words.size == 1) first.uppercase() else (first + words.last().take(1)).uppercase()
    }
}
