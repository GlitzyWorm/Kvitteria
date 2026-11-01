/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.pipeline.data

import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.data.network.safeEmptyCall
import dev.jelshoj.kvitteria.core.data.receipt.PagedResultDto
import dev.jelshoj.kvitteria.core.data.serialization.InstantSerializer
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.feature.pipeline.domain.MailboxActivity
import dev.jelshoj.kvitteria.feature.pipeline.domain.MailboxHealth
import dev.jelshoj.kvitteria.feature.pipeline.domain.PipelineRepository
import dev.jelshoj.kvitteria.feature.pipeline.domain.PipelineStatus
import dev.jelshoj.kvitteria.feature.pipeline.domain.ReceiptFailure
import io.ktor.client.request.delete
import io.ktor.client.request.get
import io.ktor.client.request.parameter
import io.ktor.client.request.post
import java.time.Instant
import kotlinx.serialization.Serializable
import org.koin.core.module.dsl.bind
import org.koin.core.module.dsl.singleOf
import org.koin.dsl.module

@Serializable
data class MailboxActivityDto(
    val mailboxId: String,
    val memberId: String,
    val memberDisplayName: String,
    val folder: String,
    @Serializable(with = InstantSerializer::class) val lastMessageAt: Instant?,
    val receiptsThisMonth: Int,
)

@Serializable
data class PipelineStatusDto(
    val queued: Int,
    val extracting: Int,
    val persisting: Int,
    val failed: Int,
    val unreconciled: Int,
    @Serializable(with = InstantSerializer::class) val lastPolledAt: Instant?,
    val pollIntervalSeconds: Int,
    val mailboxes: List<MailboxActivityDto>,
)

@Serializable
data class ReceiptFailureDto(
    val id: String,
    val subject: String,
    val from: String,
    @Serializable(with = InstantSerializer::class) val mailDate: Instant,
    @Serializable(with = InstantSerializer::class) val failedAt: Instant,
    val error: String,
    val attempts: Int,
    val memberId: String,
    val memberDisplayName: String,
    val mailboxId: String?,
)

/** The status report has no error or enabled flag, so those come from the mailbox list. */
@Serializable
data class MailboxHealthDto(val id: String, val isEnabled: Boolean, val lastError: String?)

fun PipelineStatusDto.toPipelineStatus(): PipelineStatus = PipelineStatus(
    queued = queued,
    extracting = extracting,
    persisting = persisting,
    failed = failed,
    unreconciled = unreconciled,
    lastPolledAt = lastPolledAt,
    pollIntervalSeconds = pollIntervalSeconds,
    mailboxes = mailboxes.map {
        MailboxActivity(it.mailboxId, it.memberDisplayName, it.folder, it.lastMessageAt, it.receiptsThisMonth)
    },
)

fun ReceiptFailureDto.toReceiptFailure(): ReceiptFailure =
    ReceiptFailure(id, subject, from, mailDate, failedAt, error, attempts, memberDisplayName)

class KtorPipelineRepository(private val api: ApiHttpClient) : PipelineRepository {
    override suspend fun status(): Result<PipelineStatus, ApiError> =
        safeCall<PipelineStatusDto> { api.client.get("/api/pipeline/status") }.map { it.toPipelineStatus() }

    override suspend fun failures(): Result<List<ReceiptFailure>, ApiError> =
        safeCall<PagedResultDto<ReceiptFailureDto>> {
            api.client.get("/api/pipeline/failures") {
                parameter("page", 1)
                parameter("pageSize", MAX_FAILURES)
            }
        }.map { page -> page.items.map { it.toReceiptFailure() } }

    override suspend fun mailboxHealth(): Result<List<MailboxHealth>, ApiError> =
        safeCall<List<MailboxHealthDto>> { api.client.get("/api/mailboxes") }
            .map { list -> list.map { MailboxHealth(it.id, it.isEnabled, it.lastError) } }

    override suspend fun retry(id: String): EmptyResult<ApiError> =
        safeEmptyCall { api.client.post("/api/pipeline/failures/$id/retry") }

    override suspend fun dismiss(id: String): EmptyResult<ApiError> =
        safeEmptyCall { api.client.delete("/api/pipeline/failures/$id") }

    private companion object {
        const val MAX_FAILURES = 100
    }
}

val pipelineDataModule = module {
    singleOf(::KtorPipelineRepository) { bind<PipelineRepository>() }
}
