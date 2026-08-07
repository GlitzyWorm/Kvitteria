using System.Threading.Channels;
using Backend.Features.Mail;
namespace Backend.Features.Extraction;

public sealed class ExtractionQueue
{
	private readonly Channel<Receipt> _channel = Channel.CreateBounded<Receipt>(
		new BoundedChannelOptions(100)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true
		});

	public ValueTask EnqueueAsync(Receipt record, CancellationToken ct) =>
	_channel.Writer.WriteAsync(record, ct);

	public IAsyncEnumerable<Receipt> DequeueAllAsync(CancellationToken ct) =>
	_channel.Reader.ReadAllAsync(ct);
}