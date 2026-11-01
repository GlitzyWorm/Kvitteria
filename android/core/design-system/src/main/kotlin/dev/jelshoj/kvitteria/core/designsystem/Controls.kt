/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.designsystem

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.minimumInteractiveComponentSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

enum class KvButtonStyle { Primary, Secondary, Ghost }

private const val DISABLED_ALPHA = 0.45f

/**
 * The accent is a line and a glow, never a flood, so [KvButtonStyle.Secondary] is the default and
 * [KvButtonStyle.Primary] an accent outline whose press is a tint. The reference's compact height is
 * what's drawn. The touch target around it is 48dp.
 */
@Composable
fun KvButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    style: KvButtonStyle = KvButtonStyle.Secondary,
    glyph: KvGlyph? = null,
    enabled: Boolean = true,
    busy: Boolean = false,
    fill: Boolean = false,
) {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    val shape = RoundedCornerShape(KvRadius.md)
    val tint = if (style == KvButtonStyle.Secondary) KvColors.text else KvColors.accent
    val edge = when (style) {
        KvButtonStyle.Primary -> KvColors.accent
        KvButtonStyle.Secondary -> KvColors.divider
        KvButtonStyle.Ghost -> Color.Transparent
    }
    val press = when {
        !pressed -> Color.Transparent
        style == KvButtonStyle.Secondary -> KvColors.textWash7
        else -> KvColors.accentTint22
    }

    Box(
        modifier = modifier
            .minimumInteractiveComponentSize()
            .then(if (fill) Modifier.fillMaxWidth() else Modifier)
            .heightIn(min = KvSpace.controlHeight)
            .alpha(if (enabled) 1f else DISABLED_ALPHA)
            .clip(shape)
            .background(press, shape)
            .border(1.dp, edge, shape)
            .clickable(
                enabled = enabled && !busy,
                role = Role.Button,
                interactionSource = interaction,
                indication = null,
                onClick = onClick,
            )
            .padding(horizontal = if (style == KvButtonStyle.Ghost) KvSpace.s2 else KvSpace.s3 * 1.2f, vertical = KvSpace.s2),
        contentAlignment = Alignment.Center,
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(6.dp),
        ) {
            if (busy) {
                KvSpinner(size = 15.dp)
            } else if (glyph != null) {
                KvIcon(glyph, contentDescription = null, tint = tint, size = 16.dp)
            }
            BasicText(
                text = text,
                style = KvType.button.copy(color = tint, textAlign = TextAlign.Center),
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
            )
        }
    }
}

/**
 * An icon-only control. It draws at [size] and reacts at 48dp, because the design's compact sizes stay
 * while the finger doesn't get any smaller.
 */
@Composable
fun KvIconButton(
    glyph: KvGlyph,
    contentDescription: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    size: Dp = KvSpace.controlHeight,
    tint: Color = KvColors.text,
    outlined: Boolean = false,
) {
    val shape = RoundedCornerShape(KvRadius.md)
    Box(
        modifier = modifier
            .minimumInteractiveComponentSize()
            .size(size)
            .alpha(if (enabled) 1f else DISABLED_ALPHA)
            .clip(shape)
            .then(if (outlined) Modifier.border(1.dp, KvColors.divider, shape) else Modifier)
            .clickable(enabled = enabled, role = Role.Button, onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        KvIcon(glyph, contentDescription = contentDescription, tint = tint)
    }
}

enum class KvTagStyle { Accent, Neutral, Outline }

@Composable
fun KvTag(text: String, modifier: Modifier = Modifier, style: KvTagStyle = KvTagStyle.Neutral) {
    val shape = RoundedCornerShape(KvRadius.tag)
    val color = when (style) {
        KvTagStyle.Accent -> KvColors.accent100
        KvTagStyle.Neutral -> KvColors.neutral100
        KvTagStyle.Outline -> KvColors.accent
    }
    val fill = when (style) {
        KvTagStyle.Accent -> Modifier.background(KvColors.accent800, shape)
        KvTagStyle.Neutral -> Modifier.background(KvColors.neutral800, shape)
        KvTagStyle.Outline -> Modifier.border(1.dp, KvColors.accent, shape)
    }
    BasicText(
        text = text,
        style = KvType.meta.copy(color = color),
        maxLines = 1,
        overflow = TextOverflow.Ellipsis,
        modifier = modifier.then(fill).padding(horizontal = 10.dp, vertical = 3.dp),
    )
}

/** The text field with its label above and any error or hint below. */
@Composable
fun KvField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit,
    modifier: Modifier = Modifier,
    placeholder: String? = null,
    hint: String? = null,
    error: String? = null,
    enabled: Boolean = true,
    singleLine: Boolean = true,
    textStyle: TextStyle = KvType.control,
    keyboardOptions: KeyboardOptions = KeyboardOptions.Default,
    keyboardActions: KeyboardActions = KeyboardActions.Default,
    visualTransformation: VisualTransformation = VisualTransformation.None,
) {
    val interaction = remember { MutableInteractionSource() }
    val focused by interaction.collectIsFocusedAsState()
    val shape = RoundedCornerShape(KvRadius.md)
    val edge = when {
        error != null -> KvColors.accent300
        focused -> KvColors.accent
        else -> KvColors.divider
    }

    Column(modifier = modifier, verticalArrangement = Arrangement.spacedBy(5.dp)) {
        BasicText(text = label, style = KvType.label)
        BasicTextField(
            value = value,
            onValueChange = onValueChange,
            enabled = enabled,
            singleLine = singleLine,
            textStyle = textStyle.copy(color = if (enabled) KvColors.text else KvColors.textMuted55),
            cursorBrush = SolidColor(KvColors.accent),
            keyboardOptions = keyboardOptions,
            keyboardActions = keyboardActions,
            visualTransformation = visualTransformation,
            interactionSource = interaction,
            modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp),
            decorationBox = { inner ->
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = 48.dp)
                        .background(KvColors.bg, shape)
                        .border(1.dp, edge, shape)
                        .padding(horizontal = 10.dp, vertical = 6.dp),
                    contentAlignment = Alignment.CenterStart,
                ) {
                    if (value.isEmpty() && placeholder != null) {
                        BasicText(text = placeholder, style = textStyle.copy(color = KvColors.textMuted50))
                    }
                    inner()
                }
            },
        )
        when {
            error != null -> BasicText(text = error, style = KvType.hint.copy(color = KvColors.accent300))
            hint != null -> BasicText(text = hint, style = KvType.hint)
        }
    }
}

/** A field that opens something rather than taking text, such as a picker sheet. */
@Composable
fun KvSelectField(
    label: String,
    value: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    hint: String? = null,
) {
    val shape = RoundedCornerShape(KvRadius.md)
    Column(modifier = modifier, verticalArrangement = Arrangement.spacedBy(5.dp)) {
        BasicText(text = label, style = KvType.label)
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 48.dp)
                .alpha(if (enabled) 1f else DISABLED_ALPHA)
                .clip(shape)
                .background(KvColors.bg, shape)
                .border(1.dp, KvColors.divider, shape)
                .clickable(enabled = enabled, role = Role.DropdownList, onClick = onClick)
                .padding(horizontal = 10.dp, vertical = 6.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(KvSpace.s2),
        ) {
            BasicText(
                text = value,
                style = KvType.control,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.weight(1f),
            )
            KvIcon(PhosphorIcons.CaretDown, contentDescription = null, size = 15.dp)
        }
        if (hint != null) BasicText(text = hint, style = KvType.hint)
    }
}

/** The inline choice row. The selection is an inset accent outline, never a fill. */
@Composable
fun KvSegmented(
    options: List<String>,
    selected: Int,
    onSelect: (Int) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
) {
    val shape = RoundedCornerShape(KvRadius.md)
    Row(
        modifier = modifier
            .height(IntrinsicSize.Min)
            .clip(shape)
            .border(1.dp, KvColors.divider, shape)
            .selectableGroup(),
    ) {
        options.forEachIndexed { index, option ->
            if (index > 0) Box(Modifier.width(1.dp).fillMaxHeight().background(KvColors.divider))
            val isSelected = index == selected
            Box(
                modifier = Modifier
                    .weight(1f)
                    .heightIn(min = 48.dp)
                    .then(if (isSelected) Modifier.border(1.dp, KvColors.accent, shape) else Modifier)
                    .selectable(
                        selected = isSelected,
                        enabled = enabled,
                        role = Role.RadioButton,
                        onClick = { onSelect(index) },
                    )
                    .padding(horizontal = 8.dp, vertical = 7.dp),
                contentAlignment = Alignment.Center,
            ) {
                BasicText(
                    text = option,
                    style = KvType.small.copy(
                        color = if (isSelected) KvColors.accent else KvColors.textMuted70,
                        textAlign = TextAlign.Center,
                    ),
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
    }
}

/** The same choice row for more options than a phone's width holds, which scrolls sideways instead. */
@Composable
fun KvScrollTabs(
    options: List<String>,
    selected: Int,
    onSelect: (Int) -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(
        modifier = modifier.horizontalScroll(rememberScrollState()).selectableGroup(),
        horizontalArrangement = Arrangement.spacedBy(KvSpace.s2),
    ) {
        options.forEachIndexed { index, option ->
            val isSelected = index == selected
            val shape = RoundedCornerShape(KvRadius.md)
            Box(
                modifier = Modifier
                    .heightIn(min = 48.dp)
                    .padding(vertical = 6.dp)
                    .clip(shape)
                    .border(1.dp, if (isSelected) KvColors.accent else KvColors.divider, shape)
                    .selectable(selected = isSelected, role = Role.Tab, onClick = { onSelect(index) })
                    .padding(horizontal = 12.dp, vertical = 7.dp),
                contentAlignment = Alignment.Center,
            ) {
                BasicText(
                    text = option,
                    style = KvType.small.copy(color = if (isSelected) KvColors.accent else KvColors.textMuted70),
                    maxLines = 1,
                )
            }
        }
    }
}

/** A labelled checkbox. The whole row is the target. */
@Composable
fun KvCheckbox(
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    content: @Composable RowScope.() -> Unit,
) {
    val shape = RoundedCornerShape(KvRadius.sm)
    Row(
        modifier = modifier
            .fillMaxWidth()
            .heightIn(min = 48.dp)
            .alpha(if (enabled) 1f else DISABLED_ALPHA)
            .toggleable(value = checked, enabled = enabled, role = Role.Checkbox, onValueChange = onCheckedChange)
            .padding(vertical = KvSpace.s2),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(9.dp),
    ) {
        Box(
            modifier = Modifier
                .size(18.dp)
                .border(1.5.dp, if (checked) KvColors.accent else KvColors.neutral600, shape),
            contentAlignment = Alignment.Center,
        ) {
            if (checked) KvIcon(PhosphorIcons.Check, contentDescription = null, size = 13.dp, tint = KvColors.accent)
        }
        content()
    }
}

/** The app's activity indicator, in the accent. */
@Composable
fun KvSpinner(modifier: Modifier = Modifier, size: Dp = 22.dp) {
    CircularProgressIndicator(
        modifier = modifier.size(size),
        color = KvColors.accent,
        trackColor = KvColors.accentFade,
        strokeWidth = 2.dp,
    )
}
