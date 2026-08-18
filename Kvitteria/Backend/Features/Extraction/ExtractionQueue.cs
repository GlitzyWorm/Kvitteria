using Backend.Features.Mail;
using Backend.Features.Shared;
namespace Backend.Features.Extraction;

/// <summary>Mail waiting to be extracted. Written by MailWorker, read by ExtractionWorker.</summary>
public sealed class ExtractionQueue() : WorkQueue<ReceiptRecord>(Capacity)
{
	public const int Capacity = 100;
}
