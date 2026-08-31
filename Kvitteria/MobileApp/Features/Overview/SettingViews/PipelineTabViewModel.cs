// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kvitteria.Contracts.DTO;
using MobileApp.Features.Shared;
using MobileApp.Localization;
using MobileApp.Services;

namespace MobileApp.Features.Overview.SettingViews;

/// <summary>
/// The household's ingestion-pipeline tuning: mail-poll interval, and the Ollama extraction model
/// and its retry behaviour.
/// </summary>
/// <remarks>
/// Named "Pipeline" rather than "Server" — the mockup's own label — because the app already has an
/// unrelated <c>ServerViewModel</c> under Features/Auth (which server to point the app at). Reads
/// the resolved member from <see cref="ISessionService.User"/>, the same as
/// <see cref="InboxTabViewModel"/>: the gate has already fetched and cached the permission list.
///
/// Edits inline with a dirty-tracked draft rather than through a modal sheet — a flat settings form
/// fits the tab directly. <see cref="RefreshAsync"/> refuses to overwrite an in-progress edit: it is
/// called on every tab appearance and every tab switch (see <c>SettingsPage</c>), and without the
/// dirty guard, switching to Inbox and back mid-edit would silently discard it.
///
/// Every field is its own observable property — the same shape <c>MailboxSheetViewModel</c> uses —
/// rather than one <see cref="PipelineSettingsDraft"/> bound into two-way. A two-way binding writes
/// back by calling the leaf property's setter directly on whatever object it currently points at;
/// <c>PipelineSettingsDraft</c> is an immutable record with no <c>INotifyPropertyChanged</c> of its
/// own, so a binding into <c>Draft.OllamaModel</c> would silently mutate that instance via reflection
/// without ever telling this view model a change happened — nothing would re-validate, and Save would
/// stay disabled forever. <see cref="PipelineSettingsDraft"/> still exists, built fresh from these
/// fields, only where it is genuinely needed: validation and the wire request.
/// </remarks>
public sealed partial class PipelineTabViewModel(
	SettingsApiClient settings,
	ISessionService sessions,
	ILocalizationService localization) : ViewModelBase(localization), IRefreshable
{
	private const string ManageSettings = "ManageSettings";

	/// <summary>The last value the server confirmed, to diff against and to revert to on Discard.</summary>
	private PipelineSettingsDraft _saved = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanEditModel))]
	public partial bool CanManage { get; set; }

	/// <summary>Drives the footnote explaining why every field below is disabled.</summary>
	public bool IsReadOnly => !CanManage;

	[ObservableProperty]
	public partial string PollIntervalSeconds { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string MessagesPerPoll { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string Model { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool ThinkingMode { get; set; }

	[ObservableProperty]
	public partial string NumCtx { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string KeepAlive { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string MaxExtractionAttempts { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string RetryTemperature { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string ReconciliationToleranceKr { get; set; } = string.Empty;

	/// <summary>
	/// A fresh record built from whatever is on screen right now, compared against <see cref="_saved"/>
	/// by structural equality — the same dirty-check idiom as before, just computed on demand instead
	/// of bound directly.
	/// </summary>
	public bool IsDirty => CurrentDraft() != _saved;

	[ObservableProperty]
	public partial string? PollIntervalError { get; set; }

	[ObservableProperty]
	public partial string? MessagesPerPollError { get; set; }

	[ObservableProperty]
	public partial string? ModelError { get; set; }

	[ObservableProperty]
	public partial string? NumCtxError { get; set; }

	[ObservableProperty]
	public partial string? KeepAliveError { get; set; }

	[ObservableProperty]
	public partial string? MaxExtractionAttemptsError { get; set; }

	[ObservableProperty]
	public partial string? RetryTemperatureError { get; set; }

	[ObservableProperty]
	public partial string? ReconciliationToleranceError { get; set; }

	[ObservableProperty]
	public partial bool HasFieldErrors { get; set; }

	/// <summary>Mirrors the mockup's <c>pipelineSaveDisabled</c>.</summary>
	public bool CanSave => IsDirty && !HasFieldErrors;

	public bool CanDiscard => IsDirty;

	/// <summary>
	/// Already-localized, so the view only ever binds to text — the same idiom <see cref="Error"/>
	/// and <see cref="SaveError"/> already use, rather than a bool the view would have to switch on.
	/// </summary>
	public string DirtyNote => Localize(IsDirty ? "PipelineUnsavedChanges" : "PipelineNoUnsavedChanges")!;

	/// <summary>The models Ollama has pulled, for the Model picker.</summary>
	[ObservableProperty]
	public partial List<string> AvailableModels { get; set; } = [];

	/// <summary>
	/// True when the model list could not be fetched. The picker disables itself and shows an inline
	/// error, but the rest of the form — the mail-collection fields — stays independently editable
	/// and saveable: Ollama being unreachable says nothing about whether the IMAP poll settings are
	/// worth changing.
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanEditModel))]
	public partial bool ModelsUnavailable { get; set; }

	/// <summary>Whether the Model picker itself should respond to input.</summary>
	public bool CanEditModel => CanManage && !ModelsUnavailable;

	/// <summary>A whole-tab failure — the settings could not be loaded at all.</summary>
	[ObservableProperty]
	public partial string? Error { get; set; }

	/// <summary>What Save failed with, if it did.</summary>
	[ObservableProperty]
	public partial string? SaveError { get; set; }

	/// <inheritdoc />
	public async Task RefreshAsync(CancellationToken ct)
	{
		if (IsBusy || IsDirty)
		{
			return;
		}

		IsBusy = true;
		Error = null;

		try
		{
			CurrentUserDto? me = sessions.User;
			CanManage = me?.Permissions.Contains(ManageSettings) is true;

			ApiResult<PipelineSettingsDto> result = await settings.GetPipelineSettingsAsync(ct);

			if (!result.IsOk || result.Value is null)
			{
				Error = Localization[result.Outcome is ApiOutcome.Unauthenticated
					? "PipelineErrorUnauthenticated"
					: "PipelineErrorUnreachable"];
				return;
			}

			// Models are loaded before the fields are filled in, so the Model picker's item list
			// already contains the current model by the time its selection is set. Setting a Picker's
			// SelectedItem before ItemsSource holds a match leaves it showing nothing, and that lookup
			// is never retried once the list arrives — it would look as if the picker had forgotten
			// which model was configured.
			await LoadModelsAsync(result.Value.OllamaModel, ct);
			Fill(result.Value);
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// A failure here does not touch <see cref="Error"/> — the settings themselves already loaded,
	/// and only the model picker needs to know.
	/// </summary>
	private async Task LoadModelsAsync(string currentModel, CancellationToken ct)
	{
		ApiResult<OllamaModelsDto> result = await settings.GetOllamaModelsAsync(ct);

		if (result.IsOk && result.Value is not null)
		{
			ModelsUnavailable = false;
			AvailableModels = result.Value.Models;
			return;
		}

		ModelsUnavailable = true;
		AvailableModels = string.IsNullOrWhiteSpace(currentModel) ? [] : [currentModel];
	}

	[RelayCommand]
	private Task ReloadAsync() => RefreshAsync(CancellationToken.None);

	[RelayCommand]
	private async Task SaveAsync()
	{
		if (IsBusy || !CanSave)
		{
			return;
		}

		IsBusy = true;
		SaveError = null;

		try
		{
			ApiResult<PipelineSettingsDto> result = await settings.UpdatePipelineSettingsAsync(CurrentDraft(), CancellationToken.None);

			if (!result.IsOk || result.Value is null)
			{
				SaveError = Describe(result.Outcome, result.Detail);
				return;
			}

			Fill(result.Value);
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private void Discard()
	{
		if (!CanDiscard)
		{
			return;
		}

		Apply(_saved);
		SaveError = null;
		Recompute();
	}

	/// <summary>Loads a confirmed value from the server into every field, and adopts it as the baseline.</summary>
	private void Fill(PipelineSettingsDto dto)
	{
		_saved = ToDraft(dto);
		Apply(_saved);
		Recompute();
	}

	private void Apply(PipelineSettingsDraft draft)
	{
		PollIntervalSeconds = draft.ImapPollIntervalSeconds;
		MessagesPerPoll = draft.ImapMaxMessagesPerPoll;
		Model = draft.OllamaModel;
		ThinkingMode = draft.OllamaThinkingMode;
		NumCtx = draft.OllamaNumCtx;
		KeepAlive = draft.OllamaKeepAlive;
		MaxExtractionAttempts = draft.OllamaMaxExtractionAttempts;
		RetryTemperature = draft.OllamaRetryTemperature;
		ReconciliationToleranceKr = draft.OllamaReconciliationToleranceKr;
	}

	private PipelineSettingsDraft CurrentDraft() => new()
	{
		ImapPollIntervalSeconds = PollIntervalSeconds,
		ImapMaxMessagesPerPoll = MessagesPerPoll,
		OllamaModel = Model,
		OllamaThinkingMode = ThinkingMode,
		OllamaNumCtx = NumCtx,
		OllamaKeepAlive = KeepAlive,
		OllamaMaxExtractionAttempts = MaxExtractionAttempts,
		OllamaRetryTemperature = RetryTemperature,
		OllamaReconciliationToleranceKr = ReconciliationToleranceKr
	};

	private static PipelineSettingsDraft ToDraft(PipelineSettingsDto dto) => new()
	{
		ImapPollIntervalSeconds = dto.ImapPollIntervalSeconds.ToString(CultureInfo.InvariantCulture),
		ImapMaxMessagesPerPoll = dto.ImapMaxMessagesPerPoll.ToString(CultureInfo.InvariantCulture),
		OllamaModel = dto.OllamaModel,
		OllamaThinkingMode = dto.OllamaThinkingMode,
		OllamaNumCtx = dto.OllamaNumCtx.ToString(CultureInfo.InvariantCulture),
		OllamaKeepAlive = dto.OllamaKeepAlive,
		OllamaMaxExtractionAttempts = dto.OllamaMaxExtractionAttempts.ToString(CultureInfo.InvariantCulture),
		OllamaRetryTemperature = dto.OllamaRetryTemperature.ToString(CultureInfo.InvariantCulture),
		OllamaReconciliationToleranceKr = dto.OllamaReconciliationToleranceKr.ToString(CultureInfo.InvariantCulture)
	};

	partial void OnPollIntervalSecondsChanged(string value) => Recompute();

	partial void OnMessagesPerPollChanged(string value) => Recompute();

	partial void OnModelChanged(string value) => Recompute();

	partial void OnThinkingModeChanged(bool value) => Recompute();

	partial void OnNumCtxChanged(string value) => Recompute();

	partial void OnKeepAliveChanged(string value) => Recompute();

	partial void OnMaxExtractionAttemptsChanged(string value) => Recompute();

	partial void OnRetryTemperatureChanged(string value) => Recompute();

	partial void OnReconciliationToleranceKrChanged(string value) => Recompute();

	/// <summary>
	/// Re-validates against whatever is on screen right now and re-derives everything that depends on
	/// it. Called from every field's own change hook, so it runs once per keystroke rather than once
	/// per save — the same cost <c>MailboxSheetViewModel</c> would pay too if it validated live instead
	/// of only on Save.
	/// </summary>
	private void Recompute()
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(CurrentDraft());

		PollIntervalError = Localize(errors.ImapPollIntervalSeconds);
		MessagesPerPollError = Localize(errors.ImapMaxMessagesPerPoll);
		ModelError = Localize(errors.OllamaModel);
		NumCtxError = Localize(errors.OllamaNumCtx);
		KeepAliveError = Localize(errors.OllamaKeepAlive);
		MaxExtractionAttemptsError = Localize(errors.OllamaMaxExtractionAttempts);
		RetryTemperatureError = Localize(errors.OllamaRetryTemperature);
		ReconciliationToleranceError = Localize(errors.OllamaReconciliationToleranceKr);
		HasFieldErrors = errors.HasAny;

		OnPropertyChanged(nameof(IsDirty));
		OnPropertyChanged(nameof(CanSave));
		OnPropertyChanged(nameof(CanDiscard));
		OnPropertyChanged(nameof(DirtyNote));
	}

	/// <summary>
	/// The server's own sentence is used verbatim where it knows something this app cannot work
	/// out — a model that is not pulled (naming the ones that are), Ollama unreachable. Everything
	/// the app can categorize itself is localized. Same idiom as <c>MailboxSheetViewModel.Describe</c>.
	/// </summary>
	private string Describe(ApiOutcome outcome, string? detail) => outcome switch
	{
		ApiOutcome.Verification or ApiOutcome.Invalid when !string.IsNullOrWhiteSpace(detail) => detail,
		ApiOutcome.Forbidden => Localization["PipelineErrorForbidden"],
		ApiOutcome.NotFound => Localization["PipelineErrorNotFound"],
		ApiOutcome.Unauthenticated => Localization["PipelineErrorUnauthenticated"],
		_ => Localization["PipelineErrorUnreachable"]
	};
}
