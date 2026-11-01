/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import android.content.Context
import androidx.annotation.StringRes
import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError

/** A string that's a resource, or a value that may be one depending on what happened. */
sealed interface UiText {
    data class DynamicString(val value: String) : UiText

    data class StringResource(@param:StringRes val id: Int, val args: List<Any> = emptyList()) : UiText

    @Composable
    fun asString(): String = when (this) {
        is DynamicString -> value
        is StringResource -> stringResource(id, *args.toTypedArray())
    }

    fun asString(context: Context): String = when (this) {
        is DynamicString -> value
        is StringResource -> context.getString(id, *args.toTypedArray())
    }
}

fun DataError.Network.toUiText(): UiText = UiText.StringResource(
    when (this) {
        DataError.Network.NO_INTERNET, DataError.Network.REQUEST_TIMEOUT -> R.string.error_no_connection
        DataError.Network.FORBIDDEN -> R.string.error_forbidden
        DataError.Network.UNAUTHORIZED -> R.string.error_unauthorized
        DataError.Network.NOT_FOUND -> R.string.error_not_found
        DataError.Network.TOO_MANY_REQUESTS -> R.string.error_too_many_requests
        DataError.Network.SERVER_ERROR,
        DataError.Network.BAD_GATEWAY,
        DataError.Network.SERVICE_UNAVAILABLE,
        -> R.string.error_server
        DataError.Network.BAD_REQUEST,
        DataError.Network.CONFLICT,
        DataError.Network.PAYLOAD_TOO_LARGE,
        DataError.Network.SERIALIZATION,
        DataError.Network.UNKNOWN,
        -> R.string.error_unknown
    },
)

private val verbatim = setOf(
    DataError.Network.BAD_REQUEST,
    DataError.Network.CONFLICT,
    DataError.Network.BAD_GATEWAY,
    DataError.Network.SERVICE_UNAVAILABLE,
)

/**
 * The server's `detail` names things a client can't reconstruct, like the host or the folders that do
 * exist, so on these statuses it's shown as it is, in the server's language. Anything the app can
 * categorize for itself stays localized.
 */
fun ApiError.toUiText(): UiText =
    detail?.takeIf { kind in verbatim }?.let(UiText::DynamicString) ?: kind.toUiText()

/** For a screen where the server's message also answers a 404, as Pipeline's already-handled case does. */
fun ApiError.detailOrUiText(): UiText = detail?.let(UiText::DynamicString) ?: kind.toUiText()
