namespace Kvitteria.Contracts.DTO;

public record ItemAliasDto(Guid Id, string Alias, CanonicalItemDto? CanonicalItem);