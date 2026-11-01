/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.domain

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.Result
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource
import org.junit.jupiter.params.provider.NullSource
import org.junit.jupiter.params.provider.ValueSource

class AuthErrorMapperTest {
    @ParameterizedTest
    @CsvSource(
        "invalid_credentials,INVALID_CREDENTIALS",
        "user_already_exists,USER_ALREADY_EXISTS",
        "email_exists,USER_ALREADY_EXISTS",
        "weak_password,WEAK_PASSWORD",
        "email_address_invalid,EMAIL_INVALID",
        "email_not_confirmed,EMAIL_NOT_CONFIRMED",
        "over_request_rate_limit,RATE_LIMITED",
        "over_email_send_rate_limit,RATE_LIMITED",
        "signup_disabled,SIGNUP_DISABLED",
        "refresh_token_already_used,SESSION_EXPIRED",
    )
    fun `maps a known error code`(code: String, expected: AuthError) {
        val body = """{ "code": 400, "error_code": "$code", "msg": "whatever" }"""
        assertThat(AuthErrorMapper.map(400, body).error).isEqualTo(expected)
    }

    @Test
    fun `maps the oauth style shape`() {
        val body = """{ "error": "invalid_grant", "error_description": "Invalid login credentials" }"""
        val mapped = AuthErrorMapper.map(400, body)
        assertThat(mapped.error).isEqualTo(AuthError.INVALID_CREDENTIALS)
        assertThat(mapped.serverMessage).isEqualTo("Invalid login credentials")
    }

    @ParameterizedTest
    @CsvSource(
        "400,INVALID_CREDENTIALS",
        "401,INVALID_CREDENTIALS",
        "422,EMAIL_INVALID",
        "429,RATE_LIMITED",
        "500,SERVER",
        "503,SERVER",
        "418,GENERIC",
    )
    fun `falls back to the status`(status: Int, expected: AuthError) {
        val body = """{ "code": 0, "error_code": "something_new", "msg": "?" }"""
        assertThat(AuthErrorMapper.map(status, body).error).isEqualTo(expected)
    }

    @ParameterizedTest
    @NullSource
    @ValueSource(strings = ["", "   "])
    fun `survives an empty body`(body: String?) {
        val mapped = AuthErrorMapper.map(400, body)
        assertThat(mapped.error).isEqualTo(AuthError.INVALID_CREDENTIALS)
        assertThat(mapped.serverMessage).isNull()
    }

    @Test
    fun `survives a body that is not json`() {
        val body = "<html><head><title>502 Bad Gateway</title></head></html>"
        val mapped = AuthErrorMapper.map(502, body)
        assertThat(mapped.error).isEqualTo(AuthError.SERVER)
        assertThat(mapped.serverMessage).isEqualTo(body)
    }

    @Test
    fun `truncates a long non-json body`() {
        assertThat(AuthErrorMapper.map(500, "x".repeat(500)).serverMessage?.length).isEqualTo(200)
    }

    @Test
    fun `error codes match case-insensitively`() {
        assertThat(AuthErrorMapper.map(400, """{ "error_code": "Weak_Password" }""").error)
            .isEqualTo(AuthError.WEAK_PASSWORD)
    }

    @Test
    fun `credentials are validated locally`() {
        assertThat(CredentialRules.validate("not-an-email", "longenough"))
            .isEqualTo(Result.Error(AuthError.EMAIL_REQUIRED))
        assertThat(CredentialRules.validate("a@b.dk", "short")).isEqualTo(Result.Error(AuthError.PASSWORD_TOO_SHORT))
        assertThat(CredentialRules.validate(" a@b.dk ", "longenough")).isEqualTo(Result.Success(Unit))
    }
}
