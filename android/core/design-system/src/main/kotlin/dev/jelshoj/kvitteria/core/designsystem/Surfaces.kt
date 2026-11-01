/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.designsystem

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.dropShadow
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.graphics.shadow.Shadow
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.DpOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties

/**
 * Elevation on a dark ground is a hairline edge plus ambient darkness, never a stacked drop shadow. Each
 * level is the two halves together, and the small level is an edge alone.
 */
fun Modifier.kvElevation(level: KvElevation, shape: Shape): Modifier = when (level) {
    KvElevation.Small -> border(1.dp, KvColors.neutral800, shape)
    KvElevation.Medium -> dropShadow(shape, Shadow(18.dp, Color.Black, offset = DpOffset(0.dp, 6.dp), alpha = 0.55f))
        .border(1.dp, KvColors.neutral700, shape)
    KvElevation.Large -> dropShadow(shape, Shadow(40.dp, Color.Black, offset = DpOffset(0.dp, 16.dp), alpha = 0.65f))
        .border(1.dp, KvColors.neutral500, shape)
}

/** A surface-filled block at the large radius. */
@Composable
fun KvPanel(
    modifier: Modifier = Modifier,
    padding: Dp = KvSpace.s8,
    content: @Composable ColumnScope.() -> Unit,
) {
    val shape = RoundedCornerShape(KvRadius.lg)
    Column(
        modifier = modifier.kvElevation(KvElevation.Small, shape).background(KvColors.surface, shape).padding(padding),
        verticalArrangement = Arrangement.spacedBy(KvSpace.s4),
        content = content,
    )
}

/**
 * Sits on a surface and is filled with the page ground, which is what separates it from the panel around
 * it. [edge] is the coloured left rule the Pipeline problems use.
 */
@Composable
fun KvCard(
    modifier: Modifier = Modifier,
    elevation: KvElevation = KvElevation.Small,
    edge: Color? = null,
    padding: Dp = KvSpace.s4,
    content: @Composable ColumnScope.() -> Unit,
) {
    val shape = RoundedCornerShape(KvRadius.md)
    Column(
        modifier = modifier
            .kvElevation(elevation, shape)
            .background(KvColors.bg, shape)
            .clip(shape)
            .drawBehind {
                if (edge != null) drawRect(edge, size = size.copy(width = 2.dp.toPx()))
            }
            .padding(padding),
        verticalArrangement = Arrangement.spacedBy(KvSpace.s2),
        content = content,
    )
}

/** The faded-at-both-ends rule. The fade runs over 48dp, whatever the width. */
@Composable
fun KvRule(modifier: Modifier = Modifier, color: Color = KvColors.divider) {
    Spacer(
        modifier
            .fillMaxWidth()
            .height(1.dp)
            .drawBehind {
                val fade = (48.dp.toPx() / size.width).coerceAtMost(0.5f)
                drawRect(
                    Brush.horizontalGradient(
                        0f to color.copy(alpha = 0f),
                        fade to color,
                        1f - fade to color,
                        1f to color.copy(alpha = 0f),
                    ),
                )
            },
    )
}

/** Something the person must read before continuing: an accent edge over an accent wash. */
@Composable
fun KvCallout(text: String, modifier: Modifier = Modifier) {
    val shape = RoundedCornerShape(KvRadius.md)
    BasicText(
        text = text,
        style = KvType.small.copy(color = KvColors.accent300),
        modifier = modifier
            .fillMaxWidth()
            .background(KvColors.accentTint10, shape)
            .border(1.dp, KvColors.accentTint30, shape)
            .padding(horizontal = 12.dp, vertical = 11.dp),
    )
}

/** The muted counterpart, for a footnote that is information rather than a problem. */
@Composable
fun KvNote(text: String, modifier: Modifier = Modifier) {
    BasicText(
        text = text,
        style = KvType.label.copy(color = KvColors.neutral500),
        modifier = modifier
            .fillMaxWidth()
            .background(KvColors.textWash4, RoundedCornerShape(KvRadius.md))
            .padding(horizontal = 12.dp, vertical = 11.dp),
    )
}

/** The accent eyebrow with its short trailing line. */
@Composable
fun KvEyebrow(text: String, modifier: Modifier = Modifier) {
    Row(
        modifier = modifier,
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        BasicText(text = text.uppercase(), style = KvType.eyebrow)
        Spacer(Modifier.width(22.dp).height(1.dp).background(KvColors.accent.copy(alpha = 0.5f)))
    }
}

/** A screen or section title. */
@Composable
fun KvHeading(text: String, modifier: Modifier = Modifier, style: androidx.compose.ui.text.TextStyle = KvType.h4) {
    BasicText(text = text, style = style, modifier = modifier.semantics { heading() })
}

/** The accent-outlined square holding the receipt glyph, with the accent glow behind it. */
@Composable
fun KvBrandMark(modifier: Modifier = Modifier) {
    val shape = RoundedCornerShape(7.dp)
    Box(
        modifier = modifier
            .size(26.dp)
            .dropShadow(shape, Shadow(18.dp, KvColors.accent, alpha = 0.22f))
            .background(KvColors.bg, shape)
            .border(1.dp, KvColors.accent, shape),
        contentAlignment = Alignment.Center,
    ) {
        KvIcon(PhosphorIcons.Receipt, contentDescription = null, size = 15.dp, tint = KvColors.accent)
    }
}

@Composable
fun KvWordmark(modifier: Modifier = Modifier) {
    Row(
        modifier = modifier,
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(KvSpace.s3),
    ) {
        KvBrandMark()
        BasicText(text = "Kvitteria", style = KvType.wordmark)
    }
}

/** The circular initials of a member. */
@Composable
fun KvAvatar(
    initials: String,
    modifier: Modifier = Modifier,
    size: Dp = 34.dp,
    accented: Boolean = true,
) {
    val color = if (accented) KvColors.accent else KvColors.neutral400
    val edge = if (accented) KvColors.accent.copy(alpha = 0.4f) else KvColors.neutral800
    Box(
        modifier = modifier.size(size).border(1.dp, edge, RoundedCornerShape(50)),
        contentAlignment = Alignment.Center,
    ) {
        BasicText(
            text = initials,
            style = KvType.h5.copy(fontSize = if (size > 38.dp) KvType.h5.fontSize else KvType.h6.fontSize, color = color),
            maxLines = 1,
            overflow = TextOverflow.Clip,
        )
    }
}

/**
 * The top layer: page ground, large radius, widest fall. The title sits above a body that scrolls, and
 * the actions stay in view with the keyboard open.
 */
@Composable
fun KvDialog(
    title: String,
    onDismiss: () -> Unit,
    actions: @Composable () -> Unit,
    modifier: Modifier = Modifier,
    content: @Composable ColumnScope.() -> Unit,
) {
    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false, decorFitsSystemWindows = false),
    ) {
        val shape = RoundedCornerShape(KvRadius.lg)
        Box(
            modifier = Modifier.fillMaxWidth().imePadding().padding(KvSpace.gutter),
            contentAlignment = Alignment.Center,
        ) {
            Column(
                modifier = modifier
                    .widthIn(max = 430.dp)
                    .fillMaxWidth()
                    .kvElevation(KvElevation.Large, shape)
                    .background(KvColors.bg, shape)
                    .padding(KvSpace.s6),
                verticalArrangement = Arrangement.spacedBy(KvSpace.s3),
            ) {
                KvHeading(title, style = KvType.h4)
                Column(
                    modifier = Modifier.weight(1f, fill = false).verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(14.dp),
                    content = content,
                )
                Row(
                    modifier = Modifier.fillMaxWidth().padding(top = KvSpace.s2),
                    horizontalArrangement = Arrangement.spacedBy(KvSpace.s2, Alignment.End),
                    verticalAlignment = Alignment.CenterVertically,
                ) { actions() }
            }
        }
    }
}

/**
 * The overview card. A surface fill with an accent-tinted edge, a radial accent bloom off the top-left,
 * and a blob bleeding in from the top-right that the card clips.
 */
@Composable
fun KvHeroCard(
    modifier: Modifier = Modifier,
    content: @Composable ColumnScope.() -> Unit,
) {
    val shape = RoundedCornerShape(KvRadius.md)
    Column(
        modifier = modifier
            .dropShadow(shape, Shadow(18.dp, Color.Black, offset = DpOffset(0.dp, 6.dp), alpha = 0.55f))
            .background(KvColors.surface, shape)
            .clip(shape)
            .drawBehind {
                drawRect(
                    Brush.radialGradient(
                        0f to KvColors.accentTint16,
                        0.62f to KvColors.accentFade,
                        center = Offset.Zero,
                        radius = size.maxDimension * 1.4f,
                    ),
                )
                val radius = 140.dp.toPx()
                val center = Offset(size.width + 90.dp.toPx() - radius, radius - 110.dp.toPx())
                drawCircle(
                    Brush.radialGradient(
                        0f to KvColors.accentTint22,
                        0.7f to KvColors.accentFade,
                        center = center,
                        radius = radius,
                    ),
                    radius = radius,
                    center = center,
                )
            }
            .border(1.dp, KvColors.accentTint30, shape)
            .padding(horizontal = 18.dp, vertical = 20.dp),
        content = content,
    )
}
