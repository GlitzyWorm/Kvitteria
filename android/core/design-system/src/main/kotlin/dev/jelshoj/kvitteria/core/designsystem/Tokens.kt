/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.designsystem

import androidx.compose.runtime.Immutable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Shadow
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.text.PlatformTextStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.LineHeightStyle
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.em
import androidx.compose.ui.unit.sp

/**
 * The palette from `docs/design-tokens.html`. It's a dark-only system: no light ramp was designed, so
 * nothing here switches on the system theme.
 */
@Immutable
object KvColors {
    val bg = Color(0xFF0B1914)
    val surface = Color(0xFF1A2521)
    val text = Color(0xFFE4E9E7)

    /** The dialog backdrop: the page ground at 70 %, dimming towards the ground it already sits on. */
    val scrim = Color(0xB30B1914)

    /** Text at 16 %. */
    val divider = Color(0x29E4E9E7)

    /** A saturated presence for deck and stat-band grounds, never an interface surface. */
    val section = Color(0xFF054328)

    val accent100 = Color(0xFFD7F3E2)
    val accent200 = Color(0xFFC4E0CE)
    val accent300 = Color(0xFF8ECFA9)
    val accent400 = Color(0xFF7CBB96)
    val accent500 = Color(0xFF68A783)
    val accent600 = Color(0xFF51906D)
    val accent700 = Color(0xFF337250)
    val accent800 = Color(0xFF054E2F)
    val accent900 = Color(0xFF022E1A)
    val accent = accent500

    val accentTint10 = Color(0x1A68A783)
    val accentTint14 = Color(0x2468A783)
    val accentTint16 = Color(0x2968A783)
    val accentTint22 = Color(0x3868A783)
    val accentTint30 = Color(0x4D68A783)
    val accentTint34 = Color(0x5768A783)

    /** The accent at zero alpha. Fading to `Transparent` would pass through black and leave a grey band. */
    val accentFade = Color(0x0068A783)

    val neutral100 = Color(0xFFEEF3F1)
    val neutral200 = Color(0xFFDCE3E0)
    val neutral300 = Color(0xFFC8D0CD)
    val neutral400 = Color(0xFFAAB4B0)
    val neutral500 = Color(0xFF8A9591)
    val neutral600 = Color(0xFF6A7470)
    val neutral700 = Color(0xFF4E5854)
    val neutral800 = Color(0xFF333D39)
    val neutral900 = Color(0xFF1E2925)

    val textMuted70 = Color(0xB3E4E9E7)
    val textMuted60 = Color(0x99E4E9E7)
    val textMuted55 = Color(0x8CE4E9E7)
    val textMuted50 = Color(0x80E4E9E7)

    val textWash8 = Color(0x14E4E9E7)
    val textWash7 = Color(0x12E4E9E7)
    val textWash6 = Color(0x0FE4E9E7)
    val textWash4 = Color(0x0AE4E9E7)
}

/** The 0.70× density scale. Fractional on purpose: these are the reference's own values. */
@Immutable
object KvSpace {
    val s1: Dp = 2.8.dp
    val s2: Dp = 5.6.dp
    val s3: Dp = 8.4.dp
    val s4: Dp = 11.2.dp
    val s6: Dp = 16.8.dp
    val s8: Dp = 22.4.dp

    /** The gutter either side of a phone screen's content. */
    val gutter: Dp = 16.dp

    val controlHeight: Dp = 36.dp
}

@Immutable
object KvRadius {
    val sm: Dp = 4.dp
    val tag: Dp = 6.dp
    val md: Dp = 8.dp
    val lg: Dp = 14.dp
}

private val heading = FontFamily(Font(R.font.montserrat_medium, FontWeight.Medium))

private val body = FontFamily(
    Font(R.font.poppins_regular, FontWeight.Normal),
    Font(R.font.poppins_medium, FontWeight.Medium),
)

/** The Phosphor face. Reached through [KvIcon], never directly. */
internal val iconFont = FontFamily(Font(R.font.phosphor_regular))

/**
 * Glyph metrics, not line height, would otherwise decide how tall a line is, and Poppins carries a lot
 * of both. Trimming keeps a stack of text at the spacing the reference draws.
 */
private val tight = LineHeightStyle(LineHeightStyle.Alignment.Center, LineHeightStyle.Trim.None)
private val noPadding = PlatformTextStyle(includeFontPadding = false)

private fun headingStyle(size: Int) = TextStyle(
    fontFamily = heading,
    fontWeight = FontWeight.Medium,
    fontSize = size.sp,
    lineHeight = 1.12.em,
    letterSpacing = (-0.015).em,
    color = KvColors.text,
    platformStyle = noPadding,
    lineHeightStyle = tight,
)

private fun bodyStyle(size: Float, color: Color = KvColors.text) = TextStyle(
    fontFamily = body,
    fontWeight = FontWeight.Normal,
    fontSize = size.sp,
    lineHeight = 1.55.em,
    color = color,
    platformStyle = noPadding,
    lineHeightStyle = tight,
)

/**
 * The type scale. Headings are Montserrat capped at weight 500, because hierarchy comes from size and
 * space. Body is Poppins. Amounts are always Montserrat with tabular figures, which Poppins doesn't have.
 */
@Immutable
object KvType {
    val h1 = headingStyle(42)
    val h2 = headingStyle(32)
    val h3 = headingStyle(25)
    val h4 = headingStyle(20)
    val h5 = headingStyle(16)
    val h6 = headingStyle(13).copy(letterSpacing = 0.08.em)

    val body = bodyStyle(15f)
    val control = bodyStyle(14f)
    val small = bodyStyle(13f)
    val label = bodyStyle(12f, KvColors.textMuted70)
    val meta = bodyStyle(11f, KvColors.textMuted50)
    val muted = bodyStyle(13f, KvColors.textMuted55)
    val hint = bodyStyle(11.5f, KvColors.neutral500)

    val eyebrow = bodyStyle(11f, KvColors.accent).copy(letterSpacing = 0.14.em)
    val kicker = bodyStyle(10f, KvColors.accent).copy(letterSpacing = 0.1.em)
    val columnHeader = bodyStyle(11f, KvColors.textMuted60).copy(letterSpacing = 0.08.em)

    val button = headingStyle(14).copy(lineHeight = 1.2.em, letterSpacing = 0.em)
    val cardTitle = headingStyle(17).copy(lineHeight = 1.2.em, letterSpacing = 0.em)
    val wordmark = headingStyle(17).copy(letterSpacing = (-0.02).em)
    val mono = TextStyle(
        fontFamily = FontFamily.Monospace,
        fontSize = 12.sp,
        lineHeight = 1.5.em,
        color = KvColors.neutral400,
        platformStyle = noPadding,
    )

    private val amountBase = headingStyle(15).copy(
        fontFeatureSettings = "tnum",
        lineHeight = 1.2.em,
        letterSpacing = 0.em,
    )

    val amountSmall = amountBase.copy(fontSize = 13.sp)
    val amount = amountBase
    val amountMedium = amountBase.copy(fontSize = 23.sp, letterSpacing = (-0.02).em)
    val amountLarge = amountBase.copy(fontSize = 27.sp, lineHeight = 1.1.em)

    /** The hero figure: set tight so a long amount reads as one block, and lit from behind. */
    val amountHero = amountBase.copy(
        fontSize = 46.sp,
        lineHeight = 0.95.em,
        letterSpacing = (-0.04).em,
        shadow = Shadow(color = KvColors.accentTint34, offset = Offset.Zero, blurRadius = 46f),
    )
}

@Immutable
enum class KvElevation { Small, Medium, Large }

/** The tokens read in one place, as `KvitteriaTheme.colors.accent`. */
object KvitteriaTheme {
    val colors = KvColors
    val space = KvSpace
    val radius = KvRadius
    val type = KvType
}
