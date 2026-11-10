/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.insights.presentation

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.drawText
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvElevation
import dev.jelshoj.kvitteria.core.designsystem.KvRadius
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.kvElevation
import dev.jelshoj.kvitteria.core.domain.insights.PriceMonth
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.KrFormat
import java.math.BigDecimal
import java.time.YearMonth

private val PlotHeight = 170.dp
private val Inset = 10.dp

/** Where the chart puts a price: a column per month, and the axis' lowest and highest tick as its edges. */
private class Plot(
    val width: Float,
    val height: Float,
    val gutter: Float,
    val inset: Float,
    val low: Double,
    val high: Double,
    val count: Int,
) {
    fun y(price: Double): Float = inset + (height - inset * 2) * (1f - ((price - low) / (high - low)).toFloat())

    fun x(index: Int): Float = gutter + (width - gutter) / count.coerceAtLeast(1) * (index + 0.5f)
}

/**
 * The price per month as one line per run of priced months, so a gap breaks it rather than dropping to zero.
 * Tapping a column selects the nearest priced month and shows its price and number of purchases.
 */
@Composable
internal fun PriceChart(months: List<PriceMonth>, selected: YearMonth?, onSelect: (YearMonth) -> Unit) {
    val prices = months.mapNotNull { it.price?.toDouble() }
    val ticks = remember(months) { if (prices.isEmpty()) emptyList() else PriceAxis.ticks(prices.min(), prices.max()) }
    val wholeKroner = ticks.all { it == Math.rint(it) }
    val labels = ticks.map { KrFormat.amount(BigDecimal.valueOf(it), if (wholeKroner) 0 else 2).removeSuffix("kr").trimEnd() }

    val measurer = rememberTextMeasurer()
    val density = LocalDensity.current
    val labelStyle = KvType.meta.copy(color = KvColors.neutral500)
    val gutterPx = with(density) {
        (labels.maxOfOrNull { measurer.measure(it, labelStyle).size.width } ?: 0) + 8.dp.toPx()
    }
    val gutter = with(density) { gutterPx.toDp() }
    val selectedIndex = months.indexOfFirst { it.month == selected && it.price != null }

    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        if (ticks.isNotEmpty()) BasicText("kr", style = labelStyle)
        Box(Modifier.fillMaxWidth().height(PlotHeight)) {
            Canvas(Modifier.fillMaxSize().clearAndSetSemantics {}) {
                if (ticks.isEmpty()) return@Canvas
                val plot = Plot(size.width, size.height, gutterPx, Inset.toPx(), ticks.first(), ticks.last(), months.size)

                ticks.forEachIndexed { index, tick ->
                    val y = plot.y(tick)
                    drawLine(KvColors.textWash8, Offset(gutterPx, y), Offset(size.width, y), strokeWidth = 1.dp.toPx())
                    val text = measurer.measure(labels[index], labelStyle)
                    val top = (y - text.size.height / 2f).coerceIn(0f, size.height - text.size.height)
                    drawText(text, topLeft = Offset(gutterPx - 6.dp.toPx() - text.size.width, top))
                }

                if (selectedIndex >= 0) {
                    val x = plot.x(selectedIndex)
                    drawLine(KvColors.accent800, Offset(x, 0f), Offset(x, size.height), strokeWidth = 1.dp.toPx())
                }

                val path = Path()
                var drawing = false
                months.forEachIndexed { index, month ->
                    val price = month.price?.toDouble()
                    if (price == null) {
                        drawing = false
                    } else {
                        val at = Offset(plot.x(index), plot.y(price))
                        if (drawing) path.lineTo(at.x, at.y) else path.moveTo(at.x, at.y)
                        drawing = true
                    }
                }
                drawPath(path, KvColors.accent, style = Stroke(width = 2.dp.toPx()))

                months.forEachIndexed { index, month ->
                    val price = month.price?.toDouble() ?: return@forEachIndexed
                    val at = Offset(plot.x(index), plot.y(price))
                    if (index == selectedIndex) {
                        drawCircle(KvColors.bg, radius = 7.dp.toPx(), center = at)
                        drawCircle(KvColors.accent, radius = 5.dp.toPx(), center = at)
                    } else {
                        drawCircle(KvColors.accent, radius = 3.dp.toPx(), center = at)
                    }
                }
            }

            // One column per month, as the trend chart's bars are. A priced month is announced; a gap is not,
            // but still takes a tap and selects the nearest priced month.
            Row(Modifier.fillMaxSize().padding(start = gutter)) {
                months.forEachIndexed { index, month ->
                    val target = PriceAxis.nearestPriced(months, index)
                    val column = Modifier
                        .weight(1f)
                        .fillMaxHeight()
                    if (target == null) {
                        Box(column.clearAndSetSemantics {})
                    } else if (month.price != null) {
                        val description = "${DanishTime.month(month.month)}: ${KrFormat.amount(month.price)}"
                        Box(
                            column
                                .clickable(role = Role.Button) { onSelect(months[target].month) }
                                .semantics { contentDescription = description },
                        )
                    } else {
                        Box(column.clickable(role = Role.Button) { onSelect(months[target].month) }.clearAndSetSemantics {})
                    }
                }
            }

            if (selectedIndex >= 0 && ticks.isNotEmpty()) {
                val month = months[selectedIndex]
                Callout(month, selectedIndex, months.size, gutterPx, ticks.first(), ticks.last(), density)
            }
        }
        Row(Modifier.fillMaxWidth().padding(start = gutter)) {
            months.forEach { month ->
                BasicText(
                    DanishTime.shortMonth(month.month),
                    style = KvType.meta.copy(
                        color = if (month.month == selected) KvColors.accent else KvColors.neutral500,
                        textAlign = TextAlign.Center,
                    ),
                    modifier = Modifier.weight(1f),
                )
            }
        }
    }
}

/** The selected month's price and purchase count, above its point and kept inside the chart's width. */
@Composable
private fun Callout(month: PriceMonth, index: Int, count: Int, gutter: Float, low: Double, high: Double, density: Density) {
    val inset = with(density) { Inset.toPx() }
    val margin = with(density) { 8.dp.roundToPx() }
    val purchases = pluralStringResource(R.plurals.purchase_count, month.purchaseCount, month.purchaseCount)
    val surface = RoundedCornerShape(KvRadius.md)

    Column(
        modifier = Modifier
            .layout { measurable, constraints ->
                val placeable = measurable.measure(constraints.copy(minWidth = 0, minHeight = 0))
                val plot = Plot(constraints.maxWidth.toFloat(), constraints.maxHeight.toFloat(), gutter, inset, low, high, count)
                val x = (plot.x(index) - placeable.width / 2f).toInt().coerceIn(0, (constraints.maxWidth - placeable.width).coerceAtLeast(0))
                val pointY = plot.y(month.price!!.toDouble())
                val above = pointY - placeable.height - margin
                // Below the point when there is no room above it.
                val y = if (above >= 0f) above else pointY + margin
                layout(constraints.maxWidth, constraints.maxHeight) {
                    placeable.place(IntOffset(x, y.toInt().coerceIn(0, (constraints.maxHeight - placeable.height).coerceAtLeast(0))))
                }
            }
            .kvElevation(KvElevation.Small, surface)
            .background(KvColors.surface, surface)
            .padding(horizontal = KvSpace.s3, vertical = KvSpace.s2)
            .clearAndSetSemantics {},
    ) {
        BasicText(
            "${DanishTime.month(month.month)} · ${KrFormat.amount(month.price)}",
            style = KvType.control.copy(color = KvColors.text),
        )
        BasicText(purchases, style = KvType.meta.copy(color = KvColors.neutral400))
    }
}
