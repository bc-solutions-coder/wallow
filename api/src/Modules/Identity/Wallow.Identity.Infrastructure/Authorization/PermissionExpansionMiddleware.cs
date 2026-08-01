using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Authorization;

public class PermissionExpansionMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Key under which <c>TenantResolutionMiddleware</c> stamps the tenant the request was
    /// resolved onto. It differs from the caller's own org_id only on a cross-tenant override.
    /// </summary>
    private const string ResolvedTenantItemKey = "TenantId";

    /// <summary>
    /// The role whose permission set the global-admin claim confers in every tenant. Global
    /// admin is not backed by an assignable role, so its permissions are read from the map
    /// directly rather than from a role claim.
    /// </summary>
    private const string AdminRole = "admin";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            ClaimsIdentity? identity = context.User.Identity as ClaimsIdentity;

            // Check if this is a service account or API key request.
            // OpenIddict uses "azp" for server-issued tokens, "client_id" for validated principals.
            string? clientId = context.User.GetClientId();

            if (clientId?.StartsWith("sa-", StringComparison.Ordinal) == true
                    || clientId?.StartsWith("app-", StringComparison.Ordinal) == true)
            {
                // Service account / developer app: map OAuth2 scopes to permissions
                ExpandServiceAccountScopes(context, identity);
            }
            else if (context.User.GetAuthMethod() == "api_key")
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

            ExpandGlobalAdmin(context, identity);
        }

        await next(context);
    }

    private static void ExpandUserRoles(HttpContext context, ClaimsIdentity? identity)
    {
        // A role is granted by one tenant and carries no authority in another, so a request
        // resolved onto a different tenant gets nothing from it — including AdminAccess and
        // SystemSettings. The global-admin claim is the only cross-tenant grant.
        if (IsCrossTenantRequest(context))
        {
            return;
        }

        // Read role claims from both standard and OIDC claim types
        List<string> roles = context.User.GetRoles().ToList();

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
        // A scope is granted by one tenant just as a role is. Without this the scope path is
        // simply the way around the guard on the role path.
        if (IsCrossTenantRequest(context))
        {
            return;
        }

        // Collect permissions already granted by role expansion to avoid duplicates
        HashSet<string> existingPermissions = new(context.User.GetPermissions(), StringComparer.Ordinal);

        // Extract scopes: "scope" (space-separated in JWT) + "oi_scp" (OpenIddict validated principal)
        List<string> scopes = context.User.GetScopes().ToList();

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

    /// <summary>
    /// Grants the administrative permission set in whichever tenant the request resolved onto.
    /// The flag is seeded, never assignable through a tenant-facing endpoint, so it is the one
    /// way a human governs tenants other than their own.
    /// </summary>
    private static void ExpandGlobalAdmin(HttpContext context, ClaimsIdentity? identity)
    {
        if (!context.User.IsGlobalAdmin())
        {
            return;
        }

        HashSet<string> existingPermissions = new(context.User.GetPermissions(), StringComparer.Ordinal);

        foreach (string permission in RolePermissionMapping.GetPermissions([AdminRole]))
        {
            if (existingPermissions.Add(permission))
            {
                identity?.AddClaim(new Claim("permission", permission));
            }
        }
    }

    /// <summary>
    /// Whether this request is asking for a tenant the caller's own token does not name. A caller
    /// that names NO tenant counts as cross-tenant: a permission is always held somewhere, so a
    /// principal that states no organization holds none, and treating "no tenant" as "same tenant"
    /// would expand everything for a token that was scoped to nothing.
    /// </summary>
    private static bool IsCrossTenantRequest(HttpContext context)
    {
        string? ownTenantId = context.User.GetTenantId();
        if (string.IsNullOrEmpty(ownTenantId))
        {
            return true;
        }

        string? resolvedTenantId = context.Items.TryGetValue(ResolvedTenantItemKey, out object? resolved)
            ? resolved as string
            : null;

        return !string.IsNullOrEmpty(resolvedTenantId)
            && !string.Equals(ownTenantId, resolvedTenantId, StringComparison.OrdinalIgnoreCase);
    }

    private static void ExpandServiceAccountScopes(HttpContext context, ClaimsIdentity? identity)
    {
        // A service account and an API key are each issued against one organization, so they get
        // the same guard a human does. Without it a machine credential is the widest hole in the
        // model: it expands every scope it carries into every tenant it asks for.
        if (IsCrossTenantRequest(context))
        {
            return;
        }

        // Extract scopes from token - can be space-separated in a single claim
        List<string> scopes = context.User.GetScopes().ToList();

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
