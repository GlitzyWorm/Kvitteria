namespace Kvitteria.Contracts.DTO;

/// <summary>Omitted fields are left alone. Status is how a member is revoked; rows are not deleted.</summary>
public record PatchAppUserDto(string? DisplayName, string? Role, string? Status);
