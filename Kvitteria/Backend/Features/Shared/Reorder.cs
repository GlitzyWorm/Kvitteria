namespace Backend.Features.Shared;

public static class Reorder
{
	/// <summary>
	/// Assigns each id's index as its new sort order, requiring the supplied ids to be exactly the
	/// existing set.
	/// </summary>
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
		HashSet<Guid> requested = orderedIds.ToHashSet();

		// Equal counts plus one-directional containment would accept a duplicated id, so
		// distinctness is checked as well.
		if (requested.Count != orderedIds.Count
		    || requested.Count != byId.Count
		    || !requested.All(byId.ContainsKey))
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
