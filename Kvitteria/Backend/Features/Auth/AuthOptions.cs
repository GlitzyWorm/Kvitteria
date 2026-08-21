using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Auth;

public sealed class AuthOptions : IOptionsSection
{
	public static string SectionName => "Auth";

	/// <summary>
	/// Must equal the <c>iss</c> claim GoTrue stamps on the token, which is the public URL a *client*
	/// reached auth on — not the one this process uses to fetch keys. See <see cref="JwksUrl"/>.
	/// No default: an address that silently falls back to localhost is a 401 nobody can explain.
	/// </summary>
	[Required(AllowEmptyStrings = false)]
	public string Issuer { get; set; } = string.Empty;

	/// <summary>
	/// GoTrue issues access tokens with the audience <c>authenticated</c>. Unlike the addresses here,
	/// this is a constant of the identity provider rather than a deployment detail, so it keeps a
	/// default.
	/// </summary>
	[Required(AllowEmptyStrings = false)]
	public string Audience { get; set; } = "authenticated";

	/// <summary>
	/// Where to fetch signing keys. Kept separate from <see cref="Issuer"/> because the token was
	/// issued to a client that reached auth through the reverse proxy, while this process fetches keys
	/// by the identity provider's own name on the container network. Conflating the two presents as an
	/// unexplained 401.
	/// </summary>
	public string? JwksUrl { get; set; }

	/// <summary>
	/// The legacy symmetric signing secret, accepted <b>only in Development</b>, where it takes
	/// precedence over <see cref="JwksUrl"/>. Anywhere else it is rejected outright: honouring it
	/// would downgrade a deployment that already has working asymmetric keys to HS256, and do so
	/// silently.
	/// </summary>
	public string? SharedSecret { get; set; }

	/// <summary>
	/// Permits fetching <see cref="JwksUrl"/> over plain HTTP outside Development. The shipped compose
	/// stack needs this, because the key fetch is a hop between containers on a private network and
	/// there is no certificate to present there. It is an explicit opt-out rather than an inference
	/// from the environment name, so a deployment that really is exposing its JWKS in the clear says
	/// so in one auditable place.
	/// </summary>
	public bool AllowInsecureJwksTransport { get; set; }

	/// <summary>
	/// The rules data annotations cannot express. Returns the reason the configuration is unusable, or
	/// null when it is fine. Takes the environment rather than reading it, so the rules stay a pure
	/// function of their inputs.
	/// </summary>
	public string? Validate(bool isDevelopment)
	{
		if (!isDevelopment && !string.IsNullOrWhiteSpace(SharedSecret))
			return $"{SectionName}: SharedSecret is a Development-only affordance. Outside Development it would take precedence over JwksUrl and downgrade token validation to HS256; remove it.";

		if (string.IsNullOrWhiteSpace(JwksUrl) && string.IsNullOrWhiteSpace(SharedSecret))
			return $"{SectionName}: one of JwksUrl (preferred) or SharedSecret is required.";

		return null;
	}
}
