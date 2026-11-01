/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain

import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isFalse
import assertk.assertions.isNull
import assertk.assertions.isTrue
import org.junit.jupiter.api.Test

class ResultTest {
    private val failure: Result<Int, ApiError> = Result.Error(ApiError(DataError.Network.CONFLICT, "taken"))
    private val success: Result<Int, ApiError> = Result.Success(2)

    @Test
    fun `map transforms a success`() {
        assertThat(success.map { it * 2 }).isEqualTo(Result.Success(4))
    }

    @Test
    fun `map leaves a failure alone`() {
        assertThat(failure.map { it * 2 }).isEqualTo(failure)
    }

    @Test
    fun `onSuccess runs only on success and returns the same result`() {
        var seen: Int? = null
        assertThat(success.onSuccess { seen = it }).isEqualTo(success)
        assertThat(seen).isEqualTo(2)

        seen = null
        failure.onSuccess { seen = it }
        assertThat(seen).isNull()
    }

    @Test
    fun `onFailure runs only on failure and returns the same result`() {
        var ran = false
        assertThat(failure.onFailure { ran = true }).isEqualTo(failure)
        assertThat(ran).isTrue()

        ran = false
        success.onFailure { ran = true }
        assertThat(ran).isFalse()
    }

    @Test
    fun `asEmptyResult drops the value and keeps the failure`() {
        assertThat(success.asEmptyResult()).isEqualTo(Result.Success(Unit))
        assertThat(failure.asEmptyResult()).isEqualTo(failure)
    }
}
