/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.domain

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isFalse
import assertk.assertions.isNotNull
import assertk.assertions.isNull
import assertk.assertions.isTrue
import java.math.BigDecimal
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource

class PipelineSettingsDraftValidatorTest {
    private fun valid() = PipelineSettingsDraft(
        pollIntervalSeconds = "60",
        maxMessagesPerPoll = "25",
        model = "qwen3.5:4b",
        numCtx = "8192",
        keepAlive = "30m",
        maxExtractionAttempts = "3",
        retryTemperature = "0.3",
        reconciliationToleranceKr = "0.01",
    )

    private fun errors(draft: PipelineSettingsDraft) = PipelineSettingsDraftValidator.validate(draft)

    @Test
    fun `accepts a complete draft`() {
        assertThat(errors(valid()).hasAny).isFalse()
    }

    @ParameterizedTest
    @ValueSource(strings = ["1", "86400"])
    fun `accepts poll interval at the bounds`(value: String) {
        assertThat(errors(valid().copy(pollIntervalSeconds = value)).pollIntervalSeconds).isNull()
    }

    @ParameterizedTest
    @ValueSource(strings = ["0", "86401", "", "abc"])
    fun `rejects poll interval outside the bounds`(value: String) {
        assertThat(errors(valid().copy(pollIntervalSeconds = value)).pollIntervalSeconds)
            .isEqualTo(PipelineSettingsError.POLL_INTERVAL)
    }

    @ParameterizedTest
    @ValueSource(strings = ["1", "1000"])
    fun `accepts messages per poll at the bounds`(value: String) {
        assertThat(errors(valid().copy(maxMessagesPerPoll = value)).maxMessagesPerPoll).isNull()
    }

    @ParameterizedTest
    @ValueSource(strings = ["0", "1001"])
    fun `rejects messages per poll outside the bounds`(value: String) {
        assertThat(errors(valid().copy(maxMessagesPerPoll = value)).maxMessagesPerPoll)
            .isEqualTo(PipelineSettingsError.MESSAGES_PER_POLL)
    }

    @ParameterizedTest
    @ValueSource(strings = ["256", "1048576"])
    fun `accepts context window at the bounds`(value: String) {
        assertThat(errors(valid().copy(numCtx = value)).numCtx).isNull()
    }

    @ParameterizedTest
    @ValueSource(strings = ["255", "1048577"])
    fun `rejects context window outside the bounds`(value: String) {
        assertThat(errors(valid().copy(numCtx = value)).numCtx).isEqualTo(PipelineSettingsError.NUM_CTX)
    }

    @ParameterizedTest
    @ValueSource(strings = ["1", "10"])
    fun `accepts extraction attempts at the bounds`(value: String) {
        assertThat(errors(valid().copy(maxExtractionAttempts = value)).maxExtractionAttempts).isNull()
    }

    @ParameterizedTest
    @ValueSource(strings = ["0", "11"])
    fun `rejects extraction attempts outside the bounds`(value: String) {
        assertThat(errors(valid().copy(maxExtractionAttempts = value)).maxExtractionAttempts)
            .isEqualTo(PipelineSettingsError.MAX_EXTRACTION_ATTEMPTS)
    }

    @ParameterizedTest
    @ValueSource(strings = ["0", "2", "0,35"])
    fun `accepts retry temperature at the bounds`(value: String) {
        assertThat(errors(valid().copy(retryTemperature = value)).retryTemperature).isNull()
    }

    @ParameterizedTest
    @ValueSource(strings = ["-0.1", "2.1"])
    fun `rejects retry temperature outside the bounds`(value: String) {
        assertThat(errors(valid().copy(retryTemperature = value)).retryTemperature)
            .isEqualTo(PipelineSettingsError.RETRY_TEMPERATURE)
    }

    @ParameterizedTest
    @ValueSource(strings = ["0", "100"])
    fun `accepts reconciliation tolerance at the bounds`(value: String) {
        assertThat(errors(valid().copy(reconciliationToleranceKr = value)).reconciliationToleranceKr).isNull()
    }

    @ParameterizedTest
    @ValueSource(strings = ["-0.01", "100.01"])
    fun `rejects reconciliation tolerance outside the bounds`(value: String) {
        assertThat(errors(valid().copy(reconciliationToleranceKr = value)).reconciliationToleranceKr)
            .isEqualTo(PipelineSettingsError.RECONCILIATION_TOLERANCE)
    }

    @ParameterizedTest
    @ValueSource(strings = ["", "   "])
    fun `rejects a missing model`(model: String) {
        assertThat(errors(valid().copy(model = model)).model).isEqualTo(PipelineSettingsError.MODEL_REQUIRED)
    }

    @ParameterizedTest
    @ValueSource(strings = ["", "   "])
    fun `rejects a missing keep alive`(keepAlive: String) {
        assertThat(errors(valid().copy(keepAlive = keepAlive)).keepAlive)
            .isEqualTo(PipelineSettingsError.KEEP_ALIVE_REQUIRED)
    }

    @Test
    fun `reports every broken field at once`() {
        val found = errors(valid().copy(pollIntervalSeconds = "0", model = ""))
        assertThat(found.pollIntervalSeconds).isNotNull()
        assertThat(found.model).isNotNull()
        assertThat(found.maxMessagesPerPoll).isNull()
        assertThat(found.hasAny).isTrue()
    }

    @Test
    fun `parses every field`() {
        val parsed = PipelineSettingsDraftValidator.parse(valid())
        assertThat(parsed.pollIntervalSeconds).isEqualTo(60)
        assertThat(parsed.maxMessagesPerPoll).isEqualTo(25)
        assertThat(parsed.numCtx).isEqualTo(8192)
        assertThat(parsed.maxExtractionAttempts).isEqualTo(3)
        assertThat(parsed.retryTemperature).isEqualTo(0.3)
        assertThat(parsed.reconciliationToleranceKr).isEqualTo(BigDecimal("0.01"))
    }

    @Test
    fun `a saved value becomes an unchanged draft`() {
        val saved = PipelineSettingsDraftValidator.parse(valid())
        assertThat(saved.toDraft()).isEqualTo(valid())
    }
}
