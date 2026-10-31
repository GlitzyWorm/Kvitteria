/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.android.library) apply false
    alias(libs.plugins.kotlin.jvm) apply false
    alias(libs.plugins.kotlin.compose) apply false
    alias(libs.plugins.kotlin.serialization) apply false
    alias(libs.plugins.spotless)
}

// MPL-2.0's copyleft is per file and attaches through this Exhibit A notice, so a source file without
// it is not clearly covered. spotlessCheck fails the build on a file that lacks it.
val mplHeader = """
    /*
     * This Source Code Form is subject to the terms of the Mozilla Public
     * License, v. 2.0. If a copy of the MPL was not distributed with this
     * file, You can obtain one at https://mozilla.org/MPL/2.0/.
     */

""".trimIndent() + "\n"

spotless {
    // .gitattributes keeps every checkout on LF, so the check is the same on Windows and on CI.
    lineEndings = com.diffplug.spotless.LineEnding.UNIX
    kotlin {
        target(
            fileTree(rootDir) {
                include("**/*.kt")
                exclude("**/build/**", "**/.gradle/**", "**/.kotlin/**")
            },
        )
        licenseHeader(mplHeader, "package |import |@file")
    }
    kotlinGradle {
        target(
            fileTree(rootDir) {
                include("**/*.gradle.kts")
                exclude("**/build/**", "**/.gradle/**", "**/.kotlin/**")
            },
        )
        licenseHeader(mplHeader, "import |plugins|pluginManagement|dependencyResolutionManagement|rootProject|@file")
    }
}
