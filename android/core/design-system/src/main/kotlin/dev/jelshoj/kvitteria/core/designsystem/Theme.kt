/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.designsystem

import androidx.compose.foundation.LocalIndication
import androidx.compose.foundation.text.selection.LocalTextSelectionColors
import androidx.compose.foundation.text.selection.TextSelectionColors
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.ripple
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider

/**
 * Maps the tokens onto a Material dark scheme, so that the system-styled pieces the app does use, such
 * as a bottom sheet or pull-to-refresh, come out dark. Dark regardless of the system setting.
 */
private val scheme = darkColorScheme(
    primary = KvColors.accent,
    onPrimary = KvColors.bg,
    primaryContainer = KvColors.accent800,
    onPrimaryContainer = KvColors.accent100,
    secondary = KvColors.accent400,
    onSecondary = KvColors.bg,
    tertiary = KvColors.accent300,
    onTertiary = KvColors.bg,
    background = KvColors.bg,
    onBackground = KvColors.text,
    surface = KvColors.bg,
    onSurface = KvColors.text,
    surfaceVariant = KvColors.surface,
    onSurfaceVariant = KvColors.neutral400,
    surfaceContainerLowest = KvColors.bg,
    surfaceContainerLow = KvColors.surface,
    surfaceContainer = KvColors.surface,
    surfaceContainerHigh = KvColors.surface,
    surfaceContainerHighest = KvColors.neutral900,
    outline = KvColors.divider,
    outlineVariant = KvColors.neutral800,
    error = KvColors.accent300,
    onError = KvColors.bg,
    scrim = KvColors.scrim,
)

private val selection = TextSelectionColors(handleColor = KvColors.accent, backgroundColor = KvColors.accentTint30)

@Composable
fun KvitteriaTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = scheme) {
        CompositionLocalProvider(
            LocalTextSelectionColors provides selection,
            LocalIndication provides ripple(color = KvColors.accent),
            content = content,
        )
    }
}
