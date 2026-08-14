namespace Backend.Features.Endpoints.ReceiptEndpoints.DTO;

public record PutCanonicalItemDto(Guid? CanonicalItemId, bool CreateOrUpdateAlias);