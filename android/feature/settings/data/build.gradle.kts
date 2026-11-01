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
    namespace = "dev.jelshoj.kvitteria.feature.settings.data"
}

dependencies {
    implementation(project(":core:domain"))
    implementation(project(":core:data"))
    implementation(project(":feature:settings:domain"))
    implementation(libs.kotlinx.coroutines.android)
}
