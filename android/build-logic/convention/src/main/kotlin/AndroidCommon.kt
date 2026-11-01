/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

import com.android.build.api.dsl.CommonExtension
import org.gradle.api.JavaVersion
import org.gradle.api.Project
import org.gradle.api.tasks.testing.Test
import org.gradle.kotlin.dsl.dependencies
import org.gradle.kotlin.dsl.withType

const val COMPILE_SDK = 37
const val MIN_SDK = 26
const val TARGET_SDK = 36

internal fun Project.configureAndroidCommon(extension: CommonExtension) {
    extension.apply {
        compileSdk = COMPILE_SDK
        defaultConfig.minSdk = MIN_SDK
        compileOptions.sourceCompatibility = JavaVersion.VERSION_21
        compileOptions.targetCompatibility = JavaVersion.VERSION_21
    }
    tasks.withType<Test>().configureEach { useJUnitPlatform() }
    configureTestDependencies()
}

internal fun Project.configureTestDependencies() {
    dependencies {
        add("testImplementation", lib("junit-jupiter"))
        add("testImplementation", lib("assertk"))
        add("testImplementation", lib("turbine"))
        add("testImplementation", lib("kotlinx-coroutines-test"))
        add("testRuntimeOnly", lib("junit-platform-launcher"))
    }
}
