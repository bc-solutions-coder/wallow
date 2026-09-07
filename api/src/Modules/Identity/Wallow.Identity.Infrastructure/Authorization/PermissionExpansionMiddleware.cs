using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Authorization;

public class PermissionExpansionMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Request item populated by TenantResolutionMiddleware after tenant resolution.
    /// </summary>
    private const string ResolvedTenantItemKey = "TenantId";

    /// <summary>
    /// Permission-map role used for the global-administrator claim, independent of tenant role claims.
    /// </summary>
    private const string AdminRole = "admin";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            ClaimsIdentity? identity = context.User.Identity as ClaimsIdentity;


            string? clientId = context.User.GetClientId();

            if (clientId?.StartsWith("sa-", StringComparison.Ordinal) == true
                    || clientId?.StartsWith("app-", StringComparison.Ordinal) == true)
            {

                ExpandServiceAccountScopes(context, identity);
            }
            else if (context.User.GetAuthMethod() == "api_key")
            {

                ExpandServiceAccountScopes(context, identity);
            }
            else
            {

                ExpandUserRoles(context, identity);
                ExpandUserScopes(context, identity);
            }

            ExpandGlobalAdmin(context, identity);
        }

        await next(context);
    }

    private static void ExpandUserRoles(HttpContext context, ClaimsIdentity? identity)
    {
        // Tenant roles grant no permissions when the request selects another organization.
        if (IsCrossTenantRequest(context))
        {
            return;
        }


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
    /// Adds permissions from user scopes within the token organization.
    /// </summary>
    private static void ExpandUserScopes(HttpContext context, ClaimsIdentity? identity)
    {
        // Apply the same tenant boundary to scopes as to roles.
        if (IsCrossTenantRequest(context))
        {
            return;
        }


        HashSet<string> existingPermissions = new(context.User.GetPermissions(), StringComparer.Ordinal);


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
    /// Adds the administrative permission set for a global administrator.
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
    /// Treats missing token organizations or differing resolved organizations as cross-tenant.
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
        // Machine credentials also remain scoped to their token organization.
        if (IsCrossTenantRequest(context))
        {
            return;
        }


        List<string> scopes = context.User.GetScopes().ToList();


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
