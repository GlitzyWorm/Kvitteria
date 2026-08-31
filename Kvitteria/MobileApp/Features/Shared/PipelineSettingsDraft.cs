// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Shared;

/// <summary>
/// What the Pipeline tab edits, before it becomes a <c>PutPipelineSettingsDto</c>.
/// </summary>
/// <remarks>
/// Deliberately not that DTO, for the same reason as <see cref="MailboxDraft"/>: this type is
/// validated by <see cref="PipelineSettingsDraftValidator"/>, which is linked into MobileApp.Tests
/// as source, and that project holds no project reference at all, not even to Contracts. Mapping
/// to the wire type happens in <c>SettingsApiClient</c>, at the boundary where a Contracts
/// reference costs nothing.
///
/// Every numeric field is a string because the <c>Entry</c> it is bound to is: a half-typed number
/// is not an int or a decimal, and a numeric-typed property would fight the person typing it — the
/// same reasoning as <see cref="MailboxDraft.Port"/>.
///
/// This is a record deliberately: the Pipeline tab edits inline with a dirty-tracked draft rather
/// than through a modal sheet, so "has anything changed" needs to be an equality check between two
/// instances rather than something tracked by hand. <c>MailboxDraft</c> has no such need — a sheet
/// is either open or it isn't.
/// </remarks>
public sealed record PipelineSettingsDraft
{
	public string ImapPollIntervalSeconds { get; init; } = string.Empty;

	public string ImapMaxMessagesPerPoll { get; init; } = string.Empty;

	public string OllamaModel { get; init; } = string.Empty;

	public bool OllamaThinkingMode { get; init; }

	public string OllamaNumCtx { get; init; } = string.Empty;

	public string OllamaKeepAlive { get; init; } = string.Empty;

	public string OllamaMaxExtractionAttempts { get; init; } = string.Empty;

	public string OllamaRetryTemperature { get; init; } = string.Empty;

	public string OllamaReconciliationToleranceKr { get; init; } = string.Empty;
}

/// <summary>
/// One error per field, as a resource key rather than a message — the same arrangement
/// <see cref="MailboxDraftValidator"/> uses, which keeps localization in the view model. Unlike the
/// mailbox form's single whole-form error, the Pipeline tab shows an error inline under each field,
/// so each one needs its own slot.
/// </summary>
public sealed record PipelineSettingsFieldErrors(
	string? ImapPollIntervalSeconds = null,
	string? ImapMaxMessagesPerPoll = null,
	string? OllamaModel = null,
	string? OllamaNumCtx = null,
	string? OllamaKeepAlive = null,
	string? OllamaMaxExtractionAttempts = null,
	string? OllamaRetryTemperature = null,
	string? OllamaReconciliationToleranceKr = null)
{
	public static readonly PipelineSettingsFieldErrors None = new();

	public bool HasAny =>
		ImapPollIntervalSeconds is not null ||
		ImapMaxMessagesPerPoll is not null ||
		OllamaModel is not null ||
		OllamaNumCtx is not null ||
		OllamaKeepAlive is not null ||
		OllamaMaxExtractionAttempts is not null ||
		OllamaRetryTemperature is not null ||
		OllamaReconciliationToleranceKr is not null;
}

/// <summary>
/// Mirrors <c>PipelineSettingsValidation.TryValidate</c> on the backend field for field, plus two
/// bounds the backend does not enforce at all (<c>OllamaRetryTemperature</c>,
/// <c>OllamaReconciliationToleranceKr</c>) — adopted here as client-only hints, matching the design
/// reference, without changing what the server actually accepts.
/// </summary>
public static class PipelineSettingsDraftValidator
{
	public const int MinPollIntervalSeconds = 1;
	public const int MaxPollIntervalSeconds = 86_400;

	public const int MinMessagesPerPoll = 1;
	public const int MaxMessagesPerPoll = 1_000;

	public const int MinNumCtx = 256;
	public const int MaxNumCtx = 1_048_576;

	public const int MinExtractionAttempts = 1;
	public const int MaxExtractionAttempts = 10;

	public const double MinRetryTemperature = 0;
	public const double MaxRetryTemperature = 2;

	public const decimal MinReconciliationToleranceKr = 0;
	public const decimal MaxReconciliationToleranceKr = 100;

	public static PipelineSettingsFieldErrors Validate(PipelineSettingsDraft draft) => new(
		ImapPollIntervalSeconds:
			IntOutOfRange(draft.ImapPollIntervalSeconds, MinPollIntervalSeconds, MaxPollIntervalSeconds)
				? "PipelineErrorPollInterval" : null,
		ImapMaxMessagesPerPoll:
			IntOutOfRange(draft.ImapMaxMessagesPerPoll, MinMessagesPerPoll, MaxMessagesPerPoll)
				? "PipelineErrorMessagesPerPoll" : null,
		OllamaModel:
			string.IsNullOrWhiteSpace(draft.OllamaModel) ? "PipelineErrorModelRequired" : null,
		OllamaNumCtx:
			IntOutOfRange(draft.OllamaNumCtx, MinNumCtx, MaxNumCtx) ? "PipelineErrorNumCtx" : null,
		OllamaKeepAlive:
			string.IsNullOrWhiteSpace(draft.OllamaKeepAlive) ? "PipelineErrorKeepAliveRequired" : null,
		OllamaMaxExtractionAttempts:
			IntOutOfRange(draft.OllamaMaxExtractionAttempts, MinExtractionAttempts, MaxExtractionAttempts)
				? "PipelineErrorMaxExtractionAttempts" : null,
		OllamaRetryTemperature:
			DoubleOutOfRange(draft.OllamaRetryTemperature, MinRetryTemperature, MaxRetryTemperature)
				? "PipelineErrorRetryTemperature" : null,
		OllamaReconciliationToleranceKr:
			DecimalOutOfRange(draft.OllamaReconciliationToleranceKr, MinReconciliationToleranceKr, MaxReconciliationToleranceKr)
				? "PipelineErrorReconciliationTolerance" : null);

	/// <summary>
	/// The parsed field values, once <see cref="Validate"/> has already passed. Returns primitives
	/// rather than a <c>PutPipelineSettingsDto</c> deliberately: that type lives in Contracts, which
	/// this file must not reference — the same reason <c>MailboxDraftValidator.ParsePort</c> returns
	/// an <c>int</c> rather than a DTO. <c>SettingsApiClient</c> builds the DTO from these, at the
	/// boundary where a Contracts reference costs nothing.
	/// </summary>
	public static int ParsePollIntervalSeconds(PipelineSettingsDraft draft) =>
		int.Parse(draft.ImapPollIntervalSeconds, System.Globalization.CultureInfo.InvariantCulture);

	public static int ParseMessagesPerPoll(PipelineSettingsDraft draft) =>
		int.Parse(draft.ImapMaxMessagesPerPoll, System.Globalization.CultureInfo.InvariantCulture);

	public static int ParseNumCtx(PipelineSettingsDraft draft) =>
		int.Parse(draft.OllamaNumCtx, System.Globalization.CultureInfo.InvariantCulture);

	public static int ParseMaxExtractionAttempts(PipelineSettingsDraft draft) =>
		int.Parse(draft.OllamaMaxExtractionAttempts, System.Globalization.CultureInfo.InvariantCulture);

	public static double ParseRetryTemperature(PipelineSettingsDraft draft) =>
		double.Parse(draft.OllamaRetryTemperature, System.Globalization.CultureInfo.InvariantCulture);

	public static decimal ParseReconciliationToleranceKr(PipelineSettingsDraft draft) =>
		decimal.Parse(draft.OllamaReconciliationToleranceKr, System.Globalization.CultureInfo.InvariantCulture);

	private static bool IntOutOfRange(string value, int min, int max) =>
		!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed) ||
		parsed < min || parsed > max;

	private static bool DoubleOutOfRange(string value, double min, double max) =>
		!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) ||
		parsed < min || parsed > max;

	private static bool DecimalOutOfRange(string value, decimal min, decimal max) =>
		!decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal parsed) ||
		parsed < min || parsed > max;
}
