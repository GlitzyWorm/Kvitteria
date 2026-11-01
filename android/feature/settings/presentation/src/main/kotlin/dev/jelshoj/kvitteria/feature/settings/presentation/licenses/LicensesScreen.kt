/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.presentation.licenses

import android.content.Context
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mikepenz.aboutlibraries.Libs
import com.mikepenz.aboutlibraries.util.withContext
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvEyebrow
import dev.jelshoj.kvitteria.core.designsystem.KvIcon
import dev.jelshoj.kvitteria.core.designsystem.KvRule
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.feature.settings.presentation.R
import dev.jelshoj.kvitteria.feature.settings.presentation.SettingsTopBar

/** One notice the app ships. Bundled so it can be read in full offline. */
private data class Notice(val id: String, val title: String, val subtitle: String?, val text: String)

private fun Context.asset(name: String) = assets.open("licenses/$name").bufferedReader().use { it.readText() }

/**
 * The plugin's generated list covers every dependency without a manual edit. The notices it can't know
 * about, the app's own MPL and the fonts, sit in assets.
 */
private fun libraryNotices(context: Context): List<Notice> =
    Libs.Builder().withContext(context).build().libraries.sortedBy { it.name.lowercase() }.map { library ->
        val licenses = library.licenses.toList()
        Notice(
            id = library.uniqueId,
            title = library.name,
            subtitle = listOfNotNull(library.artifactVersion, licenses.joinToString { it.name }.ifEmpty { null })
                .joinToString(" · "),
            text = licenses.joinToString("\n\n") { license ->
                license.licenseContent?.takeIf { it.isNotBlank() } ?: listOfNotNull(license.name, license.url).joinToString("\n")
            },
        )
    }

@Composable
fun LicensesRoot(onBack: () -> Unit) {
    val context = LocalContext.current
    val app = stringResource(R.string.licenses_app_name)
    val own = remember { listOf(Notice("app", app, null, context.asset("MPL-2.0.txt"))) }
    val fonts = remember {
        listOf(
            Notice("montserrat", "Montserrat", "SIL OFL 1.1", context.asset("OFL-Montserrat.txt")),
            Notice("poppins", "Poppins", "SIL OFL 1.1", context.asset("OFL-Poppins.txt")),
            Notice("phosphor", "Phosphor Icons", "MIT", context.asset("MIT-Phosphor.txt")),
        )
    }
    val libraries = remember { libraryNotices(context) }
    var open by rememberSaveable { mutableStateOf<String?>(null) }
    val opened = (own + fonts + libraries).firstOrNull { it.id == open }

    Column(Modifier.fillMaxSize().background(KvColors.bg).safeDrawingPadding()) {
        SettingsTopBar(opened?.title ?: stringResource(R.string.licenses_title)) {
            if (opened != null) open = null else onBack()
        }
        if (opened != null) {
            androidx.activity.compose.BackHandler { open = null }
            BasicText(
                opened.text,
                style = KvType.mono.copy(fontSize = 11.sp),
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(KvSpace.gutter),
            )
        } else {
            LazyColumn(contentPadding = PaddingValues(bottom = KvSpace.s8)) {
                item { Section(stringResource(R.string.licenses_app)) }
                items(own, key = { it.id }) { notice -> Row(notice) { open = notice.id } }
                item { Section(stringResource(R.string.licenses_fonts)) }
                items(fonts, key = { it.id }) { notice -> Row(notice) { open = notice.id } }
                item { Section(stringResource(R.string.licenses_libraries)) }
                items(libraries, key = { it.id }) { notice -> Row(notice) { open = notice.id } }
            }
        }
    }
}

@Composable
private fun Section(title: String) {
    KvEyebrow(title, Modifier.padding(horizontal = KvSpace.gutter, vertical = KvSpace.s4))
}

@Composable
private fun Row(notice: Notice, onOpen: () -> Unit) {
    Column {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 48.dp)
                .clickable(role = Role.Button, onClick = onOpen)
                .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s2),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(KvSpace.s3),
        ) {
            Column(Modifier.weight(1f)) {
                BasicText(notice.title, style = KvType.control)
                if (!notice.subtitle.isNullOrEmpty()) BasicText(notice.subtitle, style = KvType.hint)
            }
            KvIcon(PhosphorIcons.CaretRight, contentDescription = null)
        }
        KvRule()
    }
}
