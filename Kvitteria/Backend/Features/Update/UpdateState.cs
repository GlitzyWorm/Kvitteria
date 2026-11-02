using System.Text.Json;
namespace Backend.Features.Update;

/// <summary>The last update as <c>scripts/update.sh</c> recorded it.</summary>
public sealed record UpdateRun(string State, string? TargetVersion, string? Detail, DateTime UpdatedAt)
{
	public const string Running = "running";
	public const string RestoreRequired = "restore-required";

	/// <summary>Not written by the script: what a <see cref="Running"/> too old to be true reads as.</summary>
	public const string Interrupted = "interrupted";

	/// <summary>Longer than a slow update, and short enough that a lost host doesn't strand anyone for long.</summary>
	private static readonly TimeSpan RunningLimit = TimeSpan.FromMinutes(30);

	private sealed record StatusFile(string? State, string? TargetVersion, string? Detail, DateTime? UpdatedAt);

	/// <summary>
	/// Null for a missing, unreadable or incomplete file, which all mean the same thing to a reader: no
	/// update has been recorded.
	/// </summary>
	public static UpdateRun? Parse(string? json, DateTime now)
	{
		if (string.IsNullOrWhiteSpace(json))
			return null;

		StatusFile? file;

		try
		{
			file = JsonSerializer.Deserialize<StatusFile>(json, JsonSerializerOptions.Web);
		}
		catch (JsonException)
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(file?.State) || file.UpdatedAt is not { } updatedAt)
			return null;

		string state = file.State == Running && now - updatedAt.ToUniversalTime() > RunningLimit ? Interrupted : file.State;

		return new UpdateRun(state, file.TargetVersion, file.Detail, updatedAt);
	}
}

public enum UpdateRefusal
{
	None,
	NothingToInstall,
	AlreadyRunning,
	RestoreRequired,
	NoAgent
}

public static class UpdateGate
{
	/// <summary>Three of the agent's beats, so one late tick isn't a dead agent.</summary>
	private static readonly TimeSpan HeartbeatLimit = TimeSpan.FromMinutes(15);

	public static bool IsAgentAlive(DateTime? heartbeat, DateTime now) =>
		heartbeat is { } beat && now - beat < HeartbeatLimit;

	/// <summary>Why an update can't start now, if it can't. A run in progress outranks everything else.</summary>
	public static UpdateRefusal Decide(bool updateAvailable, UpdateRun? lastRun, bool agentAlive) =>
		lastRun?.State switch {
			UpdateRun.Running => UpdateRefusal.AlreadyRunning,
			UpdateRun.RestoreRequired => UpdateRefusal.RestoreRequired,
			_ when !updateAvailable => UpdateRefusal.NothingToInstall,
			_ when !agentAlive => UpdateRefusal.NoAgent,
			_ => UpdateRefusal.None
		};
}
