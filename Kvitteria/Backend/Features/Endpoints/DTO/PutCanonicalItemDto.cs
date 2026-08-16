namespace Backend.Features.Endpoints.DTO;

public record PutCanonicalItemDto(Guid? CanonicalItemId, bool CreateOrUpdateAlias);