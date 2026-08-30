namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// The platform-suspension pair is present so the owning organization's admins can read the
/// operator's reason; only a global admin can place or lift the suspension itself.
/// </summary>
public record OrganizationDto(
    Guid Id,
    string Name,
    string? Domain,
    int MemberCount,
    DateTimeOffset? PlatformSuspendedAt = null,
    string? PlatformSuspensionReason = null);
