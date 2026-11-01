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
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.domain.insights.PriceMonth
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

/** One line per run of priced months, so a month with no price breaks it rather than dropping to zero. */
@Composable
internal fun PriceChart(months: List<PriceMonth>) {
    val prices = months.mapNotNull { it.price?.toFloat() }
    val low = prices.minOrNull() ?: 0f
    val high = prices.maxOrNull() ?: 1f
    val span = (high - low).takeIf { it > 0f } ?: 1f

    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Canvas(Modifier.fillMaxWidth().height(150.dp).clearAndSetSemantics {}) {
            val inset = 8.dp.toPx()
            val step = size.width / months.size.coerceAtLeast(1)
            val point = { index: Int, price: Float ->
                val y = inset + (size.height - inset * 2) * (1f - (price - low) / span)
                Offset(step * (index + 0.5f), y)
            }

            val path = Path()
            var drawing = false
            months.forEachIndexed { index, month ->
                val price = month.price?.toFloat()
                if (price == null) {
                    drawing = false
                } else {
                    val at = point(index, price)
                    if (drawing) path.lineTo(at.x, at.y) else path.moveTo(at.x, at.y)
                    drawing = true
                }
            }
            drawPath(path, KvColors.accent, style = Stroke(width = 2.dp.toPx()))

            months.forEachIndexed { index, month ->
                month.price?.toFloat()?.let { drawCircle(KvColors.accent, radius = 3.dp.toPx(), center = point(index, it)) }
            }
        }
        Row(Modifier.fillMaxWidth()) {
            months.forEach { month ->
                BasicText(
                    DanishTime.shortMonth(month.month),
                    style = KvType.meta.copy(color = KvColors.neutral500, textAlign = TextAlign.Center),
                    modifier = Modifier.weight(1f),
                )
            }
        }
    }
}
