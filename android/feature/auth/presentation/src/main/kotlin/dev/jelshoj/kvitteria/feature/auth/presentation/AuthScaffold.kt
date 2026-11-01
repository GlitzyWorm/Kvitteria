/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.presentation

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvEyebrow
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvPanel
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.KvWordmark

/** The frame every screen before the tabs shares: the wordmark above one panel, clear of the keyboard. */
@Composable
internal fun AuthScaffold(
    kicker: String,
    title: String,
    blurb: String?,
    modifier: Modifier = Modifier,
    foot: String? = null,
    content: @Composable ColumnScope.() -> Unit,
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .background(KvColors.bg)
            .safeDrawingPadding()
            .imePadding()
            .verticalScroll(rememberScrollState())
            .padding(KvSpace.gutter),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(KvSpace.s8),
    ) {
        Spacer(Modifier.height(KvSpace.s8))
        KvWordmark()
        KvPanel(Modifier.widthIn(max = 460.dp).fillMaxWidth()) {
            KvEyebrow(kicker)
            KvHeading(title)
            if (blurb != null) BasicText(blurb, style = KvType.small.copy(color = KvColors.neutral300))
            Spacer(Modifier.height(KvSpace.s1))
            content()
        }
        if (foot != null) {
            BasicText(
                foot,
                style = KvType.label.copy(color = KvColors.neutral500),
                modifier = Modifier.widthIn(max = 460.dp).fillMaxWidth(),
            )
        }
    }
}
