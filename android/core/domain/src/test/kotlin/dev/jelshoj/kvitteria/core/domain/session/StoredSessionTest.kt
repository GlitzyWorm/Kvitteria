/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.session

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isTrue
import java.time.Duration
import java.time.Instant
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource

class StoredSessionTest {
    private val now = Instant.parse("2026-08-26T12:00:00Z")

    private fun response(expiresAt: Long?, expiresIn: Long?, email: String = "you@household.dk") =
        StoredSession.fromTokenResponse("access", "refresh", expiresAt, expiresIn, email, now)

    @Test
    fun `prefers the absolute expiry`() {
        val expected = now.plus(Duration.ofHours(2))
        assertThat(response(expected.epochSecond, 3600).expiresAt).isEqualTo(expected)
    }

    @Test
    fun `falls back to the relative expiry`() {
        assertThat(response(null, 3600).expiresAt).isEqualTo(now.plusSeconds(3600))
    }

    @Test
    fun `neither expiry means expired now`() {
        val session = response(null, null)
        assertThat(session.expiresAt).isEqualTo(now)
        assertThat(session.isExpiring(now)).isTrue()
    }

    @ParameterizedTest
    @CsvSource("600,false", "59,true", "60,true", "-1,true")
    fun `expires within the refresh skew`(secondsRemaining: Long, expected: Boolean) {
        val session = StoredSession("access", "refresh", now.plusSeconds(secondsRemaining), "you@household.dk")
        assertThat(session.isExpiring(now)).isEqualTo(expected)
    }

    @Test
    fun `the skew is a minute`() {
        assertThat(StoredSession.REFRESH_SKEW).isEqualTo(Duration.ofSeconds(60))
    }

    @Test
    fun `carries the address it belongs to`() {
        assertThat(response(null, 3600, "You@Household.dk").email).isEqualTo("You@Household.dk")
    }
}
