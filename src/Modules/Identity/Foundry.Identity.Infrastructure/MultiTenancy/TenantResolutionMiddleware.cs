using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Text.Json;
using Foundry.Shared.Kernel;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Foundry.Identity.Infrastructure.MultiTenancy;

public partial class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    private static readonly Meter _meter = Diagnostics.CreateMeter("Foundry");
    private static readonly Counter<long> _requestsByTenantCounter = _meter.CreateCounter<long>(
        "foundry.requests_by_tenant_total",
        description: "Total requests by tenant");

    public async Task InvokeAsync(HttpContext context, ITenantContextSetter tenantSetter)
    {
        Guid? resolvedTenantId = null;
        string resolvedTenantName = string.Empty;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            Claim? orgClaim = context.User.FindFirst("organization");
            if (orgClaim != null)
            {
                Guid? orgId = ParseOrganizationId(orgClaim.Value);
                string orgName = ParseOrganizationName(orgClaim.Value);

                if (orgId.HasValue)
                {
                    resolvedTenantId = orgId.Value;
                    resolvedTenantName = orgName;
                    LogTenantResolved(orgId, orgName);
                }
            }

            // Admin override: allow X-Tenant-Id header for verified realm admin
            string? headerTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(headerTenantId))
            {
                if (!Guid.TryParseExact(headerTenantId, "D", out Guid overrideId))
                {
                    LogInvalidTenantIdHeader(headerTenantId);
                }
                else if (HasRealmAdminRole(context.User))
                {
                    string adminUserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                    string requestPath = context.Request.Path.Value ?? "/";

                    LogAdminTenantOverride(overrideId, resolvedTenantId, adminUserId, requestPath);
                    resolvedTenantId = overrideId;
                    resolvedTenantName = string.Empty;
                }
            }

            // Region resolution: JWT claim > header > default
            string? region = context.User.FindFirst("tenant_region")?.Value;
            if (string.IsNullOrEmpty(region))
            {
                region = context.Request.Headers["X-Tenant-Region"].FirstOrDefault();
            }

            string resolvedRegion = !string.IsNullOrEmpty(region)
                ? region
                : RegionConfiguration.PrimaryRegion;

            if (resolvedTenantId.HasValue)
            {
                tenantSetter.SetTenant(TenantId.Create(resolvedTenantId.Value), resolvedTenantName, resolvedRegion);
            }

            if (resolvedRegion != RegionConfiguration.PrimaryRegion)
            {
                LogRegionResolved(resolvedRegion);
            }
        }

        if (resolvedTenantId.HasValue)
        {
            _requestsByTenantCounter.Add(1, new KeyValuePair<string, object?>("tenant_id", resolvedTenantId.Value.ToString()));
        }

        string? userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            : null;

        if (userId is not null)
        {
            Activity.Current?.SetTag("enduser.id", userId);
        }

        using (LogContext.PushProperty("TenantId", resolvedTenantId, destructureObjects: false))
        using (LogContext.PushProperty("UserId", userId, destructureObjects: false))
        {
            await next(context);
        }
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid GUID format in X-Tenant-Id header: {HeaderValue}")]
    private partial void LogInvalidTenantIdHeader(string headerValue);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tenant region resolved: {Region}")]
    private partial void LogRegionResolved(string region);
}
