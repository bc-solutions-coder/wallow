namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// User and required role name to add to the organization.
/// </summary>
public record AddMemberRequest(Guid UserId, string Role);
