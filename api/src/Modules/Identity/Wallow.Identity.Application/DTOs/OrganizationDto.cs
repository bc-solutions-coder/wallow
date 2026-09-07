namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// Organization details, including the platform suspension reason visible to its admins.
/// </summary>
public record OrganizationDto(
    Guid Id,
    string Name,
    string? Domain,
    int MemberCount,
    DateTimeOffset? PlatformSuspendedAt = null,
    string? PlatformSuspensionReason = null);
