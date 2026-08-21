using Backend.Features.Auth;
namespace Backend.Tests;

public class AuthOptionsTests
{
	private const string LocalSecret = "super-secret-jwt-token-with-at-least-32-characters-long";
	private const string LocalJwks = "http://gotrue:9999/.well-known/jwks.json";

	/// <summary>
	/// The whole point of validating at startup: no signing source otherwise surfaces as a 401 with no
	/// explanation, on the first request rather than on boot.
	/// </summary>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void A_signing_source_is_required(bool isDevelopment)
	{
		var options = new AuthOptions { JwksUrl = null, SharedSecret = null };

		Assert.NotNull(options.Validate(isDevelopment));
	}

	/// <summary>Whitespace is not a configured value, however much it looks like one.</summary>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Whitespace_does_not_count_as_a_signing_source(bool isDevelopment)
	{
		var options = new AuthOptions { JwksUrl = "   ", SharedSecret = "  " };

		Assert.NotNull(options.Validate(isDevelopment));
	}

	/// <summary>
	/// Nothing is configured by default, deliberately. An address that fell back to localhost would let
	/// a mistyped <c>Auth__JwksUrl</c> start cleanly and then fail against the wrong host, so a bare
	/// configuration has to be rejected rather than quietly usable.
	/// </summary>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void A_bare_configuration_has_no_signing_source(bool isDevelopment)
	{
		Assert.NotNull(new AuthOptions().Validate(isDevelopment));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void A_jwks_url_alone_is_valid(bool isDevelopment)
	{
		Assert.Null(new AuthOptions { JwksUrl = LocalJwks, SharedSecret = null }.Validate(isDevelopment));
	}

	/// <summary>
	/// The secret exists for a local identity provider that has not been given asymmetric keys.
	/// </summary>
	[Fact]
	public void A_shared_secret_alone_is_valid_in_development()
	{
		Assert.Null(new AuthOptions { JwksUrl = null, SharedSecret = LocalSecret }.Validate(isDevelopment: true));
	}

	/// <summary>
	/// Rejected rather than ignored: the secret takes precedence over the keys, so tolerating it would
	/// downgrade token validation to HS256 without saying so.
	/// </summary>
	[Fact]
	public void A_shared_secret_is_rejected_outside_development()
	{
		Assert.NotNull(new AuthOptions { JwksUrl = null, SharedSecret = LocalSecret }.Validate(isDevelopment: false));
	}

	/// <summary>
	/// Both together is a deliberate act now that neither carries a default, and in Development the
	/// secret is what wins.
	/// </summary>
	[Fact]
	public void Both_together_are_allowed_in_development_because_the_secret_takes_precedence()
	{
		Assert.Null(new AuthOptions { JwksUrl = LocalJwks, SharedSecret = LocalSecret }.Validate(isDevelopment: true));
	}

	/// <summary>
	/// The case the rule exists for: a working JWKS is present, so the secret is not needed, and letting
	/// it win would be a silent downgrade of an otherwise sound deployment.
	/// </summary>
	[Fact]
	public void Both_together_are_rejected_outside_development()
	{
		Assert.NotNull(new AuthOptions { JwksUrl = LocalJwks, SharedSecret = LocalSecret }.Validate(isDevelopment: false));
	}
}
