/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.designsystem

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Stable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.delay

private const val TOAST_MILLIS = 2600L

/** Holds the message on show. A second message simply replaces the first. */
@Stable
class KvToastState {
    var message by mutableStateOf<String?>(null)
        private set

    suspend fun show(text: String, millis: Long = TOAST_MILLIS) {
        message = text
        delay(millis)
        if (message == text) message = null
    }
}

@Composable
fun rememberKvToastState(): KvToastState = remember { KvToastState() }

/** Wraps a screen with its toast above the content, announced to screen readers when it appears. */
@Composable
fun KvToastBox(
    state: KvToastState,
    modifier: Modifier = Modifier,
    content: @Composable BoxScope.() -> Unit,
) {
    Box(modifier.fillMaxSize()) {
        content()
        val message = state.message
        if (message != null) {
            val shape = RoundedCornerShape(10.dp)
            Row(
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .padding(KvSpace.gutter)
                    .widthIn(max = 420.dp)
                    .kvElevation(KvElevation.Large, shape)
                    .background(KvColors.surface, shape)
                    .padding(horizontal = 16.dp, vertical = 11.dp)
                    .semantics { liveRegion = LiveRegionMode.Polite },
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(9.dp),
            ) {
                KvIcon(PhosphorIcons.CheckCircle, contentDescription = null, tint = KvColors.accent)
                BasicText(message, style = KvType.small)
            }
        }
    }
}
