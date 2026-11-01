/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.domain

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import java.math.BigDecimal

data class PipelineSettings(
    val pollIntervalSeconds: Int,
    val maxMessagesPerPoll: Int,
    val model: String,
    val thinkingMode: Boolean,
    val numCtx: Int,
    val keepAlive: String,
    val maxExtractionAttempts: Int,
    val retryTemperature: Double,
    val reconciliationToleranceKr: BigDecimal,
)

/**
 * What the Pipeline section edits. Numbers are text because the fields are, and it's a data class so
 * that "has anything changed" is an equality check against the saved values.
 */
data class PipelineSettingsDraft(
    val pollIntervalSeconds: String = "",
    val maxMessagesPerPoll: String = "",
    val model: String = "",
    val thinkingMode: Boolean = false,
    val numCtx: String = "",
    val keepAlive: String = "",
    val maxExtractionAttempts: String = "",
    val retryTemperature: String = "",
    val reconciliationToleranceKr: String = "",
)

fun PipelineSettings.toDraft(): PipelineSettingsDraft = PipelineSettingsDraft(
    pollIntervalSeconds = pollIntervalSeconds.toString(),
    maxMessagesPerPoll = maxMessagesPerPoll.toString(),
    model = model,
    thinkingMode = thinkingMode,
    numCtx = numCtx.toString(),
    keepAlive = keepAlive,
    maxExtractionAttempts = maxExtractionAttempts.toString(),
    retryTemperature = retryTemperature.toString(),
    reconciliationToleranceKr = reconciliationToleranceKr.toPlainString(),
)

enum class PipelineSettingsError {
    POLL_INTERVAL,
    MESSAGES_PER_POLL,
    NUM_CTX,
    MAX_EXTRACTION_ATTEMPTS,
    RETRY_TEMPERATURE,
    RECONCILIATION_TOLERANCE,
    MODEL_REQUIRED,
    KEEP_ALIVE_REQUIRED,
}

/** One error per field, because each shows its own under the field. */
data class PipelineSettingsFieldErrors(
    val pollIntervalSeconds: PipelineSettingsError? = null,
    val maxMessagesPerPoll: PipelineSettingsError? = null,
    val model: PipelineSettingsError? = null,
    val numCtx: PipelineSettingsError? = null,
    val keepAlive: PipelineSettingsError? = null,
    val maxExtractionAttempts: PipelineSettingsError? = null,
    val retryTemperature: PipelineSettingsError? = null,
    val reconciliationToleranceKr: PipelineSettingsError? = null,
) {
    val hasAny: Boolean get() = this != PipelineSettingsFieldErrors()
}

/**
 * Mirrors the server's validation field for field, plus two bounds the server doesn't enforce (retry
 * temperature and reconciliation tolerance), adopted from the design as client-only hints. A decimal
 * comma is accepted, because a Danish keyboard offers one.
 */
object PipelineSettingsDraftValidator {
    private fun String.dotted() = trim().replace(',', '.')

    private fun intOutOfRange(value: String, range: IntRange) = value.trim().toIntOrNull()?.let { it !in range } ?: true

    private fun decimalOutOfRange(value: String, min: BigDecimal, max: BigDecimal) =
        value.dotted().toBigDecimalOrNull()?.let { it < min || it > max } ?: true

    fun validate(draft: PipelineSettingsDraft): PipelineSettingsFieldErrors = PipelineSettingsFieldErrors(
        pollIntervalSeconds = PipelineSettingsError.POLL_INTERVAL
            .takeIf { intOutOfRange(draft.pollIntervalSeconds, 1..86_400) },
        maxMessagesPerPoll = PipelineSettingsError.MESSAGES_PER_POLL
            .takeIf { intOutOfRange(draft.maxMessagesPerPoll, 1..1_000) },
        model = PipelineSettingsError.MODEL_REQUIRED.takeIf { draft.model.isBlank() },
        numCtx = PipelineSettingsError.NUM_CTX.takeIf { intOutOfRange(draft.numCtx, 256..1_048_576) },
        keepAlive = PipelineSettingsError.KEEP_ALIVE_REQUIRED.takeIf { draft.keepAlive.isBlank() },
        maxExtractionAttempts = PipelineSettingsError.MAX_EXTRACTION_ATTEMPTS
            .takeIf { intOutOfRange(draft.maxExtractionAttempts, 1..10) },
        retryTemperature = PipelineSettingsError.RETRY_TEMPERATURE
            .takeIf { decimalOutOfRange(draft.retryTemperature, BigDecimal.ZERO, BigDecimal(2)) },
        reconciliationToleranceKr = PipelineSettingsError.RECONCILIATION_TOLERANCE
            .takeIf { decimalOutOfRange(draft.reconciliationToleranceKr, BigDecimal.ZERO, BigDecimal(100)) },
    )

    /** Only meaningful once [validate] has passed. */
    fun parse(draft: PipelineSettingsDraft): PipelineSettings = PipelineSettings(
        pollIntervalSeconds = draft.pollIntervalSeconds.trim().toInt(),
        maxMessagesPerPoll = draft.maxMessagesPerPoll.trim().toInt(),
        model = draft.model.trim(),
        thinkingMode = draft.thinkingMode,
        numCtx = draft.numCtx.trim().toInt(),
        keepAlive = draft.keepAlive.trim(),
        maxExtractionAttempts = draft.maxExtractionAttempts.trim().toInt(),
        retryTemperature = draft.retryTemperature.dotted().toDouble(),
        reconciliationToleranceKr = draft.reconciliationToleranceKr.dotted().toBigDecimal(),
    )
}

interface PipelineSettingsRepository {
    suspend fun settings(): Result<PipelineSettings, ApiError>

    suspend fun availableModels(): Result<List<String>, ApiError>

    suspend fun save(settings: PipelineSettings): Result<PipelineSettings, ApiError>
}
