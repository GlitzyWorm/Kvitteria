namespace Backend.Features.Shared;

public static class Reorder
{
	/// <summary>
	/// Assigns each id's index as its new sort order, requiring the supplied ids to be exactly the
	/// existing set.
	/// </summary>
	/// <remarks>
	/// Existed twice — once for categories, once for meals — with the same O(n^2) First-inside-a-loop
	/// and the same missing guards in both copies.
	/// </remarks>
	public static bool TryApply<T>(
		IReadOnlyList<T> existing,
		IReadOnlyList<Guid>? orderedIds,
		Func<T, Guid> idOf,
		Action<T, int> setSortOrder,
		out IResult problem)
	{
		if (orderedIds == null || orderedIds.Count == 0)
		{
			problem = Results.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				detail: "OrderedIds is required and must not be empty.");
			return false;
		}

		Dictionary<Guid, T> byId = existing.ToDictionary(idOf);

		if (byId.Count != orderedIds.Count || !orderedIds.All(byId.ContainsKey))
		{
			problem = Results.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				detail: "OrderedIds must contain exactly the ids being reordered, with no duplicates.");
			return false;
		}

		for (var index = 0; index < orderedIds.Count; index++)
			setSortOrder(byId[orderedIds[index]], index);

		problem = Results.Empty;
		return true;
	}
}
