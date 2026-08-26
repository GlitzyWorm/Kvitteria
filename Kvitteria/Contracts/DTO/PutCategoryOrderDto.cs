namespace Kvitteria.Contracts.DTO;

public record PutCategoryOrderDto(Guid? ParentCategoryId, List<Guid> OrderedIds);