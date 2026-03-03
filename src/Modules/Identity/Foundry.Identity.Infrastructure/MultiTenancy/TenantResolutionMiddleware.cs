using System.Security.Claims;
using System.Text.Json;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Infrastructure.MultiTenancy;

public partial class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextSetter tenantSetter)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            Claim? orgClaim = context.User.FindFirst("organization");
            Guid? resolvedTenantId = null;
            if (orgClaim != null)
            {
                Guid? orgId = ParseOrganizationId(orgClaim.Value);
                string orgName = ParseOrganizationName(orgClaim.Value);

                if (orgId.HasValue)
                {
                    resolvedTenantId = orgId.Value;
                    tenantSetter.SetTenant(TenantId.Create(orgId.Value), orgName);
                    LogTenantResolved(orgId, orgName);
                }
            }

            // Admin override: allow X-Tenant-Id header for verified realm admin
            string? headerTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(headerTenantId) &&
                HasRealmAdminRole(context.User) &&
                Guid.TryParse(headerTenantId, out Guid overrideId))
            {
                string userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                string requestPath = context.Request.Path.Value ?? "/";

                tenantSetter.SetTenant(TenantId.Create(overrideId));
                LogAdminTenantOverride(overrideId, resolvedTenantId, userId, requestPath);
            }

            // Region resolution: JWT claim > header > default
            string? region = context.User.FindFirst("tenant_region")?.Value;
            if (string.IsNullOrEmpty(region))
            {
                region = context.Request.Headers["X-Tenant-Region"].FirstOrDefault();
            }

            tenantSetter.Region = !string.IsNullOrEmpty(region)
                ? region
                : RegionConfiguration.PrimaryRegion;

            if (tenantSetter.Region != RegionConfiguration.PrimaryRegion)
            {
                LogRegionResolved(tenantSetter.Region);
            }
        }

        await _next(context);
    }

    private const string AdminRole = "admin";

    private static bool HasRealmAdminRole(ClaimsPrincipal user)
    {
        // Check standard role claims first (mapped by auth middleware)
        if (user.FindAll(ClaimTypes.Role).Any(c =>
                string.Equals(c.Value, AdminRole, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Fallback: parse Keycloak realm_access claim directly
        string? realmAccess = user.FindFirst("realm_access")?.Value;
        if (string.IsNullOrEmpty(realmAccess))
        {
            return false;
        }

        try
        {
            JsonElement parsed = JsonSerializer.Deserialize<JsonElement>(realmAccess);
            if (parsed.TryGetProperty("roles", out JsonElement rolesArray))
            {
                return rolesArray.EnumerateArray()
                    .Any(r => string.Equals(r.GetString(), AdminRole, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (JsonException)
        {
            // Invalid JSON, deny admin access
        }

        return false;
    }

    private static Guid? ParseOrganizationId(string organizationClaimValue)
    {
        if (string.IsNullOrWhiteSpace(organizationClaimValue))
        {
            return null;
        }

        // Try simple GUID format first
        if (Guid.TryParse(organizationClaimValue, out Guid simpleGuid))
        {
            return simpleGuid;
        }

        // Try Keycloak 26+ JSON format: {"orgId": {"name": "orgName"}}
        try
        {
            JsonElement json = JsonSerializer.Deserialize<JsonElement>(organizationClaimValue);

            // The organization ID is the property name, not a value
            foreach (JsonProperty property in json.EnumerateObject())
            {
                if (Guid.TryParse(property.Name, out Guid orgId))
                {
                    return orgId;
                }
            }
        }
        catch (JsonException)
        {
            // Not valid JSON, fall through
        }

        return null;
    }

    private static string ParseOrganizationName(string organizationClaimValue)
    {
        if (string.IsNullOrWhiteSpace(organizationClaimValue))
        {
            return string.Empty;
        }

        // Try Keycloak 26+ JSON format: {"orgId": {"name": "orgName"}}
        try
        {
            JsonElement json = JsonSerializer.Deserialize<JsonElement>(organizationClaimValue);

            // The organization name is inside the nested object
            foreach (JsonProperty property in json.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object &&
                    property.Value.TryGetProperty("name", out JsonElement nameElement))
                {
                    return nameElement.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
            // Not valid JSON, return the raw value as name
            return organizationClaimValue;
        }

        return string.Empty;
    }
}

public partial class TenantResolutionMiddleware
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Tenant resolved: {TenantId} ({TenantName})")]
    private partial void LogTenantResolved(Guid? tenantId, string tenantName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin tenant override: OverridingTenantId={OverridingTenantId}, OriginalTenantId={OriginalTenantId}, UserId={UserId}, RequestPath={RequestPath}")]
    private partial void LogAdminTenantOverride(Guid overridingTenantId, Guid? originalTenantId, string userId, string requestPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tenant region resolved: {Region}")]
    private partial void LogRegionResolved(string region);
}
