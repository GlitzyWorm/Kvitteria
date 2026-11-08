/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

plugins {
    id("kvitteria.android.feature")
}

android {
    namespace = "dev.jelshoj.kvitteria.feature.capture.presentation"
}

dependencies {
    implementation(project(":feature:capture:domain"))
    implementation(libs.androidx.activity.compose)
    // Google Play services, under Google's terms: closed source, which the licence rule allows, and not GPL.
    implementation(libs.mlkit.document.scanner)
}
