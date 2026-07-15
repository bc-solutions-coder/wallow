using System.Security.Claims;

namespace Wallow.Shared.Kernel.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Resolves the user identifier from NameIdentifier or the OIDC "sub" claim.
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? principal?.FindFirst("sub")?.Value;

    /// <summary>
    /// Resolves the client identifier from "client_id" or "azp" (authorized party) claim.
    /// </summary>
    public static string? GetClientId(this ClaimsPrincipal? principal) =>
        principal?.FindFirst("client_id")?.Value
        ?? principal?.FindFirst("azp")?.Value;

    /// <summary>
    /// Resolves the tenant/organization identifier from the "org_id" claim.
    /// </summary>
    public static string? GetTenantId(this ClaimsPrincipal? principal) =>
        principal?.FindFirst("org_id")?.Value;

    /// <summary>
    /// Resolves the tenant/organization name from the "org_name" claim.
    /// </summary>
    public static string? GetTenantName(this ClaimsPrincipal? principal) =>
        principal?.FindFirst("org_name")?.Value;

    /// <summary>
    /// Resolves the user's email from the standard email claim or OIDC "email" claim.
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimTypes.Email)?.Value
        ?? principal?.FindFirst("email")?.Value;

    /// <summary>
    /// Resolves the user's display name from "name", ClaimTypes.Name, or "preferred_username".
    /// </summary>
    public static string? GetDisplayName(this ClaimsPrincipal? principal) =>
        principal?.FindFirst("name")?.Value
        ?? principal?.FindFirst(ClaimTypes.Name)?.Value
        ?? principal?.FindFirst("preferred_username")?.Value;

    /// <summary>
    /// Resolves the authentication method from the "auth_method" claim.
    /// </summary>
    public static string? GetAuthMethod(this ClaimsPrincipal? principal) =>
        principal?.FindFirst("auth_method")?.Value;

    /// <summary>
    /// Resolves the tenant region from the "tenant_region" claim.
    /// </summary>
    public static string? GetTenantRegion(this ClaimsPrincipal? principal) =>
        principal?.FindFirst("tenant_region")?.Value;

    /// <summary>
    /// Resolves the user's first name from the GivenName claim.
    /// </summary>
    public static string? GetFirstName(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimTypes.GivenName)?.Value;

    /// <summary>
    /// Resolves the user's last name from the Surname claim.
    /// </summary>
    public static string? GetLastName(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimTypes.Surname)?.Value;

    /// <summary>
    /// Resolves the user's subscription plan from the "plan" claim.
    /// </summary>
    public static string? GetPlan(this ClaimsPrincipal? principal) =>
        principal?.FindFirst("plan")?.Value;

    /// <summary>
    /// Returns all roles from both ClaimTypes.Role and OIDC "role" claims, deduplicated.
    /// </summary>
    public static IReadOnlyList<string> GetRoles(this ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return [];
        }

        return principal.FindAll(ClaimTypes.Role)
            .Concat(principal.FindAll("role"))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns all permission claims.
    /// </summary>
    public static IReadOnlyList<string> GetPermissions(this ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return [];
        }

        return principal.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
    }

    /// <summary>
    /// Returns all OAuth2 scopes from "scope" (space-separated) and "oi_scp" (OpenIddict) claims, deduplicated.
    /// </summary>
    public static IReadOnlyList<string> GetScopes(this ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return [];
        }

        return principal.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Concat(principal.FindAll("oi_scp").Select(c => c.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
