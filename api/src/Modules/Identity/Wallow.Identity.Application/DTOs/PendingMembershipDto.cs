namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// One outstanding access request. Carries the requester's identity because the reviewer's
/// question is "who is this person", and the moment they asked, because a request that has been
/// waiting a week reads differently from one that arrived a minute ago.
/// </summary>
public record PendingMembershipDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset? RequestedAt);
