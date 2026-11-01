/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.data

import dev.jelshoj.kvitteria.core.data.network.AnonymousHttpClient
import dev.jelshoj.kvitteria.core.data.serialization.KvitteriaJson
import dev.jelshoj.kvitteria.core.data.session.AUTH_PREFIX
import dev.jelshoj.kvitteria.core.data.session.GoTrueSessionDto
import dev.jelshoj.kvitteria.core.data.session.toStoredSession
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import dev.jelshoj.kvitteria.feature.auth.domain.AuthError
import dev.jelshoj.kvitteria.feature.auth.domain.AuthErrorMapper
import dev.jelshoj.kvitteria.feature.auth.domain.AuthRepository
import io.ktor.client.request.post
import io.ktor.client.request.setBody
import io.ktor.client.statement.bodyAsText
import io.ktor.http.ContentType
import io.ktor.http.contentType
import io.ktor.http.isSuccess
import java.io.IOException
import java.time.Clock
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import org.koin.core.module.dsl.bind
import org.koin.core.module.dsl.singleOf
import org.koin.dsl.module

class GoTrueAuthRepository(
    private val http: AnonymousHttpClient,
    private val clock: Clock,
) : AuthRepository {
    @Serializable
    private data class PasswordGrant(val email: String, val password: String)

    override suspend fun signIn(email: String, password: String): Result<StoredSession, AuthError> =
        post("$AUTH_PREFIX/token?grant_type=password", email, password)

    override suspend fun signUp(email: String, password: String): Result<StoredSession, AuthError> =
        post("$AUTH_PREFIX/signup", email, password)

    private suspend fun post(route: String, email: String, password: String): Result<StoredSession, AuthError> {
        val (status, body) = try {
            val response = http.client.post(route) {
                contentType(ContentType.Application.Json)
                setBody(PasswordGrant(email.trim(), password))
            }
            response.status to response.bodyAsText()
        } catch (_: IOException) {
            return Result.Error(AuthError.NETWORK)
        }

        if (!status.isSuccess()) return Result.Error(AuthErrorMapper.map(status.value, body).error)

        val tokens = try {
            KvitteriaJson.decodeFromString<GoTrueSessionDto>(body)
        } catch (_: SerializationException) {
            return Result.Error(AuthError.GENERIC)
        }

        // A 200 with no tokens means confirmation is required, and this project sends no mail.
        if (!tokens.hasTokens) return Result.Error(AuthError.CONFIRMATION_REQUIRED)
        return Result.Success(tokens.toStoredSession(email.trim(), clock))
    }
}

val authDataModule = module {
    singleOf(::GoTrueAuthRepository) { bind<AuthRepository>() }
}
