/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

import org.gradle.api.Plugin
import org.gradle.api.Project
import org.gradle.kotlin.dsl.dependencies

class KtorConventionPlugin : Plugin<Project> {
    override fun apply(target: Project) = with(target) {
        pluginManager.apply("kvitteria.serialization")
        dependencies {
            add("implementation", lib("ktor-client-core"))
            add("implementation", lib("ktor-client-okhttp"))
            add("implementation", lib("ktor-client-auth"))
            add("implementation", lib("ktor-client-content-negotiation"))
            add("implementation", lib("ktor-client-logging"))
            add("implementation", lib("ktor-serialization-kotlinx-json"))
            add("testImplementation", lib("ktor-client-mock"))
        }
    }
}
