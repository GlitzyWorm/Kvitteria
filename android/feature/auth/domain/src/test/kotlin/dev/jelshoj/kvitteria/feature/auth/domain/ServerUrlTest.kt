/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.domain

import assertk.assertThat
import assertk.assertions.isEqualTo
import dev.jelshoj.kvitteria.core.domain.Result
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource

class ServerUrlTest {
    @ParameterizedTest
    @CsvSource(
        "kvitteria.example.dk,https://kvitteria.example.dk",
        "'  kvitteria.example.dk  ',https://kvitteria.example.dk",
        "https://kvitteria.example.dk/,https://kvitteria.example.dk",
        "https://kvitteria.example.dk/api,https://kvitteria.example.dk",
        "https://kvitteria.example.dk/auth/v1,https://kvitteria.example.dk",
        "https://kvitteria.example.dk/scalar#tag/receipts,https://kvitteria.example.dk",
        "HTTPS://Kvitteria.Example.DK,https://kvitteria.example.dk",
    )
    fun `normalizes to a bare origin`(input: String, expected: String) {
        assertThat(ServerUrl.normalize(input)).isEqualTo(Result.Success(expected))
    }

    @ParameterizedTest
    @CsvSource(
        "10.0.2.2:8080,https://10.0.2.2:8080",
        "http://10.0.2.2:8080,http://10.0.2.2:8080",
        "http://10.0.2.2:8080/auth/v1,http://10.0.2.2:8080",
        "http://localhost:8081/api,http://localhost:8081",
    )
    fun `keeps a non-default port`(input: String, expected: String) {
        assertThat(ServerUrl.normalize(input)).isEqualTo(Result.Success(expected))
    }

    @Test
    fun `defaults a missing scheme to https`() {
        assertThat(ServerUrl.normalize("kvitteria.example.dk")).isEqualTo(Result.Success("https://kvitteria.example.dk"))
    }

    @Test
    fun `drops a default port`() {
        assertThat(ServerUrl.normalize("https://kvitteria.example.dk:443"))
            .isEqualTo(Result.Success("https://kvitteria.example.dk"))
    }

    @ParameterizedTest
    @CsvSource(
        "'',EMPTY",
        "'   ',EMPTY",
        ",EMPTY",
        "ftp://kvitteria.example.dk,UNSUPPORTED_SCHEME",
        "file:///etc/passwd,UNSUPPORTED_SCHEME",
        "https://,UNPARSEABLE",
        "'not an address',UNPARSEABLE",
    )
    fun `rejects what can't be an origin`(input: String?, expected: ServerUrlError) {
        assertThat(ServerUrl.normalize(input)).isEqualTo(Result.Error(expected))
    }

    @ParameterizedTest
    @CsvSource(
        "https://kvitteria.example.dk/api,true",
        "https://kvitteria.example.dk/auth/v1/,true",
        "https://kvitteria.example.dk/HEALTH,true",
        "https://kvitteria.example.dk,false",
        "https://api.example.dk,false",
    )
    fun `recognizes a pasted route`(input: String, expected: Boolean) {
        assertThat(ServerUrl.looksLikeKnownRoute(input)).isEqualTo(expected)
    }
}
