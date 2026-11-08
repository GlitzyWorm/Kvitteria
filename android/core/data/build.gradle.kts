/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

plugins {
    id("kvitteria.android.library")
    id("kvitteria.koin")
    id("kvitteria.ktor")
}

android {
    namespace = "dev.jelshoj.kvitteria.core.data"
}

dependencies {
    api(project(":core:domain"))
    implementation(libs.androidx.datastore.preferences)
    implementation(libs.kotlinx.coroutines.android)

    // The contract test covers every wire model, including the ones feature data modules own.
    testImplementation(project(":feature:sort:data"))
    testImplementation(project(":feature:pipeline:data"))
    testImplementation(project(":feature:settings:data"))
    testImplementation(project(":feature:capture:data"))
}
