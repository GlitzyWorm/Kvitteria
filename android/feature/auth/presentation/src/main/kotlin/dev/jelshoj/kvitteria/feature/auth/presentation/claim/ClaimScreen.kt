/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.presentation.claim

import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.KvitteriaTheme
import dev.jelshoj.kvitteria.feature.auth.presentation.AuthScaffold
import dev.jelshoj.kvitteria.feature.auth.presentation.R

/**
 * Calls nothing, because there's no claim endpoint. It exists so the person about to sign up learns
 * beforehand what signing up will make them.
 */
@Composable
fun ClaimScreen(
    onContinue: () -> Unit,
    onChangeServer: () -> Unit,
) {
    AuthScaffold(
        kicker = stringResource(R.string.claim_kicker),
        title = stringResource(R.string.claim_title),
        blurb = stringResource(R.string.claim_blurb),
    ) {
        BasicText(stringResource(R.string.claim_detail), style = KvType.small.copy(color = KvColors.neutral400))
        KvButton(
            text = stringResource(R.string.claim_continue),
            onClick = onContinue,
            style = KvButtonStyle.Primary,
            fill = true,
        )
        KvButton(text = stringResource(R.string.change_server), onClick = onChangeServer, fill = true)
    }
}

@Preview
@Composable
private fun ClaimScreenPreview() {
    KvitteriaTheme { ClaimScreen(onContinue = {}, onChangeServer = {}) }
}
