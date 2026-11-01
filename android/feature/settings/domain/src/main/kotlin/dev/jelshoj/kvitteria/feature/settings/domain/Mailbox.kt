/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.domain

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Error
import dev.jelshoj.kvitteria.core.domain.Result
import java.time.Instant

data class Mailbox(
    val id: String,
    val ownerId: String,
    val ownerName: String,
    val ownerEmail: String,
    val host: String,
    val port: Int,
    val useSsl: Boolean,
    val username: String,
    val folder: String,
    val isEnabled: Boolean,
    val hasPassword: Boolean,
    val lastPolledAt: Instant?,
    val lastError: String?,
    val consecutiveFailures: Int,
)

/**
 * What the mailbox form edits. [port] is text because the field is: a half-typed port isn't a number.
 * [password] always starts empty, because the server never returns one, and left empty on an edit it
 * means keep the stored one.
 */
data class MailboxDraft(
    val id: String? = null,
    val ownerId: String = "",
    val host: String = "",
    val port: String = "993",
    val useSsl: Boolean = true,
    val folder: String = "",
    val username: String = "",
    val password: String = "",
    val isEnabled: Boolean = true,
) {
    val isEdit: Boolean get() = id != null
}

enum class MailboxDraftError : Error {
    REQUIRED_FIELDS,
    PORT,
    PASSWORD_REQUIRED,
    NO_OWNER,
}

/**
 * The completeness checking the server also does, run first so an empty field costs no IMAP connection
 * attempt. It doesn't check for a duplicate folder: the unique index owns that answer, and reports it
 * as a 409.
 */
object MailboxDraftValidator {
    const val MIN_PORT = 1
    const val MAX_PORT = 65535

    fun validate(draft: MailboxDraft): EmptyResult<MailboxDraftError> {
        if (draft.host.isBlank() || draft.folder.isBlank() || draft.username.isBlank()) {
            return Result.Error(MailboxDraftError.REQUIRED_FIELDS)
        }
        val port = draft.port.takeIf { text -> text.isNotEmpty() && text.all { it in '0'..'9' } }?.toIntOrNull()
        if (port == null || port !in MIN_PORT..MAX_PORT) return Result.Error(MailboxDraftError.PORT)

        // A new mailbox has no stored password to fall back on.
        if (!draft.isEdit && draft.password.isBlank()) return Result.Error(MailboxDraftError.PASSWORD_REQUIRED)
        if (draft.ownerId.isBlank()) return Result.Error(MailboxDraftError.NO_OWNER)
        return Result.Success(Unit)
    }

    /** Only meaningful once [validate] has passed. */
    fun parsePort(draft: MailboxDraft): Int = draft.port.toInt()
}

interface MailboxRepository {
    suspend fun mailboxes(): Result<List<Mailbox>, ApiError>

    suspend fun create(draft: MailboxDraft): Result<Mailbox, ApiError>

    /** An empty password keeps the stored one. */
    suspend fun update(draft: MailboxDraft): Result<Mailbox, ApiError>

    suspend fun setEnabled(mailbox: Mailbox, enabled: Boolean): Result<Mailbox, ApiError>

    suspend fun remove(id: String): EmptyResult<ApiError>
}
