using Microsoft.AspNetCore.Diagnostics;
namespace Backend.Features.Shared;

/// <summary>
/// Maps the framework's parameter-binding failures to the status code they already carry.
/// <c>UseExceptionHandler</c> otherwise reports every exception as a 500, which would turn a
/// malformed query-string value — e.g. <c>?status=nonsense</c> — into a server error.
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
