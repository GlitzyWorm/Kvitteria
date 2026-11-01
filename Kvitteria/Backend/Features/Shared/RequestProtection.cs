using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using IPNetwork = System.Net.IPNetwork;

namespace Backend.Features.Shared;

/// <summary>
/// Who a request is from and how often they may ask. The entrypoint states the client address once, in
/// <c>X-Real-IP</c>; the backend believes that header only from a private-network peer, and the
/// <c>/api</c> group is rate limited per address.
/// </summary>
public static class RequestProtection
{
	public const string PolicyName = "api";
	public const string ClientAddressHeader = "X-Real-IP";

	public const int PermitLimit = 300;
	public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
	public const int SegmentsPerWindow = 6;

	/// <summary>
	/// Every caller whose address is unknown shares one window rather than escaping the limit.
	/// </summary>
	public const string UnknownAddressKey = "unknown";

	/// <summary>
	/// The compose subnet is assigned by Docker and cannot be named statically, and pinning one risks
	/// colliding with the host's own network. These ranges are safe because the backend publishes no port
	/// outside the development override, so only peers on the compose network can reach it.
	/// </summary>
	public static readonly IReadOnlyList<IPNetwork> PrivateNetworks = [
		IPNetwork.Parse("10.0.0.0/8"),
		IPNetwork.Parse("172.16.0.0/12"),
		IPNetwork.Parse("192.168.0.0/16"),
		IPNetwork.Parse("fc00::/7")
	];

	/// <summary>
	/// The key one client's requests are counted under. An IPv4 address that arrived mapped into IPv6 is
	/// the same client as its plain form, so the two cannot be used to get a second window.
	/// </summary>
	public static string PartitionKey(IPAddress? address)
	{
		if (address is null)
			return UnknownAddressKey;

		return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
	}

	extension(IServiceCollection services)
	{
		public IServiceCollection AddRequestProtection()
		{
			services.Configure<ForwardedHeadersOptions>(options => {
				options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
				options.ForwardedForHeaderName = ClientAddressHeader;
				options.ForwardLimit = 1;

				// Replaces the defaults, which trust loopback only.
				options.KnownProxies.Clear();
				options.KnownIPNetworks.Clear();
				foreach (IPNetwork network in PrivateNetworks)
					options.KnownIPNetworks.Add(network);
			});

			services.AddRateLimiter(options => {
				// The limiter's own default is 503.
				options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

				options.AddPolicy(PolicyName, httpContext => RateLimitPartition.GetSlidingWindowLimiter(
					PartitionKey(httpContext.Connection.RemoteIpAddress),
					_ => new SlidingWindowRateLimiterOptions {
						PermitLimit = PermitLimit,
						Window = Window,
						SegmentsPerWindow = SegmentsPerWindow,
						QueueLimit = 0
					}));

				options.OnRejected = async (context, ct) => {
					HttpContext httpContext = context.HttpContext;
					httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

					// A sliding window reports no retry time of its own, and a permit is free again within one
					// window at the latest, so that is the honest upper bound.
					if (!context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
						retryAfter = Window;
					httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

					IProblemDetailsService problems = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
					await problems.TryWriteAsync(new ProblemDetailsContext {
						HttpContext = httpContext,
						ProblemDetails = {
							Status = StatusCodes.Status429TooManyRequests,
							Detail = "Too many requests. Wait a moment and try again."
						}
					});
				};
			});

			return services;
		}
	}
}
