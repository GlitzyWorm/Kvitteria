/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

plugins {
    id("kvitteria.android.library")
    id("kvitteria.compose")
}

android {
    namespace = "dev.jelshoj.kvitteria.core.presentation"
}

dependencies {
    implementation(project(":core:domain"))
    implementation(project(":core:design-system"))
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.appcompat)
}
