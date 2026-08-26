namespace Kvitteria.Contracts.DTO;

/// <summary>What every member may do. Admins are unaffected and always hold everything.</summary>
public record MemberPermissionsDto(List<string> Permissions, List<string> AvailablePermissions);

public record PutMemberPermissionsDto(List<string> Permissions);
