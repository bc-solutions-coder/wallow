using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using Wallow.Identity.Application.Telemetry;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Infrastructure.MultiTenancy;

public partial class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextSetter tenantSetter)
    {
        Guid? resolvedTenantId = null;
        string resolvedTenantName = string.Empty;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            Claim? orgIdClaim = context.User.FindFirst("org_id");
            if (orgIdClaim != null && Guid.TryParse(orgIdClaim.Value, out Guid orgId))
            {
                resolvedTenantId = orgId;
                resolvedTenantName = context.User.FindFirst("org_name")?.Value ?? string.Empty;
                LogTenantResolved(orgId, resolvedTenantName);
            }

            // Allow X-Tenant-Id header for realm admins and service accounts
            string? headerTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(headerTenantId))
            {
                if (!Guid.TryParseExact(headerTenantId, "D", out Guid overrideId))
                {
                    LogInvalidTenantIdHeader(headerTenantId);
                }
                else if (HasRealmAdminRole(context.User) || IsOperatorServiceAccount(context.User))
                {
                    string callerId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                    string requestPath = context.Request.Path.Value ?? "/";

                    LogAdminTenantOverride(overrideId, resolvedTenantId, callerId, requestPath);
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
            IdentityModuleTelemetry.RequestsAuthenticatedTotal.Add(1);
        }

        string? userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
              ?? context.User.FindFirst("sub")?.Value
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

    /// <summary>
    /// Checks whether the caller is an operator service account (prefixed with "sa-").
    /// Developer application clients (prefixed with "app-") are intentionally excluded
    /// and must not be granted the X-Tenant-Id override path.
    /// </summary>
    private static bool IsOperatorServiceAccount(ClaimsPrincipal user)
    {
        string? clientId = user.FindFirst("azp")?.Value;
        return clientId?.StartsWith("sa-", StringComparison.Ordinal) == true;
    }

    private const string AdminRole = "admin";

    private static bool HasRealmAdminRole(ClaimsPrincipal user)
    {
        return user.FindAll(ClaimTypes.Role).Any(c =>
            string.Equals(c.Value, AdminRole, StringComparison.OrdinalIgnoreCase));
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
