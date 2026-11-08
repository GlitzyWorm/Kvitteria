/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria

import android.graphics.Color
import android.os.Build
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import androidx.core.content.IntentCompat
import androidx.lifecycle.lifecycleScope
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.feature.capture.domain.ImagePreparer
import dev.jelshoj.kvitteria.feature.capture.domain.PendingCapture
import dev.jelshoj.kvitteria.feature.capture.presentation.CaptureNotices
import dev.jelshoj.kvitteria.feature.capture.presentation.toUiText
import kotlinx.coroutines.launch
import org.koin.android.ext.android.inject
import androidx.activity.SystemBarStyle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import dev.jelshoj.kvitteria.core.designsystem.KvitteriaTheme
import dev.jelshoj.kvitteria.navigation.KvitteriaRoot

/** An `AppCompatActivity`, because `setApplicationLocales` only reaches one of those. */
class MainActivity : AppCompatActivity() {
    private val preparer: ImagePreparer by inject()
    private val pending: PendingCapture by inject()
    private val notices: CaptureNotices by inject()

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        // Dark bars on purpose, rather than the default that follows the system's light mode.
        enableEdgeToEdge(
            statusBarStyle = SystemBarStyle.dark(Color.TRANSPARENT),
            navigationBarStyle = SystemBarStyle.dark(Color.TRANSPARENT),
        )
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) window.isNavigationBarContrastEnforced = false
        // Only a fresh start: a recreated activity was already handed this intent.
        if (savedInstanceState == null) receiveShare(intent)
        setContent {
            KvitteriaTheme { KvitteriaRoot() }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        receiveShare(intent)
    }

    /**
     * An image shared from another app. It is read at once, because the read grant lasts only as long as
     * this activity, and waits in [PendingCapture] until the shell opens its confirmation, after sign-in if
     * need be.
     */
    private fun receiveShare(intent: Intent?) {
        if (intent?.action != Intent.ACTION_SEND || intent.type?.startsWith("image/") != true) return
        val uri = IntentCompat.getParcelableExtra(intent, Intent.EXTRA_STREAM, Uri::class.java) ?: return
        lifecycleScope.launch {
            when (val result = preparer.prepare(uri.toString())) {
                is Result.Success -> pending.set(result.data, shared = true)
                is Result.Error -> notices.post(result.error.toUiText())
            }
        }
    }
}
