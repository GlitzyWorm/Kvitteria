/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.network

import dev.jelshoj.kvitteria.core.data.serialization.KvitteriaJson
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import io.ktor.client.call.body
import io.ktor.client.plugins.HttpRequestTimeoutException
import io.ktor.client.statement.HttpResponse
import io.ktor.client.statement.bodyAsText
import io.ktor.serialization.ContentConvertException
import java.io.IOException
import java.net.SocketTimeoutException
import java.nio.channels.UnresolvedAddressException
import kotlin.coroutines.cancellation.CancellationException
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

/**
 * The one field of an RFC 7807 response this app shows anyone. The backend puts its whole message in
 * `detail`, never in `title`, so reading that one string is the entire contract.
 */
object ProblemDetail {
    /**
     * The `detail` of a problem+json body, or null if there isn't one. A body that isn't JSON is null
     * rather than an exception: an error path is the worst place to throw a second error, and a proxy
     * answering 502 with HTML is a real thing that happens.
     */
    fun read(body: String?): String? {
        if (body.isNullOrBlank()) return null
        val root: JsonElement = try {
            KvitteriaJson.parseToJsonElement(body)
        } catch (_: SerializationException) {
            return null
        }
        val detail = (root as? JsonObject)?.get("detail") as? JsonPrimitive ?: return null
        return detail.takeIf { it.isString }?.content?.takeIf { it.isNotBlank() }
    }
}

fun statusToError(status: Int): DataError.Network = when (status) {
    400 -> DataError.Network.BAD_REQUEST
    401 -> DataError.Network.UNAUTHORIZED
    403 -> DataError.Network.FORBIDDEN
    404 -> DataError.Network.NOT_FOUND
    408 -> DataError.Network.REQUEST_TIMEOUT
    409 -> DataError.Network.CONFLICT
    413 -> DataError.Network.PAYLOAD_TOO_LARGE
    429 -> DataError.Network.TOO_MANY_REQUESTS
    502 -> DataError.Network.BAD_GATEWAY
    503 -> DataError.Network.SERVICE_UNAVAILABLE
    in 500..599 -> DataError.Network.SERVER_ERROR
    else -> DataError.Network.UNKNOWN
}

/** Turns anything that can go wrong with a request into a typed failure. Cancellation stays an exception. */
suspend inline fun <reified T> safeCall(execute: () -> HttpResponse): Result<T, ApiError> {
    val response = try {
        execute()
    } catch (e: Exception) {
        return Result.Error(ApiError(exceptionToError(e)))
    }
    return responseToResult(response)
}

/** For the writes that answer with no body worth reading. */
suspend inline fun safeEmptyCall(execute: () -> HttpResponse): EmptyResult<ApiError> {
    val response = try {
        execute()
    } catch (e: Exception) {
        return Result.Error(ApiError(exceptionToError(e)))
    }
    return if (response.status.value in 200..299) Result.Success(Unit) else failureOf(response)
}

suspend inline fun <reified T> responseToResult(response: HttpResponse): Result<T, ApiError> {
    if (response.status.value !in 200..299) return failureOf(response)
    return try {
        Result.Success(response.body<T>())
    } catch (e: Exception) {
        Result.Error(ApiError(exceptionToError(e)))
    }
}

suspend fun failureOf(response: HttpResponse): Result.Error<ApiError> {
    val detail = try {
        ProblemDetail.read(response.bodyAsText())
    } catch (e: Exception) {
        if (e is CancellationException) throw e
        null
    }
    return Result.Error(ApiError(statusToError(response.status.value), detail))
}

fun exceptionToError(e: Exception): DataError.Network = when (e) {
    is CancellationException -> throw e
    is SerializationException, is ContentConvertException -> DataError.Network.SERIALIZATION
    is HttpRequestTimeoutException, is SocketTimeoutException -> DataError.Network.REQUEST_TIMEOUT
    is UnresolvedAddressException, is IOException -> DataError.Network.NO_INTERNET
    else -> DataError.Network.UNKNOWN
}
