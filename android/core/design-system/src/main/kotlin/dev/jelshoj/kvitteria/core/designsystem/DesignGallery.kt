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
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp

private val swatches = listOf(
    "bg" to KvColors.bg, "surface" to KvColors.surface, "text" to KvColors.text, "section" to KvColors.section,
    "accent100" to KvColors.accent100, "accent200" to KvColors.accent200, "accent300" to KvColors.accent300,
    "accent400" to KvColors.accent400, "accent500" to KvColors.accent500, "accent600" to KvColors.accent600,
    "accent700" to KvColors.accent700, "accent800" to KvColors.accent800, "accent900" to KvColors.accent900,
    "neutral100" to KvColors.neutral100, "neutral200" to KvColors.neutral200, "neutral300" to KvColors.neutral300,
    "neutral400" to KvColors.neutral400, "neutral500" to KvColors.neutral500, "neutral600" to KvColors.neutral600,
    "neutral700" to KvColors.neutral700, "neutral800" to KvColors.neutral800, "neutral900" to KvColors.neutral900,
)

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun Swatches() {
    FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        swatches.forEach { (name, color) ->
            Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Box(Modifier.size(52.dp).background(color, RoundedCornerShape(KvRadius.sm)).kvElevation(KvElevation.Small, RoundedCornerShape(KvRadius.sm)))
                BasicText(name, style = KvType.meta.copy(color = KvColors.neutral500))
            }
        }
    }
}

@Composable
private fun TypeScale() {
    Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s3)) {
        BasicText("H1 42", style = KvType.h1)
        BasicText("H2 32", style = KvType.h2)
        BasicText("H3 25", style = KvType.h3)
        BasicText("H4 20", style = KvType.h4)
        BasicText("H5 16", style = KvType.h5)
        BasicText("H6 13", style = KvType.h6)
        BasicText("Brødtekst Poppins 15", style = KvType.body)
        BasicText("Lille tekst 13", style = KvType.small)
        BasicText("Dæmpet 13", style = KvType.muted)
        KvEyebrow("Eyebrow")
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun Icons() {
    FlowRow(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        PhosphorIcons.all.forEach { KvIcon(it, contentDescription = null, size = 22.dp, tint = KvColors.text) }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun Components() {
    Column(verticalArrangement = Arrangement.spacedBy(KvSpace.s4)) {
        FlowRow(horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            KvButton("Primær", {}, style = KvButtonStyle.Primary, glyph = PhosphorIcons.Check)
            KvButton("Sekundær", {})
            KvButton("Spøgelse", {}, style = KvButtonStyle.Ghost)
            KvButton("Slået fra", {}, enabled = false)
        }
        Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            KvIconButton(PhosphorIcons.Minus, "minus", {}, size = 30.dp, outlined = true)
            KvIconButton(PhosphorIcons.Plus, "plus", {}, size = 30.dp, outlined = true)
            KvIconButton(PhosphorIcons.Gear, "indstillinger", {})
        }
        Row(horizontalArrangement = Arrangement.spacedBy(KvSpace.s2)) {
            KvTag("Accent", style = KvTagStyle.Accent)
            KvTag("Neutral", style = KvTagStyle.Neutral)
            KvTag("Outline", style = KvTagStyle.Outline)
        }
        KvField("E-mail", "you@household.dk", {})
        KvField("Port", "abc", {}, error = "Skal være et tal")
        KvSegmented(listOf("Alle", "Uafstemte"), 0, {}, Modifier.fillMaxWidth())
        KvCheckbox(true, {}) { BasicText("Husk til fremtidige kvitteringer", style = KvType.small) }
        KvCard {
            BasicText("KORT".uppercase(), style = KvType.kicker)
            BasicText("Korttitel", style = KvType.cardTitle)
            KvRule()
            BasicText("Metadata", style = KvType.meta)
        }
        KvPanel { BasicText("Panel", style = KvType.body) }
        KvCallout("Noget der skal læses først")
        KvNote("En stille fodnote")
        KvCard(elevation = KvElevation.Large) { BasicText("Dialogens elevation", style = KvType.small) }
    }
}

@Composable
private fun Hero() {
    KvHeroCard(Modifier.fillMaxWidth()) {
        KvEyebrow("August 2026")
        Spacer(Modifier.height(10.dp))
        KvAmount("2.438,27 kr", style = KvType.amountHero, color = KvColors.text)
        Spacer(Modifier.height(12.dp))
        KvTag("+4,2 %", style = KvTagStyle.Accent)
    }
}

@Composable
private fun AmountColumn() {
    Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(2.dp)) {
        listOf("1.111,11 kr", "888,88 kr", "12,50 kr", "−7,10 kr", "2.000,00 kr").forEach {
            KvAmount(it, Modifier.fillMaxWidth())
        }
    }
}

/** Everything in the design system on one page. A debug activity shows it on a device. */
@Composable
fun DesignGallery(modifier: Modifier = Modifier) {
    KvitteriaTheme {
        Column(modifier.fillMaxSize().background(KvColors.bg).safeDrawingPadding()) {
            Column(
                modifier = Modifier
                    .weight(1f)
                    .verticalScroll(rememberScrollState())
                    .padding(KvSpace.gutter),
                verticalArrangement = Arrangement.spacedBy(KvSpace.s8),
            ) {
                KvWordmark()
                Hero()
                AmountColumn()
                Swatches()
                TypeScale()
                Icons()
                Components()
            }
            KvTabBar(
                tabs = listOf(
                    KvTab("Oversigt", PhosphorIcons.ChartPieSlice),
                    KvTab("Sortér", PhosphorIcons.Lightning, badge = 12),
                    KvTab("Kvitteringer", PhosphorIcons.Receipt),
                    KvTab("Indsigt", PhosphorIcons.TrendUp),
                    KvTab("Pipeline", PhosphorIcons.FlowArrow, badge = 2),
                ),
                selected = 1,
                onSelect = {},
            )
        }
    }
}

@Preview(backgroundColor = 0xFF0B1914, showBackground = true, heightDp = 2400)
@Composable
private fun DesignGalleryPreview() = DesignGallery(Modifier.background(Color(0xFF0B1914)))
