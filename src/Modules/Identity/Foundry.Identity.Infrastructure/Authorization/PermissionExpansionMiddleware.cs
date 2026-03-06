using System.Security.Claims;
using System.Text.Json;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;

namespace Foundry.Identity.Infrastructure.Authorization;

public class PermissionExpansionMiddleware
{
    private readonly RequestDelegate _next;

    public PermissionExpansionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            ClaimsIdentity? identity = context.User.Identity as ClaimsIdentity;

            // Check if this is a service account request
            string? clientId = context.User.FindFirst("azp")?.Value;
            if (clientId?.StartsWith("sa-", StringComparison.Ordinal) == true)
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

        await _next(context);
    }

    private static void ExpandUserRoles(HttpContext context, ClaimsIdentity? identity)
    {
        List<string> roles = [];

        // Read standard role claims
        List<string> standardRoles = context.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        if (standardRoles.Count > 0)
        {
            roles.AddRange(standardRoles);
        }
        else
        {
            // Fallback: Check Keycloak-specific realm_access claim
            string? realmAccess = context.User.FindFirst("realm_access")?.Value;
            if (!string.IsNullOrEmpty(realmAccess))
            {
                try
                {
                    JsonElement parsed = JsonSerializer.Deserialize<JsonElement>(realmAccess);
                    if (parsed.TryGetProperty("roles", out JsonElement rolesArray))
                    {
                        roles.AddRange(rolesArray.EnumerateArray()
                            .Where(r => r.GetString() != null)
                            .Select(r => r.GetString()!));
                    }
                }
                catch (JsonException)
                {
                    // Invalid JSON in realm_access claim, skip
                }
            }
        }

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
            string? permission = MapScopeToPermission(scope);
            if (permission is not null)
            {
                identity?.AddClaim(new Claim("permission", permission));
            }
        }
    }

    private static string? MapScopeToPermission(string scope)
    {
        return scope switch
        {
            // Billing
            "billing.read" => PermissionType.BillingRead,
            "billing.manage" => PermissionType.BillingManage,
            "invoices.read" => PermissionType.InvoicesRead,
            "invoices.write" => PermissionType.InvoicesWrite,
            "payments.read" => PermissionType.PaymentsRead,
            "payments.write" => PermissionType.PaymentsWrite,
            "subscriptions.read" => PermissionType.SubscriptionsRead,
            "subscriptions.write" => PermissionType.SubscriptionsWrite,

            // Identity - Users
            "users.read" => PermissionType.UsersRead,
            "users.write" => PermissionType.UsersUpdate,
            "users.manage" => PermissionType.UsersDelete,

            // Identity - Roles
            "roles.read" => PermissionType.RolesRead,
            "roles.write" => PermissionType.RolesUpdate,
            "roles.manage" => PermissionType.RolesDelete,

            // Identity - Organizations
            "organizations.read" => PermissionType.OrganizationsRead,
            "organizations.write" => PermissionType.OrganizationsUpdate,
            "organizations.manage" => PermissionType.OrganizationsManageMembers,

            // Identity - API Keys
            "apikeys.read" => PermissionType.ApiKeysRead,
            "apikeys.write" => PermissionType.ApiKeysUpdate,
            "apikeys.manage" => PermissionType.ApiKeyManage,

            // Identity - SSO/SCIM
            "sso.read" => PermissionType.SsoRead,
            "sso.manage" => PermissionType.SsoManage,
            "scim.manage" => PermissionType.ScimManage,

            // Storage
            "storage.read" => PermissionType.StorageRead,
            "storage.write" => PermissionType.StorageWrite,

            // Communications
            "messaging.access" => PermissionType.MessagingAccess,
            "announcements.read" => PermissionType.AnnouncementRead,
            "announcements.manage" => PermissionType.AnnouncementManage,
            "changelog.manage" => PermissionType.ChangelogManage,
            "notifications.read" => PermissionType.NotificationsRead,
            "notifications.write" => PermissionType.NotificationsWrite,

            // Configuration
            "configuration.read" => PermissionType.ConfigurationRead,
            "configuration.manage" => PermissionType.ConfigurationManage,

            // Showcases
            "showcases.read" => PermissionType.ShowcasesRead,
            "showcases.manage" => PermissionType.ShowcasesManage,

            // Platform
            "webhooks.manage" => PermissionType.WebhooksManage,

            _ => null // Unknown scopes are ignored
        };
    }
}
