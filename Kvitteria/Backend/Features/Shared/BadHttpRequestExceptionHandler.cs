using Microsoft.AspNetCore.Diagnostics;
namespace Backend.Features.Shared;

/// <summary>
/// Maps the framework's parameter-binding failures to the status code they already carry, so a
/// malformed query-string value is reported as a 400 rather than a 500.
/// </summary>
internal sealed class BadHttpRequestExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		if (exception is not BadHttpRequestException badRequest)
			return false;

		httpContext.Response.StatusCode = badRequest.StatusCode;

		return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext {
			HttpContext = httpContext,
			ProblemDetails = {
				Status = badRequest.StatusCode,
				Detail = badRequest.Message
			}
		});
	}
}
