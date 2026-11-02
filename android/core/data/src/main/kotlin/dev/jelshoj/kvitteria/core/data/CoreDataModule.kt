/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStoreFile
import dev.jelshoj.kvitteria.core.data.insights.KtorInsightsRepository
import dev.jelshoj.kvitteria.core.data.member.KtorMemberRepository
import dev.jelshoj.kvitteria.core.data.network.HttpClientFactory
import dev.jelshoj.kvitteria.core.data.receipt.KtorReceiptRepository
import dev.jelshoj.kvitteria.core.data.session.DataStoreServerSettings
import dev.jelshoj.kvitteria.core.data.session.DataStoreSessionStore
import dev.jelshoj.kvitteria.core.data.session.GoTrueSessionManager
import dev.jelshoj.kvitteria.core.data.session.GoTrueTokenClient
import dev.jelshoj.kvitteria.core.data.session.KeystoreSessionCipher
import dev.jelshoj.kvitteria.core.data.session.KtorServerProbe
import dev.jelshoj.kvitteria.core.data.session.SessionCipher
import dev.jelshoj.kvitteria.core.data.session.TokenClient
import dev.jelshoj.kvitteria.core.data.taxonomy.KtorTaxonomyRepository
import dev.jelshoj.kvitteria.core.data.update.KtorUpdateRepository
import dev.jelshoj.kvitteria.core.data.work.KtorWorkCounts
import dev.jelshoj.kvitteria.core.domain.WorkCounts
import dev.jelshoj.kvitteria.core.domain.insights.InsightsRepository
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptRepository
import dev.jelshoj.kvitteria.core.domain.session.ServerProbe
import dev.jelshoj.kvitteria.core.domain.session.ServerSettings
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.session.SessionStore
import dev.jelshoj.kvitteria.core.domain.session.UpdateRepository
import dev.jelshoj.kvitteria.core.domain.taxonomy.TaxonomyRepository
import io.ktor.client.engine.HttpClientEngine
import io.ktor.client.engine.okhttp.OkHttp
import java.time.Clock
import org.koin.android.ext.koin.androidContext
import org.koin.core.module.dsl.bind
import org.koin.core.module.dsl.singleOf
import org.koin.dsl.module

val coreDataModule = module {
    single<DataStore<Preferences>> {
        PreferenceDataStoreFactory.create { androidContext().preferencesDataStoreFile("kvitteria") }
    }
    single<Clock> { Clock.systemUTC() }
    single<HttpClientEngine> { OkHttp.create() }

    singleOf(::KeystoreSessionCipher) { bind<SessionCipher>() }
    singleOf(::DataStoreSessionStore) { bind<SessionStore>() }
    singleOf(::DataStoreServerSettings) { bind<ServerSettings>() }
    singleOf(::GoTrueTokenClient) { bind<TokenClient>() }
    singleOf(::GoTrueSessionManager) { bind<SessionManager>() }

    single { HttpClientFactory.anonymous(get(), get()) }
    single { HttpClientFactory.api(get(), get(), get()) }

    singleOf(::KtorReceiptRepository) { bind<ReceiptRepository>() }
    singleOf(::KtorTaxonomyRepository) { bind<TaxonomyRepository>() }
    singleOf(::KtorMemberRepository) { bind<MemberRepository>() }
    singleOf(::KtorInsightsRepository) { bind<InsightsRepository>() }
    singleOf(::KtorServerProbe) { bind<ServerProbe>() }
    singleOf(::KtorUpdateRepository) { bind<UpdateRepository>() }
    singleOf(::KtorWorkCounts) { bind<WorkCounts>() }
}
