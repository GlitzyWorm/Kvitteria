namespace Backend.Features.Shared;

/// <summary>Drains a <see cref="WorkQueue{T}"/>, handling each item in isolation.</summary>
public abstract class QueueWorker<TItem>(WorkQueue<TItem> queue, ILogger logger) : BackgroundService
{
	/// <summary>Used in log messages; the concrete worker's role in the pipeline.</summary>
	protected abstract string WorkerName { get; }

	protected abstract Task ProcessAsync(TItem item, CancellationToken ct);

	/// <summary>Record the failure on the item. Called for both faults and shutdown.</summary>
	protected abstract void OnFailed(TItem item, string error);

	/// <summary>Short identifier for logs.</summary>
	protected abstract string Describe(TItem item);

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		await foreach (TItem item in queue.DequeueAllAsync(ct))
		{
			try
			{
				await ProcessAsync(item, ct);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				OnFailed(item, $"{WorkerName} interrupted by shutdown");
				logger.LogInformation(
					"{Worker} stopped by shutdown while handling {Item}.", WorkerName, Describe(item));
				return;
			}
			catch (Exception ex)
			{
				OnFailed(item, ex.Message);
				logger.LogError(ex, "{Worker} failed for {Item}", WorkerName, Describe(item));
			}
		}
	}
}
