namespace Backend.Features.Shared;

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount);
