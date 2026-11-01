/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.domain

import dev.jelshoj.kvitteria.core.domain.Error
import dev.jelshoj.kvitteria.core.domain.Result
import java.net.URI
import java.net.URISyntaxException

enum class ServerUrlError : Error {
    EMPTY,
    UNPARSEABLE,
    UNSUPPORTED_SCHEME,
}

/** Reduces what someone types into the server field to the one origin every request is built from. */
object ServerUrl {
    /**
     * Suffixes a person plausibly pastes because that's the URL they had in front of them. They're routes
     * on the origin rather than part of it, so they're dropped rather than rejected.
     */
    private val knownRoutes = listOf("/auth/v1", "/api", "/health", "/scalar", "/openapi")

    /**
     * Trims, defaults a missing scheme to https, and drops any path, query and fragment. The default is
     * the secure one on purpose: the insecure case is the one worth typing out. A pasted `/auth/v1` would
     * otherwise end up doubled in every request.
     */
    fun normalize(raw: String?): Result<String, ServerUrlError> {
        var candidate = raw.orEmpty().trim()
        if (candidate.isEmpty()) return Result.Error(ServerUrlError.EMPTY)

        // URI misreads "localhost:8080" as scheme "localhost", so the scheme is decided here.
        if ("://" !in candidate) candidate = "https://$candidate"

        val parsed = try {
            URI(candidate)
        } catch (_: URISyntaxException) {
            return Result.Error(ServerUrlError.UNPARSEABLE)
        }

        val scheme = parsed.scheme?.lowercase()
        if (scheme != "http" && scheme != "https") return Result.Error(ServerUrlError.UNSUPPORTED_SCHEME)

        val host = parsed.host?.lowercase() ?: return Result.Error(ServerUrlError.UNPARSEABLE)
        val isDefaultPort = parsed.port == -1 || (scheme == "https" && parsed.port == 443) ||
            (scheme == "http" && parsed.port == 80)

        return Result.Success(if (isDefaultPort) "$scheme://$host" else "$scheme://$host:${parsed.port}")
    }

    /** True when [raw] ends in a route that isn't part of the origin, so the UI can say it was removed. */
    fun looksLikeKnownRoute(raw: String?): Boolean {
        val candidate = raw.orEmpty().trim().trimEnd('/')
        return knownRoutes.any { candidate.endsWith(it, ignoreCase = true) }
    }
}
