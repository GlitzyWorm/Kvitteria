using Backend.Features.Persistence.Factory;
using Microsoft.Extensions.Logging.Abstractions;
namespace Backend.Tests;

public class PurchaseDateTests
{
	private static readonly DateTimeOffset MailDate = new(2026, 8, 9, 11, 17, 40, TimeSpan.FromHours(2));

	[Theory]
	[InlineData("2026-08-09 11:17:40")]   // the format the prompt asks for
	[InlineData("2026-08-09T11:17:40")]
	[InlineData("2026-08-09 11:17")]
	[InlineData("2026-08-09")]
	public void Accepts_the_formats_the_model_produces(string value) =>
		Assert.True(PurchaseDate.TryParseLocal(value, out _));

	[Theory]
	[InlineData("")]
	[InlineData("4. juli 2026")]           // the model was asked to convert these, but sometimes does not
	[InlineData("09-08-2026 11:17:40")]
	[InlineData("not a date at all")]
	public void Rejects_rather_than_throwing_on_unparseable_output(string value) =>
		Assert.False(PurchaseDate.TryParseLocal(value, out _));

	/// <summary>A FormatException here used to kill the receipt factory outright.</summary>
	[Fact]
	public void Falls_back_to_the_mail_date_when_the_model_output_is_unusable()
	{
		DateTime resolved = PurchaseDate.Resolve("garbage", MailDate, NullLogger.Instance, "Kvittering");

		Assert.Equal(MailDate.UtcDateTime, resolved);
	}

	[Fact]
	public void Converts_Copenhagen_local_time_to_utc()
	{
		// 9 August is CEST, i.e. UTC+2.
		DateTime resolved = PurchaseDate.Resolve("2026-08-09 11:17:40", MailDate, NullLogger.Instance, "Kvittering");

		Assert.Equal(new DateTime(2026, 8, 9, 9, 17, 40, DateTimeKind.Utc), resolved);
		Assert.Equal(DateTimeKind.Utc, resolved.Kind);
	}

	/// <summary>
	/// 02:30 on the last Sunday of March is a wall-clock time that never happens; ConvertTimeToUtc
	/// throws ArgumentException on it, which would have failed the whole receipt.
	/// </summary>
	[Fact]
	public void Survives_the_spring_forward_gap()
	{
		DateTime resolved = PurchaseDate.ToUtc(new DateTime(2026, 3, 29, 2, 30, 0));

		Assert.Equal(DateTimeKind.Utc, resolved.Kind);
		Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), resolved);
	}

	/// <summary>The autumn overlap is ambiguous rather than invalid, so it must simply not throw.</summary>
	[Fact]
	public void Handles_the_fall_back_overlap()
	{
		DateTime resolved = PurchaseDate.ToUtc(new DateTime(2026, 10, 25, 2, 30, 0));

		Assert.Equal(DateTimeKind.Utc, resolved.Kind);
	}
}
