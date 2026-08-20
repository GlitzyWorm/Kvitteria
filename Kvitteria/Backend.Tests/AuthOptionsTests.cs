using Backend.Features.Auth;
namespace Backend.Tests;

public class AuthOptionsTests
{
	private const string LocalSecret = "super-secret-jwt-token-with-at-least-32-characters-long";
	private const string LocalJwks = "http://host.docker.internal:54321/auth/v1/.well-known/jwks.json";

	/// <summary>
	/// The whole point of validating at startup: no signing source otherwise surfaces as a 401 with no
	/// explanation, on the first request rather than on boot.
	/// </summary>
	[Fact]
	public void A_signing_source_is_required()
	{
		var options = new AuthOptions { JwksUrl = null, SharedSecret = null };

		Assert.NotNull(options.Validate());
	}

	/// <summary>Whitespace is not a configured value, however much it looks like one in JSON.</summary>
	[Fact]
	public void Whitespace_does_not_count_as_a_signing_source()
	{
		var options = new AuthOptions { JwksUrl = "   ", SharedSecret = "  " };

		Assert.NotNull(options.Validate());
	}

	[Fact]
	public void The_default_configuration_points_at_the_local_stack_and_is_usable()
	{
		Assert.Null(new AuthOptions().Validate());
	}

	[Fact]
	public void A_jwks_url_alone_is_valid()
	{
		Assert.Null(new AuthOptions { JwksUrl = LocalJwks, SharedSecret = null }.Validate());
	}

	[Fact]
	public void A_shared_secret_alone_is_valid()
	{
		Assert.Null(new AuthOptions { JwksUrl = null, SharedSecret = LocalSecret }.Validate());
	}

	/// <summary>
	/// Both being set is the normal state for anyone choosing the secret, since JwksUrl carries a default.
	/// Failing startup on it would mean blanking a value to opt out of it.
	/// </summary>
	[Fact]
	public void Both_together_are_allowed_because_the_secret_takes_precedence()
	{
		Assert.Null(new AuthOptions { JwksUrl = LocalJwks, SharedSecret = LocalSecret }.Validate());
	}
}
