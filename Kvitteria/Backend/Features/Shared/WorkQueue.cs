using System.Threading.Channels;
namespace Backend.Features.Shared;

/// <summary>
/// A bounded single-reader, single-writer hand-off between two pipeline stages. A full queue
/// blocks the writer rather than dropping items.
/// </summary>
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
