/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain

import kotlinx.coroutines.flow.StateFlow

/** The two counts behind the tab badges. The shell refreshes them, and saves update them directly. */
interface WorkCounts {
    val unassignedLines: StateFlow<Int>
    val failedMail: StateFlow<Int>

    suspend fun refresh()

    fun setUnassignedLines(count: Int)

    fun setFailedMail(count: Int)
}
