/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.presentation

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvGlyph
import dev.jelshoj.kvitteria.core.designsystem.KvHeading
import dev.jelshoj.kvitteria.core.designsystem.KvIcon
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvSpinner
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons

@Composable
fun LoadingState(modifier: Modifier = Modifier) {
    Box(modifier.fillMaxSize(), contentAlignment = Alignment.Center) { KvSpinner() }
}

/** Stays scrollable, so pull-to-refresh still works on a screen with nothing on it. */
@Composable
fun MessageState(
    glyph: KvGlyph,
    title: String,
    modifier: Modifier = Modifier,
    body: String? = null,
    actionLabel: String? = null,
    onAction: (() -> Unit)? = null,
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = KvSpace.s8, vertical = 64.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(KvSpace.s3, Alignment.CenterVertically),
    ) {
        KvIcon(glyph, contentDescription = null, size = 34.dp, tint = KvColors.accent)
        KvHeading(title, style = KvType.h5.copy(textAlign = TextAlign.Center))
        if (body != null) {
            BasicText(body, style = KvType.small.copy(color = KvColors.neutral400, textAlign = TextAlign.Center))
        }
        if (actionLabel != null && onAction != null) {
            KvButton(actionLabel, onAction, style = KvButtonStyle.Primary)
        }
    }
}

@Composable
fun ErrorState(message: String, onRetry: () -> Unit, modifier: Modifier = Modifier) {
    MessageState(
        glyph = PhosphorIcons.WarningCircle,
        title = message,
        actionLabel = stringResource(R.string.retry),
        onAction = onRetry,
        modifier = modifier,
    )
}

/**
 * Draws whichever of the states a [Loadable] is in, inside pull-to-refresh. [isEmpty] decides whether
 * loaded data counts as nothing, so a new household sees an empty state rather than a screen of zeros.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun <T> LoadableContent(
    state: Loadable<T>,
    onRefresh: () -> Unit,
    modifier: Modifier = Modifier,
    isEmpty: (T) -> Boolean = { false },
    empty: @Composable () -> Unit = {},
    content: @Composable (T) -> Unit,
) {
    val data = state.data
    PullToRefreshBox(
        isRefreshing = state.isRefreshing,
        onRefresh = onRefresh,
        modifier = modifier.fillMaxSize(),
    ) {
        when {
            data == null && state.error != null -> ErrorState(state.error.asString(), onRefresh)
            data == null -> LoadingState()
            isEmpty(data) -> empty()
            else -> content(data)
        }
    }
}
