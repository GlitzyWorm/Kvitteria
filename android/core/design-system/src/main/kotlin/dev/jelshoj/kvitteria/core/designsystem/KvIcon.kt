/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.designsystem

import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Immutable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

/** A glyph in the bundled Phosphor face. */
@JvmInline
@Immutable
value class KvGlyph(val codePoint: Char)

/**
 * Code points from Phosphor's own `src/regular/style.css`, and the name after the dash there is the name
 * here. A private-use character is unreadable in place, so nothing else spells one out.
 */
object PhosphorIcons {
    val ArrowClockwise = KvGlyph('')
    val ArrowLeft = KvGlyph('')
    val ArrowRight = KvGlyph('')
    val CaretDown = KvGlyph('')
    val CaretLeft = KvGlyph('')
    val CaretRight = KvGlyph('')
    val ChartPieSlice = KvGlyph('')
    val Check = KvGlyph('')
    val CheckCircle = KvGlyph('')
    val Copy = KvGlyph('')
    val Confetti = KvGlyph('')
    val EnvelopeSimple = KvGlyph('')
    val FlowArrow = KvGlyph('')
    val Gear = KvGlyph('')
    val GraduationCap = KvGlyph('')
    val Image = KvGlyph('')
    val Info = KvGlyph('')
    val Lightning = KvGlyph('')
    val LockSimple = KvGlyph('')
    val MagnifyingGlass = KvGlyph('')
    val Minus = KvGlyph('')
    val PencilSimple = KvGlyph('')
    val Plus = KvGlyph('')
    val Receipt = KvGlyph('')
    val Scan = KvGlyph('')
    val SignOut = KvGlyph('')
    val SlidersHorizontal = KvGlyph('')
    val Sparkle = KvGlyph('')
    val Translate = KvGlyph('')
    val Trash = KvGlyph('')
    val TrendUp = KvGlyph('')
    val UserPlus = KvGlyph('')
    val Users = KvGlyph('')
    val Warning = KvGlyph('')
    val WarningCircle = KvGlyph('')
    val X = KvGlyph('')

    /** Every glyph, for the gallery that proves each one renders. */
    val all = listOf(
        ArrowClockwise, ArrowLeft, ArrowRight, CaretDown, CaretLeft, CaretRight, ChartPieSlice, Check, CheckCircle,
        Confetti, Copy, EnvelopeSimple, FlowArrow, Gear, GraduationCap, Image, Info, Lightning, LockSimple, MagnifyingGlass, Minus,
        PencilSimple, Plus, Receipt, Scan, SignOut, SlidersHorizontal, Sparkle, Translate, Trash, TrendUp, UserPlus, Users,
        Warning, WarningCircle, X,
    )
}

/**
 * Draws a glyph at a fixed size that the font-scale setting leaves alone, because icons don't get harder
 * to read. Pass null as [contentDescription] for a decorative glyph, which hides it from screen readers.
 */
@Composable
fun KvIcon(
    glyph: KvGlyph,
    contentDescription: String?,
    modifier: Modifier = Modifier,
    size: Dp = 17.dp,
    tint: Color = KvColors.neutral400,
) {
    val fontSize = with(LocalDensity.current) { size.toSp() }
    BasicText(
        text = glyph.codePoint.toString(),
        style = TextStyle(
            fontFamily = iconFont,
            fontSize = fontSize,
            lineHeight = fontSize,
            color = tint,
            textAlign = TextAlign.Center,
        ),
        softWrap = false,
        modifier = modifier.clearAndSetSemantics {
            if (contentDescription != null) this.contentDescription = contentDescription
        },
    )
}
