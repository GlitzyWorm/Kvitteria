namespace Kvitteria.Contracts.DTO;

/// <summary>A canonical item with at least one priced purchase, and how many it has.</summary>
public record PricedItemDto(Guid Id, string DisplayName, int PurchaseCount);
