/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.network

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.NullSource
import org.junit.jupiter.params.provider.ValueSource

/**
 * Every server failure the user reads comes through here, so a silent null would present as a blank
 * error callout.
 */
class ProblemDetailTest {
    @Test
    fun `reads the detail of a real problem body`() {
        val body = """
            {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.3",
             "title":"Bad Gateway","status":502,
             "detail":"IMAP folder 'INBOX/Bon' was not found. Available folders: INBOX, INBOX/Kvitteringer"}
        """.trimIndent()

        assertThat(ProblemDetail.read(body))
            .isEqualTo("IMAP folder 'INBOX/Bon' was not found. Available folders: INBOX, INBOX/Kvitteringer")
    }

    @Test
    fun `returns null when there is no detail`() {
        assertThat(ProblemDetail.read("""{"title":"Forbidden","status":403}""")).isNull()
    }

    @ParameterizedTest
    @ValueSource(strings = ["""{"detail":""}""", """{"detail":"   "}"""])
    fun `treats an empty detail as absent`(body: String) {
        assertThat(ProblemDetail.read(body)).isNull()
    }

    @ParameterizedTest
    @ValueSource(strings = ["""{"detail":42}""", """{"detail":null}""", """{"detail":{"nested":"no"}}"""])
    fun `ignores a detail that is not a string`(body: String) {
        assertThat(ProblemDetail.read(body)).isNull()
    }

    @ParameterizedTest
    @ValueSource(
        strings = [
            "<html><body>502 Bad Gateway</body></html>",
            "{\"detail\":",
            "not json at all",
            "[]",
            "\"just a string\"",
        ],
    )
    fun `returns null rather than throwing on a body that is not a problem`(body: String) {
        assertThat(ProblemDetail.read(body)).isNull()
    }

    @ParameterizedTest
    @NullSource
    @ValueSource(strings = ["", "   "])
    fun `returns null for an empty body`(body: String?) {
        assertThat(ProblemDetail.read(body)).isNull()
    }
}
