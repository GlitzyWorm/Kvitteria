/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

plugins {
    `kotlin-dsl`
}

group = "dev.jelshoj.kvitteria.buildlogic"

java {
    toolchain.languageVersion.set(JavaLanguageVersion.of(21))
}

dependencies {
    compileOnly(libs.android.gradlePlugin)
    compileOnly(libs.kotlin.gradlePlugin)
    compileOnly(libs.compose.gradlePlugin)
    compileOnly(libs.serialization.gradlePlugin)
}

gradlePlugin {
    plugins {
        register("androidApplication") {
            id = "kvitteria.android.application"
            implementationClass = "AndroidApplicationConventionPlugin"
        }
        register("androidLibrary") {
            id = "kvitteria.android.library"
            implementationClass = "AndroidLibraryConventionPlugin"
        }
        register("androidFeature") {
            id = "kvitteria.android.feature"
            implementationClass = "AndroidFeatureConventionPlugin"
        }
        register("domainModule") {
            id = "kvitteria.domain"
            implementationClass = "DomainModuleConventionPlugin"
        }
        register("compose") {
            id = "kvitteria.compose"
            implementationClass = "ComposeConventionPlugin"
        }
        register("koin") {
            id = "kvitteria.koin"
            implementationClass = "KoinConventionPlugin"
        }
        register("ktor") {
            id = "kvitteria.ktor"
            implementationClass = "KtorConventionPlugin"
        }
        register("serialization") {
            id = "kvitteria.serialization"
            implementationClass = "SerializationConventionPlugin"
        }
    }
}
