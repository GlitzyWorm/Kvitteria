namespace Kvitteria.Contracts.Shared;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
