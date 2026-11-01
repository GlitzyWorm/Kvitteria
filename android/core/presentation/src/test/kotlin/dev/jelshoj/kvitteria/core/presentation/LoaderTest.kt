/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import app.cash.turbine.test
import assertk.assertThat
import assertk.assertions.isEqualTo
import assertk.assertions.isFalse
import assertk.assertions.isNotNull
import assertk.assertions.isNull
import assertk.assertions.isTrue
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.DataError
import dev.jelshoj.kvitteria.core.domain.Result
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.runTest
import org.junit.jupiter.api.Test

@OptIn(ExperimentalCoroutinesApi::class)
class LoaderTest {
    private val failed = Result.Error(ApiError(DataError.Network.NO_INTERNET))

    @Test
    fun `starts in the loading state`() = runTest {
        val loader = Loader<String>(backgroundScope) { Result.Success("x") }
        assertThat(loader.state.value.isLoading).isTrue()
        assertThat(loader.state.value.data).isNull()
    }

    @Test
    fun `a first failure is the error state`() = runTest(UnconfinedTestDispatcher()) {
        val loader = Loader<String>(backgroundScope) { failed }
        loader.refresh()

        assertThat(loader.state.value.error).isNotNull()
        assertThat(loader.state.value.isLoading).isFalse()
    }

    @Test
    fun `a failed refresh keeps the content and reports the failure`() = runTest(UnconfinedTestDispatcher()) {
        var result: Result<String, ApiError> = Result.Success("loaded")
        val loader = Loader(backgroundScope) { result }
        loader.refresh()

        loader.failures.test {
            result = failed
            loader.refresh()

            assertThat(awaitItem()).isEqualTo(UiText.StringResource(R.string.error_no_connection))
            assertThat(loader.state.value.data).isEqualTo("loaded")
            assertThat(loader.state.value.error).isNull()
            assertThat(loader.state.value.isLoading).isFalse()
        }
    }

    @Test
    fun `a refresh with content showing is refreshing rather than loading`() = runTest(UnconfinedTestDispatcher()) {
        val gate = CompletableDeferred<Unit>()
        var first = true
        val loader = Loader(backgroundScope) {
            if (first) first = false else gate.await()
            Result.Success("x")
        }
        loader.refresh()
        loader.refresh()

        assertThat(loader.state.value.isRefreshing).isTrue()
        gate.complete(Unit)
        assertThat(loader.state.value.isRefreshing).isFalse()
    }

    @Test
    fun `a newer refresh discards an older one`() = runTest(UnconfinedTestDispatcher()) {
        val slow = CompletableDeferred<Unit>()
        var calls = 0
        val loader = Loader(backgroundScope) {
            if (++calls == 1) {
                slow.await()
                Result.Success("old")
            } else {
                Result.Success("new")
            }
        }
        loader.refresh()
        loader.refresh()
        slow.complete(Unit)

        assertThat(loader.state.value.data).isEqualTo("new")
    }

    @Test
    fun `update changes what's held`() = runTest(UnconfinedTestDispatcher()) {
        val loader = Loader(backgroundScope) { Result.Success(1) }
        loader.refresh()
        loader.update { it + 1 }
        assertThat(loader.state.value.data).isEqualTo(2)
    }
}
