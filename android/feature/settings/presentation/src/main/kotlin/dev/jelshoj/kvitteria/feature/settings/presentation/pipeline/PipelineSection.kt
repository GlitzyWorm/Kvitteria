/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.presentation.pipeline

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicText
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.designsystem.KvButton
import dev.jelshoj.kvitteria.core.designsystem.KvButtonStyle
import dev.jelshoj.kvitteria.core.designsystem.KvCheckbox
import dev.jelshoj.kvitteria.core.designsystem.KvColors
import dev.jelshoj.kvitteria.core.designsystem.KvEyebrow
import dev.jelshoj.kvitteria.core.designsystem.KvField
import dev.jelshoj.kvitteria.core.designsystem.KvNote
import dev.jelshoj.kvitteria.core.designsystem.KvRule
import dev.jelshoj.kvitteria.core.designsystem.KvSelectField
import dev.jelshoj.kvitteria.core.designsystem.KvSpace
import dev.jelshoj.kvitteria.core.designsystem.KvToastBox
import dev.jelshoj.kvitteria.core.designsystem.KvType
import dev.jelshoj.kvitteria.core.designsystem.PhosphorIcons
import dev.jelshoj.kvitteria.core.designsystem.rememberKvToastState
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.presentation.Loadable
import dev.jelshoj.kvitteria.core.presentation.LoadableContent
import dev.jelshoj.kvitteria.core.presentation.Loader
import dev.jelshoj.kvitteria.core.presentation.ObserveAsEvents
import dev.jelshoj.kvitteria.core.presentation.OnStart
import dev.jelshoj.kvitteria.core.presentation.PickerRow
import dev.jelshoj.kvitteria.core.presentation.PickerSheet
import dev.jelshoj.kvitteria.core.presentation.R as CoreR
import dev.jelshoj.kvitteria.core.presentation.UiText
import dev.jelshoj.kvitteria.core.presentation.refreshIfForbidden
import dev.jelshoj.kvitteria.core.presentation.toUiText
import dev.jelshoj.kvitteria.feature.settings.domain.PipelineSettings
import dev.jelshoj.kvitteria.feature.settings.domain.PipelineSettingsDraft
import dev.jelshoj.kvitteria.feature.settings.domain.PipelineSettingsDraftValidator
import dev.jelshoj.kvitteria.feature.settings.domain.PipelineSettingsError
import dev.jelshoj.kvitteria.feature.settings.domain.PipelineSettingsFieldErrors
import dev.jelshoj.kvitteria.feature.settings.domain.PipelineSettingsRepository
import dev.jelshoj.kvitteria.feature.settings.domain.toDraft
import dev.jelshoj.kvitteria.feature.settings.presentation.R
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.koin.compose.viewmodel.koinViewModel

data class PipelineFormState(
    val settings: Loadable<PipelineSettings> = Loadable(),
    val draft: PipelineSettingsDraft = PipelineSettingsDraft(),
    val models: List<String>? = null,
    val pickingModel: Boolean = false,
    val isSaving: Boolean = false,
    val canEdit: Boolean = false,
) {
    val saved: PipelineSettingsDraft? get() = settings.data?.toDraft()
    val isDirty: Boolean get() = saved != null && saved != draft
    val errors: PipelineSettingsFieldErrors get() = PipelineSettingsDraftValidator.validate(draft)
}

class PipelineSettingsViewModel(
    private val repository: PipelineSettingsRepository,
    private val members: MemberRepository,
    sessions: SessionManager,
) : ViewModel() {
    private val loader = Loader(viewModelScope) { repository.settings() }
    private val form = MutableStateFlow(PipelineFormState())

    val state = combine(loader.state, form, sessions.member) { settings, current, member ->
        current.copy(settings = settings, canEdit = member?.can(Permission.ManageSettings) == true)
    }.stateIn(viewModelScope, SharingStarted.Eagerly, PipelineFormState())

    private val notices = Channel<UiText>(Channel.BUFFERED)
    val messages = merge(loader.failures, notices.receiveAsFlow())

    init {
        viewModelScope.launch {
            // A reload must not throw away what someone is typing.
            loader.state.collect { loaded ->
                val data = loaded.data ?: return@collect
                form.update { if (it.draft == PipelineSettingsDraft()) it.copy(draft = data.toDraft()) else it }
            }
        }
    }

    fun refresh() {
        loader.refresh()
        viewModelScope.launch {
            // No list is no reason to stop: the current model is still shown and the rest can be saved.
            val models = (repository.availableModels() as? Result.Success)?.data
            form.update { it.copy(models = models) }
        }
    }

    fun change(draft: PipelineSettingsDraft) = form.update { it.copy(draft = draft) }

    fun pickModel(picking: Boolean) = form.update { it.copy(pickingModel = picking) }

    fun discard() {
        loader.state.value.data?.let { saved -> form.update { it.copy(draft = saved.toDraft()) } }
    }

    fun save() {
        val current = state.value
        if (current.isSaving || !current.isDirty || current.errors.hasAny) return

        form.update { it.copy(isSaving = true) }
        viewModelScope.launch {
            when (val result = repository.save(PipelineSettingsDraftValidator.parse(current.draft))) {
                is Result.Error -> {
                    form.update { it.copy(isSaving = false) }
                    notices.send(result.error.toUiText())
                    members.refreshIfForbidden(result.error)
                }
                is Result.Success -> {
                    loader.update { result.data }
                    form.update { it.copy(isSaving = false, draft = result.data.toDraft()) }
                    notices.send(UiText.StringResource(R.string.settings_saved))
                }
            }
        }
    }
}

@StringRes
private fun PipelineSettingsError.messageRes(): Int = when (this) {
    PipelineSettingsError.POLL_INTERVAL -> R.string.pipeline_error_poll_interval
    PipelineSettingsError.MESSAGES_PER_POLL -> R.string.pipeline_error_messages_per_poll
    PipelineSettingsError.NUM_CTX -> R.string.pipeline_error_num_ctx
    PipelineSettingsError.MAX_EXTRACTION_ATTEMPTS -> R.string.pipeline_error_max_extraction_attempts
    PipelineSettingsError.RETRY_TEMPERATURE -> R.string.pipeline_error_retry_temperature
    PipelineSettingsError.RECONCILIATION_TOLERANCE -> R.string.pipeline_error_reconciliation_tolerance
    PipelineSettingsError.MODEL_REQUIRED -> R.string.pipeline_error_model_required
    PipelineSettingsError.KEEP_ALIVE_REQUIRED -> R.string.pipeline_error_keep_alive_required
}

@Composable
private fun NumberField(
    @StringRes label: Int,
    @StringRes hint: Int,
    value: String,
    error: PipelineSettingsError?,
    enabled: Boolean,
    decimal: Boolean = false,
    onChange: (String) -> Unit,
) {
    KvField(
        label = stringResource(label),
        value = value,
        onValueChange = onChange,
        hint = stringResource(hint),
        error = error?.let { stringResource(it.messageRes()) },
        enabled = enabled,
        keyboardOptions = KeyboardOptions(keyboardType = if (decimal) KeyboardType.Decimal else KeyboardType.Number),
        modifier = Modifier.fillMaxWidth(),
    )
}

@Composable
fun PipelineSettingsRoot(viewModel: PipelineSettingsViewModel = koinViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val toast = rememberKvToastState()
    val context = LocalContext.current

    OnStart(viewModel::refresh)
    ObserveAsEvents(viewModel.messages) { toast.show(it.asString(context)) }

    KvToastBox(toast) {
        LoadableContent(state.settings, onRefresh = viewModel::refresh) {
            PipelineForm(
                state = state,
                onChange = viewModel::change,
                onPickModel = viewModel::pickModel,
                onSave = viewModel::save,
                onDiscard = viewModel::discard,
            )
        }
    }
}

@Composable
private fun PipelineForm(
    state: PipelineFormState,
    onChange: (PipelineSettingsDraft) -> Unit,
    onPickModel: (Boolean) -> Unit,
    onSave: () -> Unit,
    onDiscard: () -> Unit,
) {
    val draft = state.draft
    val errors = state.errors
    val edit = state.canEdit

    Column(Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier
                .weight(1f)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = KvSpace.gutter, vertical = KvSpace.s3),
            verticalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            KvEyebrow(stringResource(R.string.pipeline_mail_collection_group))
            NumberField(
                R.string.pipeline_poll_interval, R.string.pipeline_poll_interval_hint,
                draft.pollIntervalSeconds, errors.pollIntervalSeconds, edit,
            ) { onChange(draft.copy(pollIntervalSeconds = it)) }
            NumberField(
                R.string.pipeline_messages_per_poll, R.string.pipeline_messages_per_poll_hint,
                draft.maxMessagesPerPoll, errors.maxMessagesPerPoll, edit,
            ) { onChange(draft.copy(maxMessagesPerPoll = it)) }

            KvEyebrow(stringResource(R.string.pipeline_extraction_group), Modifier.padding(top = KvSpace.s4))
            KvSelectField(
                label = stringResource(R.string.pipeline_model),
                value = draft.model,
                onClick = { onPickModel(true) },
                enabled = edit && state.models != null,
                hint = stringResource(
                    if (state.models == null) R.string.pipeline_models_unavailable else R.string.pipeline_model_hint,
                ),
                modifier = Modifier.fillMaxWidth(),
            )
            if (state.models == null) KvNote(stringResource(R.string.pipeline_model_unavailable_note))
            KvCheckbox(draft.thinkingMode, { onChange(draft.copy(thinkingMode = it)) }, enabled = edit) {
                Column {
                    BasicText(stringResource(R.string.pipeline_thinking_mode), style = KvType.small)
                    BasicText(stringResource(R.string.pipeline_thinking_mode_hint), style = KvType.hint)
                }
            }
            NumberField(
                R.string.pipeline_num_ctx, R.string.pipeline_num_ctx_hint,
                draft.numCtx, errors.numCtx, edit,
            ) { onChange(draft.copy(numCtx = it)) }
            KvField(
                label = stringResource(R.string.pipeline_keep_alive),
                value = draft.keepAlive,
                onValueChange = { onChange(draft.copy(keepAlive = it)) },
                hint = stringResource(R.string.pipeline_keep_alive_hint),
                error = errors.keepAlive?.let { stringResource(it.messageRes()) },
                enabled = edit,
                modifier = Modifier.fillMaxWidth(),
            )
            NumberField(
                R.string.pipeline_max_extraction_attempts, R.string.pipeline_max_extraction_attempts_hint,
                draft.maxExtractionAttempts, errors.maxExtractionAttempts, edit,
            ) { onChange(draft.copy(maxExtractionAttempts = it)) }
            NumberField(
                R.string.pipeline_retry_temperature, R.string.pipeline_retry_temperature_hint,
                draft.retryTemperature, errors.retryTemperature, edit, decimal = true,
            ) { onChange(draft.copy(retryTemperature = it)) }
            NumberField(
                R.string.pipeline_reconciliation_tolerance, R.string.pipeline_reconciliation_tolerance_hint,
                draft.reconciliationToleranceKr, errors.reconciliationToleranceKr, edit, decimal = true,
            ) { onChange(draft.copy(reconciliationToleranceKr = it)) }

            if (!edit) KvNote(stringResource(R.string.pipeline_settings_read_only))
        }

        if (edit && state.isDirty) {
            KvRule()
            Row(
                modifier = Modifier.fillMaxWidth().padding(horizontal = KvSpace.gutter),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(KvSpace.s3),
            ) {
                KvButton(
                    stringResource(CoreR.string.save),
                    onSave,
                    style = KvButtonStyle.Primary,
                    glyph = PhosphorIcons.Check,
                    enabled = !errors.hasAny,
                    busy = state.isSaving,
                )
                KvButton(stringResource(CoreR.string.discard), onDiscard, style = KvButtonStyle.Ghost)
                BasicText(
                    stringResource(R.string.pipeline_unsaved_changes),
                    style = KvType.hint.copy(color = KvColors.neutral500),
                    modifier = Modifier.weight(1f),
                )
            }
        }
    }

    if (state.pickingModel) {
        PickerSheet(
            title = stringResource(R.string.pipeline_model),
            rows = state.models.orEmpty().map { PickerRow.Option(it, it) },
            selected = draft.model,
            onPick = {
                onChange(draft.copy(model = it))
                onPickModel(false)
            },
            onDismiss = { onPickModel(false) },
        )
    }
}
