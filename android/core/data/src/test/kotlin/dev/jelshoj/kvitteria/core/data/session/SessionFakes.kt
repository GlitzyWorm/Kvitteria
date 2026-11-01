/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.session

import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.SessionStore
import dev.jelshoj.kvitteria.core.domain.session.StoredSession
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class FakeSessionStore(var stored: StoredSession? = null) : SessionStore {
    override suspend fun load(): StoredSession? = stored

    override suspend fun save(session: StoredSession) {
        stored = session
    }

    override suspend fun clear() {
        stored = null
    }
}

class FakeServerSettings(address: String? = "http://server.test:8080") : ServerSettings {
    private val current = MutableStateFlow(address)
    override val address: StateFlow<String?> = current

    override suspend fun load(): String? = current.value

    override suspend fun save(address: String) {
        current.value = address
    }
}
