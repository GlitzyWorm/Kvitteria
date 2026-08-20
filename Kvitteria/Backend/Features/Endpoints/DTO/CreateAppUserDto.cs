namespace Backend.Features.Endpoints.DTO;

/// <summary>
/// Pre-authorizes an email. Nothing is sent anywhere: the person signs up through Supabase themselves,
/// and this row is what turns that anonymous signup into a household member.
/// </summary>
public record CreateAppUserDto(string Email, string? DisplayName, string? Role);
