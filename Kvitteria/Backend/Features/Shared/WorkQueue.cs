using System.Threading.Channels;
namespace Backend.Features.Shared;

/// <summary>
/// A bounded single-reader, single-writer hand-off between two pipeline stages.
/// </summary>
/// <remarks>
/// Both pipeline queues carry <c>ReceiptRecord</c>, so a bare <c>WorkQueue&lt;ReceiptRecord&gt;</c>
/// could not be resolved unambiguously from the container. Deriving a named type per stage keeps
/// plain constructor injection and a self-describing DI graph, without keyed-service string keys.
///
/// <c>FullMode.Wait</c> means a stalled downstream stage backpressures all the way to the mail
/// poller rather than dropping records.
/// </remarks>
public abstract class WorkQueue<T>(int capacity)
{
	private readonly Channel<T> _channel = Channel.CreateBounded<T>(
		new BoundedChannelOptions(capacity)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true,
			SingleWriter = true
		});

	public ValueTask EnqueueAsync(T item, CancellationToken ct) =>
		_channel.Writer.WriteAsync(item, ct);

	public IAsyncEnumerable<T> DequeueAllAsync(CancellationToken ct) =>
		_channel.Reader.ReadAllAsync(ct);
}
