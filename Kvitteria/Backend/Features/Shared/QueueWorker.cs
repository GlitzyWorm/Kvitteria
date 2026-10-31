namespace Backend.Features.Shared;

/// <summary>Drains a <see cref="WorkQueue{T}"/>, handling each item in isolation.</summary>
public abstract class QueueWorker<TItem>(WorkQueue<TItem> queue, ILogger logger) : BackgroundService
{
	/// <summary>Used in log messages; the concrete worker's role in the pipeline.</summary>
	protected abstract string WorkerName { get; }

	protected abstract Task ProcessAsync(TItem item, CancellationToken ct);

	/// <summary>
	/// Record the failure durably. Called for faults only: shutdown is not a failure, the item stays as
	/// it is and is resumed at the next start. Exceptions thrown here are logged and swallowed.
	/// </summary>
	protected abstract Task OnFailedAsync(TItem item, string error);

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
				logger.LogInformation(
					"{Worker} stopped by shutdown while handling {Item}; it will be resumed at next startup.",
					WorkerName, Describe(item));
				return;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "{Worker} failed for {Item}", WorkerName, Describe(item));
				await RecordFailureAsync(item, PipelineError.Describe(ex));
			}
		}
	}

	/// <summary>
	/// A database outage must not end the worker either. If this write is lost the row stays in progress
	/// and is retried at next startup rather than being lost.
	/// </summary>
	private async Task RecordFailureAsync(TItem item, string error)
	{
		try
		{
			await OnFailedAsync(item, error);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex, "{Worker} could not record the failure for {Item}; it will be resumed at next startup.",
				WorkerName, Describe(item));
		}
	}
}
