using System.Security.Claims;
using Wallow.Identity.Application.Constants;
using Microsoft.AspNetCore.Http;

namespace Wallow.Identity.Infrastructure.Authorization;

public class PermissionExpansionMiddleware(RequestDelegate next)
{

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            ClaimsIdentity? identity = context.User.Identity as ClaimsIdentity;

            // Check if this is a service account request
            string? clientId = context.User.FindFirst("azp")?.Value;
            if (clientId?.StartsWith("sa-", StringComparison.Ordinal) == true
                    || clientId?.StartsWith("app-", StringComparison.Ordinal) == true)
            {
                // Service account: map OAuth2 scopes to permissions
                ExpandServiceAccountScopes(context, identity);
            }
            else if (context.User.FindFirst("auth_method")?.Value == "api_key")
            {
                // API key: map scopes to permissions
                ExpandServiceAccountScopes(context, identity);
            }
            else
            {
                // Regular user: expand roles to permissions
                ExpandUserRoles(context, identity);
            }
        }

        await next(context);
    }

    private static void ExpandUserRoles(HttpContext context, ClaimsIdentity? identity)
    {
        List<string> roles = [];

        // Read standard role claims
        List<string> standardRoles = context.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        roles.AddRange(standardRoles);

        // Expand roles to permissions
        if (roles.Count > 0)
        {
            IEnumerable<string> permissions = RolePermissionMapping.GetPermissions(roles);

            foreach (string permission in permissions)
            {
                identity?.AddClaim(new Claim("permission", permission));
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
