namespace Kvitteria.Contracts.DTO;

public record ReceiptAcceptanceDto(
	decimal Difference,
	Guid? MemberId,
	string? MemberDisplayName,
	DateTime AcceptedAt,
	string? Note
);
