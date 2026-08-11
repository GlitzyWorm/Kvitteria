using System.Threading.Channels;
using Backend.Features.Mail;
namespace Backend.Features.Extraction;

public sealed class ExtractionQueue
{
	private readonly Channel<ReceiptRecord> _channel = Channel.CreateBounded<ReceiptRecord>(
		new BoundedChannelOptions(100)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true
		});

	public ValueTask EnqueueAsync(ReceiptRecord record, CancellationToken ct) =>
	_channel.Writer.WriteAsync(record, ct);

	public IAsyncEnumerable<ReceiptRecord> DequeueAllAsync(CancellationToken ct) =>
	_channel.Reader.ReadAllAsync(ct);
}