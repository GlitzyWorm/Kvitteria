/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.pipeline.domain

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import java.time.Instant

data class MailboxActivity(
    val mailboxId: String,
    val memberName: String,
    val folder: String,
    val lastMessageAt: Instant?,
    val receiptsThisMonth: Int,
)

data class PipelineStatus(
    val queued: Int,
    val extracting: Int,
    val persisting: Int,
    val failed: Int,
    val unreconciled: Int,
    val lastPolledAt: Instant?,
    val pollIntervalSeconds: Int,
    val mailboxes: List<MailboxActivity>,
)

/** Where a failed intake came from. Unknown is a source a newer server added. */
enum class FailureSource { Mail, Upload, Unknown }

/**
 * A mail or upload that didn't become a receipt. An upload has no [subject] or [from], and its
 * [mailDate] is when the image was taken.
 */
data class ReceiptFailure(
    val id: String,
    val subject: String,
    val from: String,
    val mailDate: Instant,
    val failedAt: Instant,
    val error: String,
    val attempts: Int,
    val memberName: String,
    val source: FailureSource = FailureSource.Mail,
    /** When it reached the server: for an upload, the upload time. */
    val receivedAt: Instant = mailDate,
    val hasTranscription: Boolean = false,
)

/** The part of a mailbox the status report doesn't carry. */
data class MailboxHealth(val mailboxId: String, val isEnabled: Boolean, val lastError: String?)

enum class MailboxState { Connected, Failing, Paused }

fun MailboxHealth?.toState(): MailboxState = when {
    this == null -> MailboxState.Connected
    !isEnabled -> MailboxState.Paused
    lastError != null -> MailboxState.Failing
    else -> MailboxState.Connected
}

interface PipelineRepository {
    suspend fun status(): Result<PipelineStatus, ApiError>

    suspend fun failures(): Result<List<ReceiptFailure>, ApiError>

    suspend fun mailboxHealth(): Result<List<MailboxHealth>, ApiError>

    suspend fun retry(id: String): EmptyResult<ApiError>

    suspend fun dismiss(id: String): EmptyResult<ApiError>
}
