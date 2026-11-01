/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.designsystem

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.defaultMinSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Immutable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@Immutable
data class KvTab(
    val label: String,
    val glyph: KvGlyph,
    /** Outstanding work, hidden at zero. */
    val badge: Int = 0,
    /** What a screen reader says for the badge. */
    val badgeDescription: String? = null,
)

/** The bottom tabs. The selected tab keeps its glyph and takes the accent colour. */
@Composable
fun KvTabBar(
    tabs: List<KvTab>,
    selected: Int,
    onSelect: (Int) -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(modifier.fillMaxWidth().background(KvColors.surface)) {
        KvRule()
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .windowInsetsPadding(WindowInsets.navigationBars)
                .padding(horizontal = 4.dp)
                .selectableGroup(),
        ) {
            tabs.forEachIndexed { index, tab ->
                val color = if (index == selected) KvColors.accent else KvColors.neutral400
                Column(
                    modifier = Modifier
                        .weight(1f)
                        .heightIn(min = 56.dp)
                        .selectable(selected = index == selected, role = Role.Tab, onClick = { onSelect(index) })
                        .padding(top = 6.dp, bottom = 6.dp)
                        .semantics { tab.badgeDescription?.let { contentDescription = "${tab.label}, $it" } },
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(3.dp),
                ) {
                    Box {
                        KvIcon(tab.glyph, contentDescription = null, size = 20.dp, tint = color)
                        if (tab.badge > 0) {
                            BasicText(
                                text = tab.badge.toString(),
                                style = KvType.amountSmall.copy(
                                    fontSize = 9.sp,
                                    color = KvColors.bg,
                                    textAlign = TextAlign.Center,
                                ),
                                maxLines = 1,
                                modifier = Modifier
                                    .align(Alignment.TopEnd)
                                    .offset(x = 11.dp, y = (-4).dp)
                                    .defaultMinSize(minWidth = 15.dp, minHeight = 15.dp)
                                    .background(KvColors.accent, RoundedCornerShape(8.dp))
                                    .padding(horizontal = 3.dp),
                            )
                        }
                    }
                    BasicText(
                        text = tab.label,
                        style = KvType.meta.copy(fontSize = 10.sp, color = color, textAlign = TextAlign.Center),
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
            }
        }
    }
}
