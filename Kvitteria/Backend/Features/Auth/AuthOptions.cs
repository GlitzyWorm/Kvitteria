using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Auth;

public sealed class AuthOptions : IOptionsSection
{
	public static string SectionName => "Auth";

	/// <summary>
	/// Must equal the <c>iss</c> claim Supabase stamps on the token, which is the URL the *client*
	/// reached Auth on — not the one this process uses to fetch keys. See <see cref="JwksUrl"/>.
	/// </summary>
	[Required]
	public string Issuer { get; set; } = "http://127.0.0.1:54321/auth/v1";

	/// <summary>Supabase issues access tokens with the audience <c>authenticated</c>.</summary>
	[Required]
	public string Audience { get; set; } = "authenticated";

	/// <summary>
	/// Where to fetch signing keys. Defaults to the local stack for a host-side run; compose overrides it,
	/// because the container reaches the same service by another name.
	/// Kept separate from <see cref="Issuer"/> because the backend runs in
	/// Docker and reaches the host stack as <c>host.docker.internal</c>, while the token was issued to a
	/// client that saw <c>127.0.0.1</c>. Conflating the two presents as an unexplained 401.
	/// </summary>
	public string? JwksUrl { get; set; } = "http://127.0.0.1:54321/auth/v1/.well-known/jwks.json";

	/// <summary>
	/// The legacy symmetric signing secret, which <b>takes precedence over</b> <see cref="JwksUrl"/> when
	/// set. Only needed because a local Supabase stack signs with it unless <c>signing_keys_path</c> is
	/// configured; prefer the JWKS otherwise. Precedence rather than mutual exclusion, because
	/// <see cref="JwksUrl"/> has a default — so "both set" is the normal state for anyone choosing the
	/// secret, not a mistake worth failing startup over.
	/// </summary>
	public string? SharedSecret { get; set; }

	/// <summary>
	/// Data annotations cannot express "at least one of these two". Returns the reason it is unusable, or
	/// null when it is fine.
	/// </summary>
	public string? Validate()
	{
		if (string.IsNullOrWhiteSpace(JwksUrl) && string.IsNullOrWhiteSpace(SharedSecret))
			return $"{SectionName}: one of JwksUrl (preferred) or SharedSecret is required.";

		return null;
	}
}
