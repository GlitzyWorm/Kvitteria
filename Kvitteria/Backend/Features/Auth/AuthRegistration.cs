using System.Text;
using Backend.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
namespace Backend.Features.Auth;

public static class AuthRegistration
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Wires bearer validation against GoTrue plus the household's own authorization. GoTrue owns
		/// identity — this process never sees a password — while roles and permissions live here, in
		/// tables with no foreign key to <c>auth.users</c>, so identity can relocate to a hosted project
		/// later without a migration.
		/// </summary>
		public IServiceCollection AddHouseholdAuth(IConfiguration configuration, IHostEnvironment environment)
		{
			services.AddOptionsSection<AuthOptions>(configuration);
			services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();

			// Read once, directly: the bearer scheme is built at startup and reconfiguring it on a
			// reloadOnChange edit is not something the framework supports anyway. This instance can
			// carry an unset Issuer or JwksUrl, since neither has a default any more — which is safe
			// only because the options registration above validates them on start, while the
			// ConfigurationManager built below is not constructed until the first authentication.
			// Startup aborts first. The null-coalescing keeps a missing section from raising an NRE
			// that would mask that well-worded failure.
			AuthOptions auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
			bool isDevelopment = environment.IsDevelopment();

			services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
			        .AddJwtBearer(options => ConfigureBearer(options, auth, isDevelopment));

			services.AddAuthorization(options => options.AddHouseholdPolicies());

			services.AddHttpContextAccessor();
			services.AddMemoryCache();
			services.AddSingleton<PermissionCache>();
			services.AddScoped<CurrentUserService>();

			// Scoped, not singleton: it resolves the request's member through a scoped DbContext.
			services.AddScoped<IAuthorizationHandler, HouseholdAuthorizationHandler>();

			return services;
		}
	}

	private static void ConfigureBearer(JwtBearerOptions options, AuthOptions auth, bool isDevelopment)
	{
		// Whoever can answer the key fetch decides which tokens are valid, so plaintext is a
		// token-forgery vector and HTTPS is the default. Two cases relax it: Development, and a
		// deployment that has declared its key fetch to be a hop on a private network.
		bool allowPlaintextKeyFetch = isDevelopment || auth.AllowInsecureJwksTransport;
		options.RequireHttpsMetadata = !allowPlaintextKeyFetch;

		// Off, so claims keep the names GoTrue issued them under ("sub", "email") instead of being
		// rewritten to the legacy WS-Federation URIs.
		options.MapInboundClaims = false;

		options.TokenValidationParameters = new TokenValidationParameters {
			ValidateIssuer = true,
			ValidIssuer = auth.Issuer,
			ValidateAudience = true,
			ValidAudience = auth.Audience,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true
		};

		// Guarded on the environment as well as on the value: startup validation has already rejected a
		// secret outside Development, and stating the invariant here means the branch cannot be reached
		// by a future caller that skips that validation.
		if (isDevelopment && !string.IsNullOrWhiteSpace(auth.SharedSecret))
		{
			options.TokenValidationParameters.IssuerSigningKey =
				new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SharedSecret));
			return;
		}

		options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
			auth.JwksUrl!,
			new JwksConfigurationRetriever(),
			new HttpDocumentRetriever { RequireHttps = !allowPlaintextKeyFetch });
	}
}

/// <summary>
/// Reports the signing-source rules that data annotations cannot express, at startup rather than on the
/// first 401 nobody can explain.
/// </summary>
internal sealed class AuthOptionsValidator(IHostEnvironment environment) : IValidateOptions<AuthOptions>
{
	public ValidateOptionsResult Validate(string? name, AuthOptions options)
	{
		string? failure = options.Validate(environment.IsDevelopment());

		return failure == null
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(failure);
	}
}
