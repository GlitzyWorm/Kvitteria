namespace Backend.Features.Update;

/// <summary>
/// The directory shared with the host agent. Everything here tolerates the directory being absent or
/// unwritable, which is what a host without an agent looks like, so that reads as "no agent" rather than as
/// an error.
/// </summary>
public sealed class UpdateStateStore
{
	// Where compose.yaml mounts ./update-state.
	private const string Directory = "/update-state";

	private static readonly string StatusPath = Path.Combine(Directory, "status.json");
	private static readonly string HeartbeatPath = Path.Combine(Directory, "heartbeat");
	private static readonly string RequestPath = Path.Combine(Directory, "request");

	public async Task<string?> ReadStatusAsync(CancellationToken ct)
	{
		try
		{
			return await File.ReadAllTextAsync(StatusPath, ct);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	public DateTime? ReadHeartbeat()
	{
		try
		{
			var file = new FileInfo(HeartbeatPath);
			return file.Exists ? file.LastWriteTimeUtc : null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	/// <summary>
	/// The file's existence is the whole request. The agent never reads it, which is why it stays empty: a
	/// request carries no instructions.
	/// </summary>
	public bool TryRequestUpdate()
	{
		try
		{
			File.WriteAllBytes(RequestPath, []);
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}
}
