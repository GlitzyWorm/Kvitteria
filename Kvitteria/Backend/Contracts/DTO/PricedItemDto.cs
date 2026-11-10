namespace Kvitteria.Contracts.DTO;

/// <summary>
/// A canonical item with at least one priced purchase, and how many it has. <paramref name="Names"/> are the
/// raw names its priced lines were printed under, most purchased first.
/// </summary>
public record PricedItemDto(Guid Id, string DisplayName, int PurchaseCount, List<PricedNameDto> Names);

/// <summary>
/// One raw name of an item. <paramref name="Key"/> is the normalized form to send back as <c>name</c>;
/// <paramref name="Label"/> is the spelling most of its lines carry.
/// </summary>
public record PricedNameDto(string Key, string Label, int PurchaseCount);
