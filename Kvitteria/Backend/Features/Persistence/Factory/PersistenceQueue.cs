using Backend.Features.Mail;
using Backend.Features.Shared;
namespace Backend.Features.Persistence.Factory;

/// <summary>Extractions waiting to be persisted. Written by ExtractionWorker, read by ReceiptFactoryWorker.</summary>
public sealed class PersistenceQueue() : WorkQueue<ReceiptRecord>(Capacity)
{
	public const int Capacity = 100;
}
