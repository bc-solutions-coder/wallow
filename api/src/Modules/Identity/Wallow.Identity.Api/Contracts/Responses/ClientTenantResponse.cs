namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Organization binding resolved for an OIDC client before authentication.
/// </summary>
public sealed record ClientTenantResponse(Guid TenantId, string? OrgName);
