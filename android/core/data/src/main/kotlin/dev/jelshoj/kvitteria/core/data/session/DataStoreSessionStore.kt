/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.session

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import dev.jelshoj.kvitteria.core.data.serialization.InstantSerializer
import dev.jelshoj.kvitteria.core.data.serialization.KvitteriaJson
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.SessionStore
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import java.time.Instant
import kotlin.coroutines.cancellation.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.serialization.Serializable

@Serializable
private data class SessionBlob(
    val accessToken: String,
    val refreshToken: String,
    @Serializable(with = InstantSerializer::class) val expiresAt: Instant,
    val email: String,
)

/**
 * The session as one encrypted value rather than a key per field. Writes aren't atomic across keys, so
 * separate keys could tear into a fresh access token and a spent refresh token, which is unrecoverable
 * because the refresh token is single-use.
 */
class DataStoreSessionStore(
    private val dataStore: DataStore<Preferences>,
    private val cipher: SessionCipher,
) : SessionStore {
    override suspend fun load(): StoredSession? {
        val stored = dataStore.data.first()[SESSION] ?: return null
        return try {
            val blob = KvitteriaJson.decodeFromString<SessionBlob>(cipher.decrypt(stored))
            StoredSession(blob.accessToken, blob.refreshToken, blob.expiresAt, blob.email)
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            // A lost key or a damaged value. Nothing can be recovered, and the sign-in screen the
            // person is about to see says everything there is to say.
            clear()
            null
        }
    }

    override suspend fun save(session: StoredSession) {
        val blob = SessionBlob(session.accessToken, session.refreshToken, session.expiresAt, session.email)
        val encrypted = cipher.encrypt(KvitteriaJson.encodeToString(SessionBlob.serializer(), blob))
        dataStore.edit { it[SESSION] = encrypted }
    }

    override suspend fun clear() {
        dataStore.edit { it.remove(SESSION) }
    }

    private companion object {
        val SESSION = stringPreferencesKey("session")
    }
}

/** The address isn't a secret, so it's stored as it is. */
class DataStoreServerSettings(private val dataStore: DataStore<Preferences>) : ServerSettings {
    private val current = MutableStateFlow<String?>(null)
    override val address: StateFlow<String?> = current.asStateFlow()

    override suspend fun load(): String? {
        current.value = dataStore.data.first()[ADDRESS]
        return current.value
    }

    override suspend fun save(address: String) {
        dataStore.edit { it[ADDRESS] = address }
        current.value = address
    }

    private companion object {
        val ADDRESS = stringPreferencesKey("server_address")
    }
}
