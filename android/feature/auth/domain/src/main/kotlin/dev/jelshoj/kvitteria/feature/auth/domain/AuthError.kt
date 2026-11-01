/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.domain

import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Error
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

enum class AuthError : Error {
    EMAIL_REQUIRED,
    PASSWORD_TOO_SHORT,
    INVALID_CREDENTIALS,
    USER_ALREADY_EXISTS,
    WEAK_PASSWORD,
    EMAIL_INVALID,
    EMAIL_NOT_CONFIRMED,
    RATE_LIMITED,
    SIGNUP_DISABLED,
    SESSION_EXPIRED,
    CONFIRMATION_REQUIRED,
    SERVER,
    NETWORK,
    GENERIC,
}

/** Exchanges a password for a session and forgets it. The app never keeps one. */
interface AuthRepository {
    suspend fun signIn(email: String, password: String): Result<StoredSession, AuthError>

    suspend fun signUp(email: String, password: String): Result<StoredSession, AuthError>
}

object CredentialRules {
    private val email = Regex("""^[^@\s]+@[^@\s]+\.[^@\s]+$""")
    private const val MIN_PASSWORD = 8

    fun validate(emailAddress: String, password: String): EmptyResult<AuthError> = when {
        !email.matches(emailAddress.trim()) -> Result.Error(AuthError.EMAIL_REQUIRED)
        password.length < MIN_PASSWORD -> Result.Error(AuthError.PASSWORD_TOO_SHORT)
        else -> Result.Success(Unit)
    }
}

/** A mapped rejection, with the server's own wording for the debug log. It's never shown. */
data class MappedAuthError(val error: AuthError, val serverMessage: String?)

/**
 * What went wrong at the identity provider, in this app's terms, so a failure reaches the screen in the
 * person's language rather than as GoTrue's English.
 */
object AuthErrorMapper {
    private const val LOG_LIMIT = 200
    private val json = Json { ignoreUnknownKeys = true }

    private val byCode = mapOf(
        "invalid_credentials" to AuthError.INVALID_CREDENTIALS,
        "invalid_grant" to AuthError.INVALID_CREDENTIALS,
        "user_already_exists" to AuthError.USER_ALREADY_EXISTS,
        "email_exists" to AuthError.USER_ALREADY_EXISTS,
        "weak_password" to AuthError.WEAK_PASSWORD,
        "email_address_invalid" to AuthError.EMAIL_INVALID,
        "validation_failed" to AuthError.EMAIL_INVALID,
        "email_not_confirmed" to AuthError.EMAIL_NOT_CONFIRMED,
        "over_request_rate_limit" to AuthError.RATE_LIMITED,
        "over_email_send_rate_limit" to AuthError.RATE_LIMITED,
        "signup_disabled" to AuthError.SIGNUP_DISABLED,
        "refresh_token_not_found" to AuthError.SESSION_EXPIRED,
        "refresh_token_already_used" to AuthError.SESSION_EXPIRED,
    )

    private fun JsonObject.text(name: String) = (get(name) as? JsonPrimitive)?.takeIf { it.isString }?.content

    /**
     * Handles both shapes the service emits: the current `{code, error_code, msg}` and the OAuth-style
     * `{error, error_description}` the token endpoint still uses. With no code, the status still separates
     * "you got it wrong" from "the server is unwell".
     */
    fun map(status: Int, body: String?): MappedAuthError {
        var code: String? = null
        var message: String? = null

        if (!body.isNullOrBlank()) {
            try {
                val root = json.parseToJsonElement(body) as? JsonObject
                code = root?.text("error_code") ?: root?.text("error")
                message = if (root == null) {
                    body.take(LOG_LIMIT)
                } else {
                    root.text("msg") ?: root.text("error_description") ?: root.text("message")
                }
            } catch (_: SerializationException) {
                // A proxy or a wrong origin answering with HTML.
                message = body.take(LOG_LIMIT)
            }
        }

        val mapped = code?.lowercase()?.let(byCode::get) ?: when {
            status == 400 || status == 401 -> AuthError.INVALID_CREDENTIALS
            status == 422 -> AuthError.EMAIL_INVALID
            status == 429 -> AuthError.RATE_LIMITED
            status >= 500 -> AuthError.SERVER
            else -> AuthError.GENERIC
        }
        return MappedAuthError(mapped, message)
    }
}
