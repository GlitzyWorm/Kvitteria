/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.domain

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isFalse
import assertk.assertions.isTrue
import dev.jelshoj.kvitteria.core.domain.Result
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource

/** The client half of a rule the server also enforces. A drift would be a round trip that always fails. */
class MailboxDraftValidatorTest {
    private fun valid() = MailboxDraft(
        ownerId = "owner",
        host = "imap.gmail.com",
        port = "993",
        folder = "INBOX/Kvitteringer",
        username = "nogen@eksempel.dk",
        password = "hemmeligt",
    )

    private val ok = Result.Success(Unit)
    private val required = Result.Error(MailboxDraftError.REQUIRED_FIELDS)
    private val badPort = Result.Error(MailboxDraftError.PORT)

    @Test
    fun `accepts a complete draft`() {
        assertThat(MailboxDraftValidator.validate(valid())).isEqualTo(ok)
    }

    @ParameterizedTest
    @ValueSource(strings = ["", "   "])
    fun `rejects a missing host`(host: String) {
        assertThat(MailboxDraftValidator.validate(valid().copy(host = host))).isEqualTo(required)
    }

    @ParameterizedTest
    @ValueSource(strings = ["", "   "])
    fun `rejects a missing folder`(folder: String) {
        assertThat(MailboxDraftValidator.validate(valid().copy(folder = folder))).isEqualTo(required)
    }

    @ParameterizedTest
    @ValueSource(strings = ["", "   "])
    fun `rejects a missing username`(username: String) {
        assertThat(MailboxDraftValidator.validate(valid().copy(username = username))).isEqualTo(required)
    }

    @ParameterizedTest
    @ValueSource(strings = ["1", "993", "65535"])
    fun `accepts a port in range`(port: String) {
        assertThat(MailboxDraftValidator.validate(valid().copy(port = port))).isEqualTo(ok)
    }

    @ParameterizedTest
    @ValueSource(strings = ["0", "-1", "65536", "", "abc", "99 3", "9.93"])
    fun `rejects a port out of range or unparseable`(port: String) {
        assertThat(MailboxDraftValidator.validate(valid().copy(port = port))).isEqualTo(badPort)
    }

    @Test
    fun `rejects an empty password when creating`() {
        assertThat(MailboxDraftValidator.validate(valid().copy(password = "")))
            .isEqualTo(Result.Error(MailboxDraftError.PASSWORD_REQUIRED))
    }

    @Test
    fun `accepts an empty password when editing`() {
        assertThat(MailboxDraftValidator.validate(valid().copy(id = "m", password = ""))).isEqualTo(ok)
    }

    @Test
    fun `reports missing fields before a bad port`() {
        assertThat(MailboxDraftValidator.validate(valid().copy(host = "", port = "notanumber"))).isEqualTo(required)
    }

    @Test
    fun `rejects a draft with no owner`() {
        assertThat(MailboxDraftValidator.validate(valid().copy(ownerId = "")))
            .isEqualTo(Result.Error(MailboxDraftError.NO_OWNER))
    }

    @Test
    fun `parses the port`() {
        assertThat(MailboxDraftValidator.parsePort(valid())).isEqualTo(993)
    }

    @Test
    fun `isEdit follows the id`() {
        assertThat(valid().isEdit).isFalse()
        assertThat(valid().copy(id = "m").isEdit).isTrue()
    }
}
