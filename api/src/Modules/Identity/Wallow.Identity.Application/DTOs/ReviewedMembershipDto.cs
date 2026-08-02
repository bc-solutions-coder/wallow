using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// One membership that is neither active nor waiting to be reviewed: a suspension or a standing
/// denial. Carries the status alongside the identity because one row shape serves both listings,
/// and the moment the status was set, because "suspended in March" and "suspended an hour ago" are
/// different decisions to look at.
/// </summary>
public record ReviewedMembershipDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    MembershipStatus Status,
    DateTimeOffset? StatusChangedAt);
