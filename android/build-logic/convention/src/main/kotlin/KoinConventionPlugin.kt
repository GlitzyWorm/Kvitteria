/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

import org.gradle.api.Plugin
import org.gradle.api.Project
import org.gradle.kotlin.dsl.dependencies

class KoinConventionPlugin : Plugin<Project> {
    override fun apply(target: Project) = with(target) {
        dependencies {
            add("implementation", platform(lib("koin-bom")))
            add("implementation", lib("koin-core"))
            val isAndroid = pluginManager.hasPlugin("com.android.library") ||
                pluginManager.hasPlugin("com.android.application")
            if (isAndroid) {
                add("implementation", lib("koin-android"))
                add("implementation", lib("koin-compose"))
                add("implementation", lib("koin-compose-viewmodel"))
            }
        }
    }
}
