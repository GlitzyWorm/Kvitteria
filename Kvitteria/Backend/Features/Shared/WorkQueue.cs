using System.Threading.Channels;
namespace Backend.Features.Shared;

/// <summary>
/// A bounded single-reader hand-off between two pipeline stages. A full queue blocks the writer
/// rather than dropping items. Several writers are allowed: the mail poller, startup recovery and the
/// retry endpoint all feed the extraction queue.
/// </summary>
public abstract class WorkQueue<T>(int capacity)
{
	private readonly Channel<T> _channel = Channel.CreateBounded<T>(
		new BoundedChannelOptions(capacity)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true,
			SingleWriter = false
		});

	public ValueTask EnqueueAsync(T item, CancellationToken ct) =>
		_channel.Writer.WriteAsync(item, ct);

	/// <summary>Enqueues without waiting. False means the queue is full.</summary>
	public bool TryEnqueue(T item) =>
		_channel.Writer.TryWrite(item);

	public IAsyncEnumerable<T> DequeueAllAsync(CancellationToken ct) =>
		_channel.Reader.ReadAllAsync(ct);
}
