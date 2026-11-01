/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import java.time.Duration
import java.time.Instant

/** How long ago something happened, in the largest unit that reads naturally. */
object RelativeTime {
    private const val MINUTES_IN_HOUR = 60
    private const val HOURS_IN_DAY = 24

    fun describe(then: Instant, now: Instant): UiText {
        val minutes = Duration.between(then, now).toMinutes()
        return when {
            minutes < 1 -> UiText.StringResource(R.string.time_just_now)
            minutes < MINUTES_IN_HOUR -> UiText.StringResource(R.string.time_minutes_ago, listOf(minutes))
            minutes < MINUTES_IN_HOUR * HOURS_IN_DAY ->
                UiText.StringResource(R.string.time_hours_ago, listOf(minutes / MINUTES_IN_HOUR))
            else -> UiText.StringResource(R.string.time_days_ago, listOf(minutes / (MINUTES_IN_HOUR * HOURS_IN_DAY)))
        }
    }
}
