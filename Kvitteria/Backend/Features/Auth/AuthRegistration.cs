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
		/// Wires bearer validation against Supabase Auth plus the household's own authorization. Supabase
		/// owns identity — this process never sees a password — while roles and permissions live here, in
		/// tables with no foreign key to <c>auth.users</c>, so auth can move to a hosted project later
		/// without a migration.
		/// </summary>
		public IServiceCollection AddHouseholdAuth(IConfiguration configuration)
		{
			services.AddOptionsSection<AuthOptions>(configuration);
			services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();

			// Read once, directly: the bearer scheme is built at startup and reconfiguring it on a
			// reloadOnChange edit is not something the framework supports anyway.
			AuthOptions auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

			services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
			        .AddJwtBearer(options => ConfigureBearer(options, auth));

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

	private static void ConfigureBearer(JwtBearerOptions options, AuthOptions auth)
	{
		// The local stack speaks plain HTTP, and there is no metadata document to fetch besides the JWKS.
		options.RequireHttpsMetadata = false;

		// Off, so claims keep the names Supabase issued them under ("sub", "email") instead of being
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

		if (!string.IsNullOrWhiteSpace(auth.SharedSecret))
		{
			options.TokenValidationParameters.IssuerSigningKey =
				new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SharedSecret));
			return;
		}

		options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
			auth.JwksUrl!,
			new JwksConfigurationRetriever(),
			new HttpDocumentRetriever { RequireHttps = false });
	}
}

/// <summary>
/// Reports the "exactly one signing source" rule that data annotations cannot express, at startup rather
/// than on the first 401 nobody can explain.
/// </summary>
internal sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
	public ValidateOptionsResult Validate(string? name, AuthOptions options)
	{
		string? failure = options.Validate();

		return failure == null
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(failure);
	}
}
