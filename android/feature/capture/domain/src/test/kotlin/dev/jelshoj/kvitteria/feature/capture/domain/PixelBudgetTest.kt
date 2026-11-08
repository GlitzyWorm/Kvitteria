/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.domain

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isLessThanOrEqualTo
import assertk.assertions.isGreaterThanOrEqualTo
import kotlin.math.abs
import org.junit.jupiter.api.Test

class PixelBudgetTest {
    private fun assertKeepsAspect(width: Int, height: Int, size: PixelBudget.Size) {
        val before = width.toDouble() / height
        val after = size.width.toDouble() / size.height
        assertThat(abs(before - after) / before).isLessThanOrEqualTo(0.01)
    }

    @Test
    fun `a 4000 by 3000 photo comes out within the budget at the same aspect`() {
        val size = PixelBudget.target(4000, 3000)

        assertThat(size.pixels).isLessThanOrEqualTo(PixelBudget.MAX_PIXELS)
        assertThat(size.pixels).isGreaterThanOrEqualTo(990_000)
        assertKeepsAspect(4000, 3000, size)
    }

    @Test
    fun `a tall screenshot keeps a readable width`() {
        val size = PixelBudget.target(1080, 2400)

        assertThat(size.pixels).isLessThanOrEqualTo(PixelBudget.MAX_PIXELS)
        assertThat(size.width).isGreaterThanOrEqualTo(660)
        assertKeepsAspect(1080, 2400, size)
    }

    @Test
    fun `an image within the budget is not scaled up`() {
        assertThat(PixelBudget.target(720, 1280)).isEqualTo(PixelBudget.Size(720, 1280))
        assertThat(PixelBudget.target(1000, 1000)).isEqualTo(PixelBudget.Size(1000, 1000))
    }

    @Test
    fun `a quarter turn swaps the sides before scaling`() {
        val upright = PixelBudget.rotated(4000, 3000, 90)

        assertThat(upright).isEqualTo(PixelBudget.Size(3000, 4000))
        assertThat(PixelBudget.rotated(4000, 3000, 180)).isEqualTo(PixelBudget.Size(4000, 3000))
        val size = PixelBudget.target(upright.width, upright.height)
        assertThat(size.height > size.width).isEqualTo(true)
        assertKeepsAspect(3000, 4000, size)
    }

    @Test
    fun `decode subsampling never drops below the budget`() {
        val sample = PixelBudget.sampleSize(4000, 3000)

        assertThat(sample).isEqualTo(2)
        assertThat((4000L / sample) * (3000L / sample)).isGreaterThanOrEqualTo(PixelBudget.MAX_PIXELS)
        assertThat(PixelBudget.sampleSize(800, 600)).isEqualTo(1)
    }
}
