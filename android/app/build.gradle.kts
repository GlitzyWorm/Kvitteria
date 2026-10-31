/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

plugins {
    id("kvitteria.android.application")
    id("kvitteria.koin")
}

android {
    namespace = "dev.jelshoj.kvitteria"

    defaultConfig {
        applicationId = "dev.jelshoj.kvitteria"
        // versionCode only ever increases; CI passes the run number.
        versionCode = (findProperty("versionCode") as String?)?.toInt() ?: 1
        versionName = "0.0.0" // x-release-please-version
    }
}

dependencies {
    implementation(project(":core:domain"))
    implementation(project(":core:data"))
    implementation(project(":core:presentation"))
    implementation(project(":core:design-system"))
    implementation(project(":feature:auth:domain"))
    implementation(project(":feature:auth:data"))
    implementation(project(":feature:auth:presentation"))
    implementation(project(":feature:sort:domain"))
    implementation(project(":feature:sort:data"))
    implementation(project(":feature:sort:presentation"))
    implementation(project(":feature:pipeline:domain"))
    implementation(project(":feature:pipeline:data"))
    implementation(project(":feature:pipeline:presentation"))
    implementation(project(":feature:settings:domain"))
    implementation(project(":feature:settings:data"))
    implementation(project(":feature:settings:presentation"))
    implementation(project(":feature:overview:presentation"))
    implementation(project(":feature:receipts:presentation"))
    implementation(project(":feature:insights:presentation"))

    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.activity.compose)
}
