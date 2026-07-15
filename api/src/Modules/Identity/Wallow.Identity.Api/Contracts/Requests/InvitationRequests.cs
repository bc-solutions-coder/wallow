namespace Wallow.Identity.Api.Contracts.Requests;

public record CreateInvitationRequest(string Email, DateTimeOffset? ExpiresAt = null);
