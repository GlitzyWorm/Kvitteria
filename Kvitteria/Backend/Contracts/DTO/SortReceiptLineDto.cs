namespace Kvitteria.Contracts.DTO;

/// <summary>
/// Everything a person decides about one unassigned line. Names exactly one of
/// <paramref name="CanonicalItemId"/> and <paramref name="NewItemName"/>. Category and meal are the full
/// desired state, so null means empty.
/// </summary>
public record SortReceiptLineDto(
	Guid? CanonicalItemId,
	string? NewItemName,
	Guid? CategoryId,
	Guid? MealId,
	bool RememberAlias,
	bool SetAsDefaults
);
