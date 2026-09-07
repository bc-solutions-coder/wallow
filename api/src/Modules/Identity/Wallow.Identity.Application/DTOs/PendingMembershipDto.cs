namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// Requester identity and request time for membership review.
/// </summary>
public record PendingMembershipDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset? RequestedAt);
