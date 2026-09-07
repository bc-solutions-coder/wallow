using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// Suspended or denied membership with identity, status, and status-change time.
/// </summary>
public record ReviewedMembershipDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    MembershipStatus Status,
    DateTimeOffset? StatusChangedAt);
