/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.session

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.emptyPreferences
import androidx.datastore.preferences.core.stringPreferencesKey
import assertk.assertThat
import assertk.assertions.contains
import assertk.assertions.isEqualTo
import assertk.assertions.isFalse
import assertk.assertions.isNull
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import java.time.Instant
import java.util.Base64
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.jupiter.api.Test

/** Stands in for Keystore, which doesn't exist on a JVM. */
private class Base64Cipher : SessionCipher {
    override fun encrypt(plain: String): String = Base64.getEncoder().encodeToString(plain.toByteArray())

    override fun decrypt(encoded: String): String = String(Base64.getDecoder().decode(encoded))
}

/** In memory, because a file-backed DataStore can't replace its file on Windows while it's open. */
private class InMemoryDataStore : DataStore<Preferences> {
    private val state = MutableStateFlow(emptyPreferences())
    override val data: Flow<Preferences> = state

    override suspend fun updateData(transform: suspend (Preferences) -> Preferences): Preferences =
        transform(state.value).also { state.value = it }
}

class DataStoreSessionStoreTest {
    private val session = StoredSession("access", "refresh", Instant.parse("2026-08-26T12:00:00Z"), "you@household.dk")
    private val key = stringPreferencesKey("session")

    private fun dataStore() = InMemoryDataStore()

    @Test
    fun `a saved session reads back whole`() = runTest {
        val store = DataStoreSessionStore(dataStore(), Base64Cipher())
        store.save(session)
        assertThat(store.load()).isEqualTo(session)
    }

    @Test
    fun `the session is one value and not plain text`() = runTest {
        val dataStore = dataStore()
        DataStoreSessionStore(dataStore, Base64Cipher()).save(session)

        val preferences = dataStore.data.first()
        assertThat(preferences.asMap().size).isEqualTo(1)
        assertThat(preferences[key].orEmpty().contains("refresh")).isFalse()
    }

    @Test
    fun `no value reads as no session`() = runTest {
        assertThat(DataStoreSessionStore(dataStore(), Base64Cipher()).load()).isNull()
    }

    @Test
    fun `a corrupt value reads as no session and is discarded`() = runTest {
        val dataStore = dataStore()
        dataStore.edit { it[key] = "not base64 at all !!" }

        assertThat(DataStoreSessionStore(dataStore, Base64Cipher()).load()).isNull()
        assertThat(dataStore.data.first()[key]).isNull()
    }

    @Test
    fun `a value that decrypts to something else reads as no session`() = runTest {
        val dataStore = dataStore()
        dataStore.edit { it[key] = Base64.getEncoder().encodeToString("{\"unexpected\":true}".toByteArray()) }

        assertThat(DataStoreSessionStore(dataStore, Base64Cipher()).load()).isNull()
    }

    @Test
    fun `clearing removes the session`() = runTest {
        val store = DataStoreSessionStore(dataStore(), Base64Cipher())
        store.save(session)
        store.clear()
        assertThat(store.load()).isNull()
    }

    @Test
    fun `the server address persists and leaves the session alone`() = runTest {
        val dataStore = dataStore()
        val settings = DataStoreServerSettings(dataStore)
        val sessions = DataStoreSessionStore(dataStore, Base64Cipher())
        sessions.save(session)

        settings.save("https://kvitteria.example.dk")

        assertThat(DataStoreServerSettings(dataStore).load()).isEqualTo("https://kvitteria.example.dk")
        assertThat(sessions.load().toString()).contains("access")
    }
}
