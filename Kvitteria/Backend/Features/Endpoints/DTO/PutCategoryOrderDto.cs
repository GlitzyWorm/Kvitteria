namespace Backend.Features.Endpoints.DTO;

public record PutCategoryOrderDto(Guid? ParentCategoryId, List<Guid> OrderedIds);