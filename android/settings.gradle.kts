/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

pluginManagement {
    includeBuild("build-logic")
    repositories {
        google {
            content {
                includeGroupByRegex("com\\.android.*")
                includeGroupByRegex("com\\.google.*")
                includeGroupByRegex("androidx.*")
            }
        }
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "kvitteria"

include(":app")
include(":core:domain", ":core:data", ":core:presentation", ":core:design-system")
for (feature in listOf("auth", "sort", "pipeline", "settings")) {
    include(":feature:$feature:domain", ":feature:$feature:data", ":feature:$feature:presentation")
}
include(":feature:overview:presentation", ":feature:receipts:presentation", ":feature:insights:presentation")
