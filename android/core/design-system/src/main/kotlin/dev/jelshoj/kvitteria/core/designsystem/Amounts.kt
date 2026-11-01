/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.designsystem

import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow

/**
 * The one place amounts are drawn: Montserrat with tabular figures, so a stacked column of them lines
 * up. It takes the formatted text, because formatting belongs to `core:presentation`.
 */
@Composable
fun KvAmount(
    text: String,
    modifier: Modifier = Modifier,
    style: TextStyle = KvType.amount,
    color: Color = style.color,
    textAlign: TextAlign = TextAlign.End,
) {
    BasicText(
        text = text,
        style = style.copy(color = color, textAlign = textAlign),
        maxLines = 1,
        overflow = TextOverflow.Ellipsis,
        modifier = modifier,
    )
}
