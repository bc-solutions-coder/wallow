namespace Wallow.Identity.Api.Contracts.Responses;

public record InvitationResponse(
    Guid Id,
    string Email,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    Guid? AcceptedByUserId);
