/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.network

import dev.jelshoj.kvitteria.core.data.serialization.KvitteriaJson
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionState
import io.ktor.client.HttpClient
import io.ktor.client.engine.HttpClientEngine
import io.ktor.client.plugins.HttpTimeout
import io.ktor.client.plugins.api.createClientPlugin
import io.ktor.client.plugins.auth.Auth
import io.ktor.client.plugins.auth.authProvider
import io.ktor.client.plugins.auth.providers.BearerAuthProvider
import io.ktor.client.plugins.auth.providers.BearerTokens
import io.ktor.client.plugins.auth.providers.bearer
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.client.HttpClientConfig
import io.ktor.http.URLBuilder
import io.ktor.http.takeFrom
import io.ktor.serialization.kotlinx.json.json
import java.io.IOException
import java.util.concurrent.atomic.AtomicReference

/**
 * The two clients this app holds differ by one plugin, and confusing them presents as an endless refresh
 * loop rather than a compile error, so each has its own type.
 *
 * This one has no bearer and no refresh. It's for GoTrue itself and for the anonymous probes.
 */
class AnonymousHttpClient(val client: HttpClient)

/** Attaches the bearer and keeps it current. Everything under `/api`. */
class ApiHttpClient(val client: HttpClient)

/** Raised when a request is made before anyone has chosen a server. */
class NoServerException : IOException("No server address is stored")

/** How long a probe waits before the server counts as unreachable, because it runs on the splash. */
private const val REQUEST_TIMEOUT_MS = 30_000L
private const val CONNECT_TIMEOUT_MS = 8_000L

/**
 * Fills in the server on any request with a relative URL, reading the address per request so a changed
 * server takes effect with no client to rebuild. An absolute URL is left alone, which is how a candidate
 * address is probed before it's stored.
 */
private fun serverAddressPlugin(settings: ServerSettings) = createClientPlugin("ServerAddress") {
    onRequest { request, _ ->
        if (request.url.host.isEmpty()) {
            val origin = URLBuilder().takeFrom(settings.address.value ?: settings.load() ?: throw NoServerException())
            request.url.protocol = origin.protocol
            request.url.host = origin.host
            request.url.port = origin.port
        }
    }
}

private fun HttpClientConfig<*>.configureShared(settings: ServerSettings) {
    expectSuccess = false
    install(ContentNegotiation) { json(KvitteriaJson) }
    install(HttpTimeout) {
        requestTimeoutMillis = REQUEST_TIMEOUT_MS
        connectTimeoutMillis = CONNECT_TIMEOUT_MS
    }
    install(serverAddressPlugin(settings))
}

object HttpClientFactory {
    fun anonymous(engine: HttpClientEngine, settings: ServerSettings): AnonymousHttpClient =
        AnonymousHttpClient(HttpClient(engine) { configureShared(settings) })

    /**
     * The bearer plugin caches what `loadTokens` returned, which would skip the expiry check and keep
     * serving a previous session's token. So the plugin installed in front of it asks the session on
     * every request and drops the cache as soon as the token differs.
     */
    fun api(engine: HttpClientEngine, settings: ServerSettings, sessions: SessionManager): ApiHttpClient {
        lateinit var client: HttpClient
        val lastToken = AtomicReference<String?>(null)

        val freshToken = createClientPlugin("FreshToken") {
            onRequest { _, _ ->
                val state = sessions.ensureFresh()
                if (state == SessionState.Unreachable) throw IOException("Could not reach the identity provider")
                val token = (state as? SessionState.Active)?.session?.accessToken
                if (lastToken.getAndSet(token) != token) client.authProvider<BearerAuthProvider>()?.clearToken()
            }
        }

        client = HttpClient(engine) {
            configureShared(settings)
            install(freshToken)
            install(Auth) {
                bearer {
                    loadTokens {
                        sessions.session.value?.let { BearerTokens(it.accessToken, it.refreshToken) }
                    }
                    refreshTokens {
                        val spent = oldTokens?.accessToken ?: return@refreshTokens null
                        when (val state = sessions.refresh(spent)) {
                            is SessionState.Active -> state.session.let { BearerTokens(it.accessToken, it.refreshToken) }
                            SessionState.Ended -> null
                            SessionState.Unreachable -> throw IOException("Could not reach the identity provider")
                        }
                    }
                }
            }
        }
        return ApiHttpClient(client)
    }
}
