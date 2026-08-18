namespace Backend.Features.Endpoints.DTO;

public record UpdateCategoryDto(
	string? DisplayName,
	Guid? ParentCategoryId,
	bool ClearParent = false
);
