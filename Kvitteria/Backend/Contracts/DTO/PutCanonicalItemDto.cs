namespace Kvitteria.Contracts.DTO;

public record PutCanonicalItemDto(Guid? CanonicalItemId, bool CreateOrUpdateAlias);