/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.auth.presentation.gate

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.text.BasicText
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvSpinner
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.KvWordmark
import dev.jelshoj.kvitteria.core.designsystem.KvitteriaTheme
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import dev.jelshoj.kvitteria.feature.auth.presentation.AuthScaffold
import dev.jelshoj.kvitteria.feature.auth.presentation.R
import org.koin.compose.viewmodel.koinViewModel

@Composable
fun GateRoot(
    onServer: () -> Unit,
    onClaim: () -> Unit,
    onSignIn: () -> Unit,
    onMain: () -> Unit,
    onRejected: () -> Unit,
    viewModel: GateViewModel = koinViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()

    ObserveAsEvents(viewModel.events) { event ->
        when (event) {
            GateEvent.GoToServer -> onServer()
            GateEvent.GoToClaim -> onClaim()
            GateEvent.GoToSignIn -> onSignIn()
            GateEvent.GoToMain -> onMain()
            GateEvent.GoToRejected -> onRejected()
        }
    }

    GateScreen(state = state, onAction = viewModel::onAction)
}

@Composable
fun GateScreen(
    state: GateState,
    onAction: (GateAction) -> Unit,
) {
    val address = state.unreachableAddress
    if (address == null) {
        Column(
            modifier = Modifier.fillMaxSize().background(KvColors.bg),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(KvSpace.s8, Alignment.CenterVertically),
        ) {
            KvWordmark()
            KvSpinner()
        }
    } else {
        AuthScaffold(
            kicker = stringResource(R.string.gate_unreachable_kicker),
            title = stringResource(R.string.gate_unreachable_title),
            blurb = stringResource(R.string.gate_unreachable_blurb),
        ) {
            BasicText(address, style = KvType.mono)
            KvButton(
                text = stringResource(CoreR.string.retry),
                onClick = { onAction(GateAction.OnRetry) },
                style = KvButtonStyle.Primary,
                glyph = PhosphorIcons.ArrowClockwise,
                fill = true,
            )
            KvButton(
                text = stringResource(R.string.change_server),
                onClick = { onAction(GateAction.OnChangeServer) },
                glyph = PhosphorIcons.SlidersHorizontal,
                fill = true,
            )
        }
    }
}

@Preview
@Composable
private fun GateUnreachablePreview() {
    KvitteriaTheme {
        GateScreen(GateState(unreachableAddress = "http://10.0.2.2:8080"), onAction = {})
    }
}
