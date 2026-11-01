/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import java.time.Clock
import java.time.Instant
import java.time.YearMonth
import java.time.ZoneId
import java.util.Locale

/**
 * Purchases happen in Denmark, so dates and months read in Danish local time whatever zone the device
 * is in. A purchase at 00:30 on the first belongs to the new month even on a phone in New York.
 *
 * Month names are spelled out here, because the platform's short forms differ between Android versions
 * and the mockup's don't carry a full stop.
 */
object DanishTime {
    val ZONE: ZoneId = ZoneId.of("Europe/Copenhagen")

    private val daMonths = listOf(
        "januar", "februar", "marts", "april", "maj", "juni", "juli", "august", "september", "oktober", "november",
        "december",
    )
    private val enMonths = listOf(
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November",
        "December",
    )

    private fun months(locale: Locale) = if (locale.language == "en") enMonths else daMonths

    private fun short(name: String) = name.take(3)

    fun currentMonth(clock: Clock): YearMonth = YearMonth.from(clock.instant().atZone(ZONE))

    fun monthOf(instant: Instant): YearMonth = YearMonth.from(instant.atZone(ZONE))

    /** `18. aug 2026`, or `18 Aug 2026` in English. */
    fun date(instant: Instant, locale: Locale = Locale.getDefault()): String {
        val date = instant.atZone(ZONE).toLocalDate()
        val month = short(months(locale)[date.monthValue - 1])
        return if (locale.language == "en") {
            "${date.dayOfMonth} $month ${date.year}"
        } else {
            "${date.dayOfMonth}. $month ${date.year}"
        }
    }

    /** The date with its time, as `18. aug 2026 07:42`. */
    fun dateTime(instant: Instant, locale: Locale = Locale.getDefault()): String {
        val time = instant.atZone(ZONE).toLocalTime()
        return "%s %02d:%02d".format(Locale.ROOT, date(instant, locale), time.hour, time.minute)
    }

    /** `August 2026`. */
    fun month(month: YearMonth, locale: Locale = Locale.getDefault()): String =
        "${months(locale)[month.monthValue - 1].replaceFirstChar { it.uppercase() }} ${month.year}"

    /** `aug`, for a chart label. */
    fun shortMonth(month: YearMonth, locale: Locale = Locale.getDefault()): String =
        short(months(locale)[month.monthValue - 1])

    /** The hour in Denmark, which is what a greeting goes by. */
    fun hour(clock: Clock): Int = clock.instant().atZone(ZONE).hour
}
