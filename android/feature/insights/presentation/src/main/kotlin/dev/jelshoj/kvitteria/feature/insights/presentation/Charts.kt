/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.domain.insights.TrendMonth
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import java.math.BigDecimal
import java.time.YearMonth

/** Six plain bars. Each is a tap target, and the selected month takes the accent. */
@Composable
internal fun TrendChart(months: List<TrendMonth>, selected: YearMonth, onSelect: (YearMonth) -> Unit) {
    val max = months.maxOfOrNull { it.total }?.takeIf { it.signum() > 0 } ?: BigDecimal.ONE

    Row(Modifier.fillMaxWidth().height(200.dp), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        months.forEach { month ->
            val isSelected = month.month == selected
            val fraction = month.total.toFloat().coerceAtLeast(0f) / max.toFloat()
            Column(
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight()
                    .clickable(role = Role.Button) { onSelect(month.month) },
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                BasicText(
                    KrFormat.amount(month.total, decimals = 0).removeSuffix(" kr"),
                    style = KvType.meta.copy(color = KvColors.neutral400, textAlign = TextAlign.Center),
                    maxLines = 1,
                )
                Box(Modifier.weight(1f).fillMaxWidth()) {
                    Canvas(Modifier.fillMaxWidth().fillMaxHeight().clearAndSetSemantics {}) {
                        val height = size.height * fraction
                        if (height > 0f) {
                            drawRoundRect(
                                color = if (isSelected) KvColors.accent else KvColors.accent800,
                                topLeft = Offset(0f, size.height - height),
                                size = Size(size.width, height),
                                cornerRadius = CornerRadius(KvRadius.sm.toPx()),
                            )
                        }
                    }
                }
                BasicText(
                    DanishTime.shortMonth(month.month),
                    style = KvType.meta.copy(
                        color = if (isSelected) KvColors.accent else KvColors.neutral500,
                        textAlign = TextAlign.Center,
                    ),
                )
            }
        }
    }
}
