namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// The tenant an OIDC client belongs to, used by the auth frontend to brand the login screen
/// before any user is authenticated.
/// </summary>
public sealed record ClientTenantResponse(Guid TenantId, string? OrgName);
