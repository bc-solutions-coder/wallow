namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Role is the name of the role this organization grants the member ("admin", "manager",
/// "user"). It is required: roles are per (user, organization), so there is no default to fall
/// back on.
/// </summary>
public record AddMemberRequest(Guid UserId, string Role);
