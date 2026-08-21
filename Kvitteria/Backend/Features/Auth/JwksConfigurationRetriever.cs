using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
namespace Backend.Features.Auth;

/// <summary>
/// Fetches signing keys from a bare JWKS endpoint. GoTrue serves the key set directly rather than
/// behind an OpenID discovery document, which is what the built-in retriever expects. Wrapping the keys
/// in an <see cref="OpenIdConnectConfiguration"/> lets <see cref="ConfigurationManager{T}"/> keep doing
/// the caching and the refresh-on-failure that makes key rotation a non-event.
/// </summary>
internal sealed class JwksConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
{
	public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
	{
		string document = await retriever.GetDocumentAsync(address, cancel);

		var configuration = new OpenIdConnectConfiguration();

		// GetSigningKeys returns concrete keys (ECDsaSecurityKey, RsaSecurityKey), not the JsonWebKey it
		// parsed them from, so this must not filter by type.
		foreach (SecurityKey key in JsonWebKeySet.Create(document).GetSigningKeys())
			configuration.SigningKeys.Add(key);

		return configuration;
	}
}
