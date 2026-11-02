using Backend.Features.Update;
namespace Backend.Tests;

public class UpdateStateTests
{
	private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

	private static string Status(string state, DateTime updatedAt) =>
		$$"""{"state":"{{state}}","targetVersion":"1.2.0","detail":"why","updatedAt":"{{updatedAt:yyyy-MM-ddTHH:mm:ssZ}}"}""";

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("not json")]
	[InlineData("[]")]
	[InlineData("{}")]
	[InlineData("""{"state":"running"}""")]
	[InlineData("""{"state":"running","updatedAt":"yesterday"}""")]
	public void An_unusable_file_reads_as_no_update(string? json)
	{
		Assert.Null(UpdateRun.Parse(json, Now));
	}

	[Fact]
	public void Reads_what_the_script_wrote()
	{
		UpdateRun? run = UpdateRun.Parse(Status("rolled-back", Now.AddHours(-3)), Now);

		Assert.Equal("rolled-back", run?.State);
		Assert.Equal("1.2.0", run?.TargetVersion);
		Assert.Equal("why", run?.Detail);
		Assert.Equal(Now.AddHours(-3), run?.UpdatedAt);
	}

	[Theory]
	[InlineData(29, "running")]
	[InlineData(31, "interrupted")]
	public void A_running_state_older_than_half_an_hour_is_not_believed(int minutes, string expected)
	{
		Assert.Equal(expected, UpdateRun.Parse(Status("running", Now.AddMinutes(-minutes)), Now)?.State);
	}

	[Fact]
	public void Only_running_goes_stale()
	{
		Assert.Equal("restore-required", UpdateRun.Parse(Status("restore-required", Now.AddDays(-30)), Now)?.State);
	}

	[Theory]
	[InlineData(14, true)]
	[InlineData(16, false)]
	public void The_agent_is_alive_while_its_heartbeat_is_recent(int minutes, bool expected)
	{
		Assert.Equal(expected, UpdateGate.IsAgentAlive(Now.AddMinutes(-minutes), Now));
	}

	[Fact]
	public void No_heartbeat_means_no_agent()
	{
		Assert.False(UpdateGate.IsAgentAlive(null, Now));
	}

	private static UpdateRun Run(string state) => new(state, "1.2.0", null, Now);

	[Fact]
	public void An_available_update_with_a_live_agent_can_start()
	{
		Assert.Equal(UpdateRefusal.None, UpdateGate.Decide(true, null, true));
		Assert.Equal(UpdateRefusal.None, UpdateGate.Decide(true, Run("rolled-back"), true));
		Assert.Equal(UpdateRefusal.None, UpdateGate.Decide(true, Run(UpdateRun.Interrupted), true));
	}

	[Fact]
	public void Nothing_to_install_is_refused()
	{
		Assert.Equal(UpdateRefusal.NothingToInstall, UpdateGate.Decide(false, null, true));
	}

	[Fact]
	public void No_agent_is_refused()
	{
		Assert.Equal(UpdateRefusal.NoAgent, UpdateGate.Decide(true, null, false));
	}

	[Fact]
	public void A_run_in_progress_outranks_everything_else()
	{
		Assert.Equal(UpdateRefusal.AlreadyRunning, UpdateGate.Decide(false, Run(UpdateRun.Running), false));
	}

	[Fact]
	public void A_pending_restore_blocks_an_update()
	{
		Assert.Equal(UpdateRefusal.RestoreRequired, UpdateGate.Decide(true, Run(UpdateRun.RestoreRequired), true));
	}
}
