namespace Backend.Features.Shared;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
	public const int MaxPageSize = 100;

	/// <summary>Validates paging parameters, yielding a problem result when they are out of range.</summary>
	public static bool TryValidate(int page, int pageSize, out IResult problem)
	{
		if (page >= 1 && pageSize >= 1 && pageSize <= MaxPageSize)
		{
			problem = Results.Empty;
			return true;
		}

		problem = Results.Problem(
			statusCode: StatusCodes.Status400BadRequest,
			detail: $"page must be >= 1 and pageSize must be between 1 and {MaxPageSize}");

		return false;
	}
}
