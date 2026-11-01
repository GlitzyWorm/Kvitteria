/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.session

import assertk.assertThat
import assertk.assertions.isEqualTo
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.NullSource
import org.junit.jupiter.params.provider.ValueSource

class HealthProbeTest {
    private val degraded = """
        {
          "status": "Degraded",
          "totalDurationMs": 42,
          "checks": [
            { "name": "postgres", "status": "Healthy", "durationMs": 3 },
            { "name": "ollama-model", "status": "Healthy", "durationMs": 9 },
            { "name": "household", "status": "Degraded", "durationMs": 2,
              "description": "No active admin yet; the first person to sign in claims the household." }
          ]
        }
    """.trimIndent()

    private val healthy = """
        { "status": "Healthy", "checks": [
            { "name": "postgres", "status": "Healthy" },
            { "name": "household", "status": "Healthy" } ] }
    """.trimIndent()

    @Test
    fun `degraded household means unclaimed`() {
        assertThat(HealthProbe.readHouseholdState(degraded)).isEqualTo(HouseholdState.Unclaimed)
    }

    @Test
    fun `healthy household means claimed`() {
        assertThat(HealthProbe.readHouseholdState(healthy)).isEqualTo(HouseholdState.Claimed)
    }

    /** The reason this reads a body rather than a status code. */
    @Test
    fun `reads the household check out of an unhealthy report`() {
        val body = """
            { "status": "Unhealthy", "checks": [
                { "name": "ollama-model", "status": "Unhealthy", "description": "Model not pulled" },
                { "name": "household", "status": "Degraded" } ] }
        """.trimIndent()

        assertThat(HealthProbe.readHouseholdState(body)).isEqualTo(HouseholdState.Unclaimed)
    }

    @Test
    fun `unhealthy household says nothing either way`() {
        val body = """{ "checks": [ { "name": "household", "status": "Unhealthy" } ] }"""
        assertThat(HealthProbe.readHouseholdState(body)).isEqualTo(HouseholdState.Unknown)
    }

    @ParameterizedTest
    @NullSource
    @ValueSource(
        strings = [
            "",
            "   ",
            "<html><body>404</body></html>",
            "{ }",
            """{ "checks": "not an array" }""",
            """{ "checks": [ { "name": "postgres", "status": "Healthy" } ] }""",
            """{ "checks": [ { "name": "household" } ] }""",
        ],
    )
    fun `anything else is unknown`(body: String?) {
        assertThat(HealthProbe.readHouseholdState(body)).isEqualTo(HouseholdState.Unknown)
    }
}
