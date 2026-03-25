using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Authorization;

public class PermissionExpansionMiddleware(RequestDelegate next)
{

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            ClaimsIdentity? identity = context.User.Identity as ClaimsIdentity;

            // Check if this is a service account or API key request.
            // OpenIddict uses "azp" for server-issued tokens, "client_id" for validated principals.
            string? clientId = context.User.FindFirst("azp")?.Value
                ?? context.User.FindFirst("client_id")?.Value;

            if (clientId?.StartsWith("sa-", StringComparison.Ordinal) == true
                    || clientId?.StartsWith("app-", StringComparison.Ordinal) == true)
            {
                // Service account / developer app: map OAuth2 scopes to permissions
                ExpandServiceAccountScopes(context, identity);
            }
            else if (context.User.FindFirst("auth_method")?.Value == "api_key")
            {
                // API key: map scopes to permissions
                ExpandServiceAccountScopes(context, identity);
            }
            else
            {
                // Regular user: expand roles to permissions, then supplement with granted scopes.
                // Role expansion provides the baseline permissions for the user's role tier.
                // Scope expansion ensures users can also access resources matching their
                // granted OAuth2 scopes (covers cases where role claims are absent from the token).
                ExpandUserRoles(context, identity);
                ExpandUserScopes(context, identity);
            }
        }

        await next(context);
    }

    private static void ExpandUserRoles(HttpContext context, ClaimsIdentity? identity)
    {
        // Read role claims from both standard and OIDC claim types
        List<string> roles = context.User.FindAll(ClaimTypes.Role)
            .Concat(context.User.FindAll("role"))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Count > 0)
        {
            IEnumerable<string> permissions = RolePermissionMapping.GetPermissions(roles);

            foreach (string permission in permissions)
            {
                identity?.AddClaim(new Claim("permission", permission));
            }
        }
    }

    /// <summary>
    /// For user tokens, also map granted OAuth2 scopes to permissions.
    /// This ensures users can access resources matching their granted scopes
    /// even if role claims are missing from the token.
    /// </summary>
    private static void ExpandUserScopes(HttpContext context, ClaimsIdentity? identity)
    {
        // Collect permissions already granted by role expansion to avoid duplicates
        HashSet<string> existingPermissions = new(
            context.User.FindAll("permission").Select(c => c.Value),
            StringComparer.Ordinal);

        // Extract scopes: "scope" (space-separated in JWT) + "oi_scp" (OpenIddict validated principal)
        List<string> scopes = context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Concat(context.User.FindAll("oi_scp").Select(c => c.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string scope in scopes)
        {
            string? permission = ScopePermissionMapper.MapScopeToPermission(scope);
            if (permission is not null && !existingPermissions.Contains(permission))
            {
                identity?.AddClaim(new Claim("permission", permission));
                existingPermissions.Add(permission);
            }
        }
    }

    private static void ExpandServiceAccountScopes(HttpContext context, ClaimsIdentity? identity)
    {
        // Extract scopes from token - can be space-separated in a single claim
        List<string> scopes = context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        // Map scopes to permissions
        foreach (string scope in scopes)
        {
            string? permission = ScopePermissionMapper.MapScopeToPermission(scope);
            if (permission is not null)
            {
                identity?.AddClaim(new Claim("permission", permission));
            }
        }
    }
}
