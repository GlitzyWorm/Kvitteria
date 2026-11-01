/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain

sealed interface DataError : Error {
    enum class Network : DataError {
        BAD_REQUEST,
        REQUEST_TIMEOUT,
        UNAUTHORIZED,
        FORBIDDEN,
        NOT_FOUND,
        CONFLICT,
        TOO_MANY_REQUESTS,
        NO_INTERNET,
        PAYLOAD_TOO_LARGE,
        SERVER_ERROR,

        /** 502: the backend reached out and the other server said no, such as a mailbox verification. */
        BAD_GATEWAY,
        SERVICE_UNAVAILABLE,
        SERIALIZATION,
        UNKNOWN,
    }

    enum class Local : DataError {
        DISK_FULL,
        NOT_FOUND,
        UNKNOWN,
    }
}

/**
 * A failed `/api` call. The enum alone isn't enough here, because the `detail` of a 400, 409 or 502
 * names things no client could reconstruct, like the folders that do exist, and has to reach the screen
 * verbatim.
 */
data class ApiError(
    val kind: DataError.Network,
    val detail: String? = null,
) : Error
