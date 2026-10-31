using Backend.Features.Extraction;
using Backend.Features.Mail;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Kvitteria.Contracts.Shared;

namespace Backend.Features.Persistence.Factory;

public sealed class ReceiptFactoryWorker(
	IServiceScopeFactory scopeFactory,
	PersistenceQueue queue,
	ReceiptIntakeStore store,
	ILogger<ReceiptFactoryWorker> logger
) : QueueWorker<ReceiptRecord>(queue, logger)
{
	protected override string WorkerName => "Receipt factory";

	protected override string Describe(ReceiptRecord item) => item.Subject;

	protected override async Task OnFailedAsync(ReceiptRecord item, string error)
	{
		item.Status = ReceiptStatus.Failed;
		item.Error = error;
		item.CompletedAt = DateTimeOffset.UtcNow;

		await store.MarkFailedAsync(item.IntakeId, error, item.Attempts, CancellationToken.None);
	}

	protected override async Task ProcessAsync(ReceiptRecord record, CancellationToken ct)
	{
		if (record.Extracted is null)
		{
			logger.LogWarning("Skipping '{Subject}': no extraction to persist.", record.Subject);
			return;
		}
		
		using IServiceScope scope = scopeFactory.CreateScope();
		var ingestion = scope.ServiceProvider.GetRequiredService<ReceiptIngestionService>();

		Receipt receipt = await ingestion.IngestAsync(record, record.Extracted, ct);

		record.Status = ReceiptStatus.Completed;
		record.CompletedAt = DateTimeOffset.UtcNow;

		logger.LogInformation(
			"Persisted receipt {ReceiptId} for '{Subject}': {LineCount} lines, total {GrandTotal}, reconciled={Reconciled}",
			receipt.Id, record.Subject, receipt.ReceiptLines.Count, receipt.GrandTotal, receipt.Reconciled);
	}
}
