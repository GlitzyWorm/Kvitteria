namespace Backend.Features.Endpoints.DTO;

public record CreateCategoryDto(
	string DisplayName,
	Guid? ParentCategoryId
);