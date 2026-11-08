/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria

import android.app.Application
import dev.jelshoj.kvitteria.feature.capture.data.captureDataModule
import dev.jelshoj.kvitteria.feature.capture.presentation.capturePresentationModule
import androidx.appcompat.app.AppCompatDelegate
import dev.jelshoj.kvitteria.core.data.coreDataModule
import dev.jelshoj.kvitteria.feature.auth.data.authDataModule
import dev.jelshoj.kvitteria.feature.auth.presentation.authPresentationModule
import dev.jelshoj.kvitteria.feature.insights.presentation.insightsPresentationModule
import dev.jelshoj.kvitteria.feature.overview.presentation.overviewPresentationModule
import dev.jelshoj.kvitteria.feature.pipeline.data.pipelineDataModule
import dev.jelshoj.kvitteria.feature.pipeline.presentation.pipelinePresentationModule
import dev.jelshoj.kvitteria.feature.receipts.presentation.receiptsPresentationModule
import dev.jelshoj.kvitteria.feature.settings.data.settingsDataModule
import dev.jelshoj.kvitteria.feature.settings.presentation.settingsPresentationModule
import dev.jelshoj.kvitteria.feature.sort.data.sortDataModule
import dev.jelshoj.kvitteria.feature.sort.presentation.sortPresentationModule
import dev.jelshoj.kvitteria.navigation.MainShellViewModel
import org.koin.android.ext.koin.androidContext
import org.koin.core.context.startKoin
import org.koin.core.module.dsl.viewModelOf
import org.koin.dsl.module

private val appModule = module {
    viewModelOf(::MainShellViewModel)
}

class KvitteriaApp : Application() {
    override fun onCreate() {
        super.onCreate()
        // Without this pin the platform's own dialogs and pickers come up light over a dark page.
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_YES)
        startKoin {
            androidContext(this@KvitteriaApp)
            modules(
                coreDataModule,
                authDataModule,
                authPresentationModule,
                settingsDataModule,
                settingsPresentationModule,
                receiptsPresentationModule,
                sortDataModule,
                sortPresentationModule,
                pipelineDataModule,
                pipelinePresentationModule,
                captureDataModule,
                capturePresentationModule,
                overviewPresentationModule,
                insightsPresentationModule,
                appModule,
            )
        }
    }
}
