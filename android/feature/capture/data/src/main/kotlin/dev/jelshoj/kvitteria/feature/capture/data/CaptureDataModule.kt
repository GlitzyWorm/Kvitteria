/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.data

import dev.jelshoj.kvitteria.feature.capture.domain.ImagePreparer
import dev.jelshoj.kvitteria.feature.capture.domain.InMemoryPendingCapture
import dev.jelshoj.kvitteria.feature.capture.domain.PendingCapture
import dev.jelshoj.kvitteria.feature.capture.domain.ReceiptUploader
import org.koin.android.ext.koin.androidContext
import org.koin.core.module.dsl.bind
import org.koin.core.module.dsl.singleOf
import org.koin.dsl.module

val captureDataModule = module {
    single<ImagePreparer> { AndroidImagePreparer(androidContext()) }
    singleOf(::KtorReceiptUploader) { bind<ReceiptUploader>() }
    singleOf(::InMemoryPendingCapture) { bind<PendingCapture>() }
}
