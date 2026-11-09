namespace Kvitteria.Contracts.DTO;

/// <summary>The difference the member saw, so a figure changed since is refused rather than accepted unseen.</summary>
public record AcceptDifferenceDto(
	decimal Difference,
	string? Note
);
