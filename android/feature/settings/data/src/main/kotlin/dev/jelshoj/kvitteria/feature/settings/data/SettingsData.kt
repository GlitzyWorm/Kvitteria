/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.data

import dev.jelshoj.kvitteria.core.data.member.AppUserDto
import dev.jelshoj.kvitteria.core.data.member.toMember
import dev.jelshoj.kvitteria.core.data.member.toPermissions
import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.data.network.safeEmptyCall
import dev.jelshoj.kvitteria.core.data.serialization.InstantSerializer
import dev.jelshoj.kvitteria.core.data.serialization.MoneySerializer
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.EmptyResult
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.feature.settings.domain.HouseholdRepository
import dev.jelshoj.kvitteria.feature.settings.domain.Mailbox
import dev.jelshoj.kvitteria.feature.settings.domain.MailboxDraft
import dev.jelshoj.kvitteria.feature.settings.domain.MailboxDraftValidator
import dev.jelshoj.kvitteria.feature.settings.domain.MailboxRepository
import dev.jelshoj.kvitteria.feature.settings.domain.MemberPermissions
import dev.jelshoj.kvitteria.feature.settings.domain.PipelineSettings
import dev.jelshoj.kvitteria.feature.settings.domain.PipelineSettingsRepository
import io.ktor.client.request.delete
import io.ktor.client.request.get
import io.ktor.client.request.patch
import io.ktor.client.request.post
import io.ktor.client.request.put
import io.ktor.client.request.setBody
import io.ktor.http.ContentType
import io.ktor.http.contentType
import java.math.BigDecimal
import java.time.Instant
import kotlinx.serialization.Serializable
import org.koin.core.module.dsl.bind
import org.koin.core.module.dsl.singleOf
import org.koin.dsl.module

@Serializable
data class MailboxDto(
    val id: String,
    val appUserId: String,
    val memberDisplayName: String,
    val memberEmail: String,
    val host: String,
    val port: Int,
    val useSsl: Boolean,
    val username: String,
    val folder: String,
    val isEnabled: Boolean,
    val hasPassword: Boolean,
    @Serializable(with = InstantSerializer::class) val lastPolledAt: Instant?,
    val lastError: String?,
    val consecutiveFailures: Int,
)

@Serializable
data class CreateMailboxDto(
    val appUserId: String,
    val host: String,
    val port: Int,
    val useSsl: Boolean,
    val username: String,
    val password: String,
    val folder: String,
    val isEnabled: Boolean,
)

@Serializable
data class PutMailboxDto(
    val appUserId: String,
    val host: String,
    val port: Int,
    val useSsl: Boolean,
    val username: String,
    val password: String?,
    val folder: String,
    val isEnabled: Boolean,
)

@Serializable
data class PipelineSettingsDto(
    val imapPollIntervalSeconds: Int,
    val imapMaxMessagesPerPoll: Int,
    val ollamaModel: String,
    val ollamaThinkingMode: Boolean,
    val ollamaNumCtx: Int,
    val ollamaKeepAlive: String,
    val ollamaMaxExtractionAttempts: Int,
    val ollamaRetryTemperature: Double,
    @Serializable(with = MoneySerializer::class) val ollamaReconciliationToleranceKr: BigDecimal,
)

@Serializable
data class OllamaModelsDto(val models: List<String>)

@Serializable
data class CreateAppUserDto(val email: String, val displayName: String?, val role: String?)

@Serializable
data class PatchAppUserDto(val displayName: String?, val role: String?, val status: String?)

@Serializable
data class MemberPermissionsDto(val permissions: List<String>, val availablePermissions: List<String>)

@Serializable
data class PutMemberPermissionsDto(val permissions: List<String>)

fun MailboxDto.toMailbox(): Mailbox = Mailbox(
    id = id,
    ownerId = appUserId,
    ownerName = memberDisplayName,
    ownerEmail = memberEmail,
    host = host,
    port = port,
    useSsl = useSsl,
    username = username,
    folder = folder,
    isEnabled = isEnabled,
    hasPassword = hasPassword,
    lastPolledAt = lastPolledAt,
    lastError = lastError,
    consecutiveFailures = consecutiveFailures,
)

fun PipelineSettingsDto.toPipelineSettings(): PipelineSettings = PipelineSettings(
    pollIntervalSeconds = imapPollIntervalSeconds,
    maxMessagesPerPoll = imapMaxMessagesPerPoll,
    model = ollamaModel,
    thinkingMode = ollamaThinkingMode,
    numCtx = ollamaNumCtx,
    keepAlive = ollamaKeepAlive,
    maxExtractionAttempts = ollamaMaxExtractionAttempts,
    retryTemperature = ollamaRetryTemperature,
    reconciliationToleranceKr = ollamaReconciliationToleranceKr,
)

fun PipelineSettings.toDto(): PipelineSettingsDto = PipelineSettingsDto(
    imapPollIntervalSeconds = pollIntervalSeconds,
    imapMaxMessagesPerPoll = maxMessagesPerPoll,
    ollamaModel = model,
    ollamaThinkingMode = thinkingMode,
    ollamaNumCtx = numCtx,
    ollamaKeepAlive = keepAlive,
    ollamaMaxExtractionAttempts = maxExtractionAttempts,
    ollamaRetryTemperature = retryTemperature,
    ollamaReconciliationToleranceKr = reconciliationToleranceKr,
)

fun MemberPermissionsDto.toMemberPermissions(): MemberPermissions {
    val known = availablePermissions.toPermissions()
    return MemberPermissions(permissions.toPermissions(), Permission.entries.filter { it in known })
}

class KtorMailboxRepository(private val api: ApiHttpClient) : MailboxRepository {
    override suspend fun mailboxes(): Result<List<Mailbox>, ApiError> =
        safeCall<List<MailboxDto>> { api.client.get("/api/mailboxes") }.map { list -> list.map { it.toMailbox() } }

    override suspend fun create(draft: MailboxDraft): Result<Mailbox, ApiError> =
        safeCall<MailboxDto> {
            api.client.post("/api/mailboxes") {
                contentType(ContentType.Application.Json)
                setBody(
                    CreateMailboxDto(
                        appUserId = draft.ownerId,
                        host = draft.host.trim(),
                        port = MailboxDraftValidator.parsePort(draft),
                        useSsl = draft.useSsl,
                        username = draft.username.trim(),
                        password = draft.password,
                        folder = draft.folder.trim(),
                        isEnabled = draft.isEnabled,
                    ),
                )
            }
        }.map { it.toMailbox() }

    override suspend fun update(draft: MailboxDraft): Result<Mailbox, ApiError> =
        put(
            id = checkNotNull(draft.id),
            body = PutMailboxDto(
                appUserId = draft.ownerId,
                host = draft.host.trim(),
                port = MailboxDraftValidator.parsePort(draft),
                useSsl = draft.useSsl,
                username = draft.username.trim(),
                password = draft.password.ifEmpty { null },
                folder = draft.folder.trim(),
                isEnabled = draft.isEnabled,
            ),
        )

    override suspend fun setEnabled(mailbox: Mailbox, enabled: Boolean): Result<Mailbox, ApiError> =
        put(
            id = mailbox.id,
            body = PutMailboxDto(
                appUserId = mailbox.ownerId,
                host = mailbox.host,
                port = mailbox.port,
                useSsl = mailbox.useSsl,
                username = mailbox.username,
                password = null,
                folder = mailbox.folder,
                isEnabled = enabled,
            ),
        )

    private suspend fun put(id: String, body: PutMailboxDto): Result<Mailbox, ApiError> =
        safeCall<MailboxDto> {
            api.client.put("/api/mailboxes/$id") {
                contentType(ContentType.Application.Json)
                setBody(body)
            }
        }.map { it.toMailbox() }

    override suspend fun remove(id: String): EmptyResult<ApiError> =
        safeEmptyCall { api.client.delete("/api/mailboxes/$id") }
}

class KtorPipelineSettingsRepository(private val api: ApiHttpClient) : PipelineSettingsRepository {
    override suspend fun settings(): Result<PipelineSettings, ApiError> =
        safeCall<PipelineSettingsDto> { api.client.get("/api/settings/pipeline") }.map { it.toPipelineSettings() }

    override suspend fun availableModels(): Result<List<String>, ApiError> =
        safeCall<OllamaModelsDto> { api.client.get("/api/settings/pipeline/ollama-models") }.map { it.models }

    override suspend fun save(settings: PipelineSettings): Result<PipelineSettings, ApiError> =
        safeCall<PipelineSettingsDto> {
            api.client.put("/api/settings/pipeline") {
                contentType(ContentType.Application.Json)
                setBody(settings.toDto())
            }
        }.map { it.toPipelineSettings() }
}

class KtorHouseholdRepository(private val api: ApiHttpClient) : HouseholdRepository {
    override suspend fun invite(email: String, displayName: String?, role: Role): Result<Member, ApiError> =
        safeCall<AppUserDto> {
            api.client.post("/api/users") {
                contentType(ContentType.Application.Json)
                setBody(CreateAppUserDto(email.trim(), displayName?.trim()?.ifEmpty { null }, role.name))
            }
        }.map { it.toMember() }

    override suspend fun updateMember(
        id: String,
        displayName: String?,
        role: Role,
        status: MemberStatus?,
    ): Result<Member, ApiError> =
        safeCall<AppUserDto> {
            api.client.patch("/api/users/$id") {
                contentType(ContentType.Application.Json)
                setBody(PatchAppUserDto(displayName?.trim()?.ifEmpty { null }, role.name, status?.name))
            }
        }.map { it.toMember() }

    override suspend fun memberPermissions(): Result<MemberPermissions, ApiError> =
        safeCall<MemberPermissionsDto> { api.client.get("/api/settings/permissions") }.map { it.toMemberPermissions() }

    override suspend fun saveMemberPermissions(granted: Set<Permission>): Result<MemberPermissions, ApiError> =
        safeCall<MemberPermissionsDto> {
            api.client.put("/api/settings/permissions") {
                contentType(ContentType.Application.Json)
                setBody(PutMemberPermissionsDto(granted.map { it.name }))
            }
        }.map { it.toMemberPermissions() }
}

val settingsDataModule = module {
    singleOf(::KtorMailboxRepository) { bind<MailboxRepository>() }
    singleOf(::KtorPipelineSettingsRepository) { bind<PipelineSettingsRepository>() }
    singleOf(::KtorHouseholdRepository) { bind<HouseholdRepository>() }
}
