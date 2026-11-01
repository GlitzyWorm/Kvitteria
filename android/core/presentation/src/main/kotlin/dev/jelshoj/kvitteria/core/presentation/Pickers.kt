/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.BasicText
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Immutable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvIcon
import dev.jelshoj.kvitteria.core.designsystem.KvRule
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.KvitteriaTheme
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.domain.taxonomy.Category
import dev.jelshoj.kvitteria.core.domain.taxonomy.Meal

/** One row of a picker. A [Section] is a heading and can't be picked. */
@Immutable
sealed interface PickerRow<out T> {
    data class Section(val label: String) : PickerRow<Nothing>

    data class Option<T>(val value: T, val label: String, val indented: Boolean = false) : PickerRow<T>
}

/** The choice list used wherever a field would be a dropdown. Results stay inside the calling screen. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun <T> PickerSheet(
    title: String,
    rows: List<PickerRow<T>>,
    selected: T?,
    onPick: (T) -> Unit,
    onDismiss: () -> Unit,
    modifier: Modifier = Modifier,
) {
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true),
        containerColor = KvColors.bg,
        modifier = modifier,
    ) {
        PickerList(title, rows, selected, onPick)
    }
}

@Composable
private fun <T> PickerList(title: String, rows: List<PickerRow<T>>, selected: T?, onPick: (T) -> Unit) {
    Column(Modifier.navigationBarsPadding()) {
        KvHeading(title, Modifier.padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3))
        LazyColumn(contentPadding = androidx.compose.foundation.layout.PaddingValues(bottom = KvSpace.s6)) {
            items(rows) { row ->
                when (row) {
                    is PickerRow.Section -> {
                        BasicText(
                            row.label.uppercase(),
                            style = KvType.eyebrow,
                            modifier = Modifier
                                .padding(start = KvSpace.gutter, end = KvSpace.gutter, top = KvSpace.s4, bottom = KvSpace.s1)
                                .semantics { heading() },
                        )
                    }
                    is PickerRow.Option -> Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .heightIn(min = 48.dp)
                            .clickable(role = Role.RadioButton, onClick = { onPick(row.value) })
                            .padding(
                                start = if (row.indented) KvSpace.gutter + KvSpace.s6 else KvSpace.gutter,
                                end = KvSpace.gutter,
                                top = KvSpace.s2,
                                bottom = KvSpace.s2,
                            ),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(KvSpace.s3),
                    ) {
                        BasicText(row.label, style = KvType.control, modifier = Modifier.weight(1f))
                        if (row.value == selected) {
                            KvIcon(PhosphorIcons.Check, contentDescription = null, tint = KvColors.accent)
                        }
                    }
                }
            }
        }
    }
}

/** The category sheet's rows. A two-level tree needs no tree control: children sit under their parent. */
fun categoryRows(categories: List<Category>, clearLabel: String?): List<PickerRow<String?>> = buildList {
    if (clearLabel != null) add(PickerRow.Option(null, clearLabel))
    categories.sortedBy { it.sortOrder }.forEach { parent ->
        add(PickerRow.Section(parent.name))
        add(PickerRow.Option(parent.id, parent.name))
        parent.children.sortedBy { it.sortOrder }.forEach { add(PickerRow.Option(it.id, it.name, indented = true)) }
    }
}

fun mealRows(meals: List<Meal>, clearLabel: String?): List<PickerRow<String?>> = buildList {
    if (clearLabel != null) add(PickerRow.Option(null, clearLabel))
    meals.sortedBy { it.sortOrder }.forEach { add(PickerRow.Option(it.id, it.name)) }
}

/** Picks a category, with an explicit clear row. [newCategoryLabel] adds a row for creating one in place. */
@Composable
fun CategoryPickerSheet(
    categories: List<Category>,
    selected: String?,
    onPick: (String?) -> Unit,
    onDismiss: () -> Unit,
    onNewCategory: (() -> Unit)? = null,
) {
    val new = stringResource(R.string.new_category_option)
    val rows: List<PickerRow<String?>> = buildList {
        if (onNewCategory != null) add(PickerRow.Option(NEW_CATEGORY, new))
        addAll(categoryRows(categories, stringResource(R.string.none)))
    }
    PickerSheet(
        title = stringResource(R.string.category),
        rows = rows,
        selected = selected,
        onPick = { if (it == NEW_CATEGORY) onNewCategory?.invoke() else onPick(it) },
        onDismiss = onDismiss,
    )
}

private const val NEW_CATEGORY = "\u0000new"

@Composable
fun MealPickerSheet(
    meals: List<Meal>,
    selected: String?,
    onPick: (String?) -> Unit,
    onDismiss: () -> Unit,
) {
    PickerSheet(
        title = stringResource(R.string.meal),
        rows = mealRows(meals, stringResource(R.string.none)),
        selected = selected,
        onPick = onPick,
        onDismiss = onDismiss,
    )
}

private val sampleCategories = listOf(
    Category(
        "c1", "Fødevarer", null,
        listOf(Category("c1-1", "Frugt & Grønt", "c1", emptyList(), 0), Category("c1-2", "Kød & Fisk", "c1", emptyList(), 1)),
        0,
    ),
    Category("c2", "Drikkevarer", null, listOf(Category("c2-1", "Kaffe & Te", "c2", emptyList(), 0)), 1),
)

@Preview(showBackground = true, backgroundColor = 0xFF0B1914)
@Composable
private fun CategoryRowsPreview() {
    KvitteriaTheme {
        Column {
            PickerList("Kategori", categoryRows(sampleCategories, "Ingen"), "c1-2", onPick = {})
            KvRule()
        }
    }
}
