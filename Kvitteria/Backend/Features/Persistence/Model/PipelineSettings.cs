namespace Backend.Features.Persistence.Model;

/// <summary>
/// Ingestion-pipeline tuning that is safe to change without a restart: exactly one row exists, the
/// same way <see cref="Users.HouseholdSettings"/> is one row for permissions. Kept separate from
/// <see cref="Users.HouseholdSettings"/> because this is about pipeline behavior, not who may do what.
/// Everything that would break connectivity or requires an out-of-band action (Database connection
/// string, Auth signing, Ollama base URL, pulling a new Ollama model before selecting it) stays in
/// appsettings instead.
/// </summary>
public sealed class PipelineSettings
{
	public Guid Id { get; set; }

	public int ImapPollIntervalSeconds { get; set; } = 60;

	/// <summary>Caps one poll so a large unseen backlog cannot hold the IMAP session open indefinitely.</summary>
	public int ImapMaxMessagesPerPoll { get; set; } = 25;

	public string OllamaModel { get; set; } = "qwen3:4b";

	public bool OllamaThinkingMode { get; set; }

	public int OllamaNumCtx { get; set; } = 8192;

	public string OllamaKeepAlive { get; set; } = "30m";

	public int OllamaMaxExtractionAttempts { get; set; } = 3;

	/// <summary>Attempt 1 always runs at temperature 0; retries use this.</summary>
	public double OllamaRetryTemperature { get; set; } = 0.3;

	/// <summary>
	/// An extraction counts as reconciled when <c>grand_total - sum(line_totals) - pant_total</c> is
	/// within this many kroner of zero. Danish receipts round to øre.
	/// </summary>
	public decimal OllamaReconciliationToleranceKr { get; set; } = 0.01M;
}
