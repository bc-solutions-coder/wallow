using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using Wallow.Identity.Application.Telemetry;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
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
            string? orgIdStr = context.User.GetTenantId();
            if (orgIdStr != null && Guid.TryParse(orgIdStr, out Guid orgId))
            {
                resolvedTenantId = orgId;
                resolvedTenantName = context.User.GetTenantName() ?? string.Empty;
                LogTenantResolved(orgId, resolvedTenantName);
            }

            // Allow X-Tenant-Id header only for the non-assignable global-admin claim and for
            // explicitly flagged platform operators. The "admin" role is granted tenant-side
            // through UsersController.AssignRole, so it must never reach another tenant.
            string? headerTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(headerTenantId))
            {
                if (!Guid.TryParseExact(headerTenantId, "D", out Guid overrideId))
                {
                    LogInvalidTenantIdHeader(headerTenantId);
                }
                else if (context.User.IsGlobalAdmin() || context.User.IsOperator())
                {
                    string callerId = context.User.GetUserId() ?? "unknown";
                    string requestPath = context.Request.Path.Value ?? "/";

                    LogAdminTenantOverride(overrideId, resolvedTenantId, callerId, requestPath);
                    resolvedTenantId = overrideId;
                    resolvedTenantName = string.Empty;
                }
            }

            // Region resolution: JWT claim > header > default
            string? region = context.User.GetTenantRegion();
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
                context.Items["TenantId"] = resolvedTenantId.Value.ToString();
                context.Items["TenantName"] = resolvedTenantName;
            }

            if (resolvedRegion != RegionConfiguration.PrimaryRegion)
            {
                LogRegionResolved(resolvedRegion);
            }

            if (!resolvedTenantId.HasValue && RequiresOrganization(context))
            {
                string callerId = context.User.GetUserId() ?? "unknown";
                string requestPath = context.Request.Path.Value ?? "/";
                LogOrganizationRequired(callerId, requestPath);
                await AuthProblemResponse.WriteAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Organization context required.",
                    "The token carries no organization. Sign in again with an organization selected to reach this resource.");
                return;
            }
        }

        if (resolvedTenantId.HasValue)
        {
            IdentityModuleTelemetry.RequestsAuthenticatedTotal.Add(1);
        }

        string? userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.GetUserId()
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
}

public partial class TenantResolutionMiddleware
{
    /// <summary>
    /// An organization-less token reaches only endpoints marked
    /// <see cref="AllowWithoutOrganizationAttribute"/> (and anonymous ones). The auth host's own
    /// cookie session is exempt: it is the sign-in surface, never a tenant-scoped API caller.
    /// </summary>
    private static bool RequiresOrganization(HttpContext context)
    {
        if (context.User.Identity?.AuthenticationType == IdentityConstants.ApplicationScheme)
        {
            return false;
        }

        Endpoint? endpoint = context.GetEndpoint();
        return endpoint is not null
            && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
            && endpoint.Metadata.GetMetadata<AllowWithoutOrganizationAttribute>() is null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization context required: token for user {UserId} carries no org_id and {RequestPath} is tenant-scoped")]
    private partial void LogOrganizationRequired(string userId, string requestPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tenant resolved: {TenantId} ({TenantName})")]
    private partial void LogTenantResolved(Guid? tenantId, string tenantName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin tenant override: OverridingTenantId={OverridingTenantId}, OriginalTenantId={OriginalTenantId}, UserId={UserId}, RequestPath={RequestPath}")]
    private partial void LogAdminTenantOverride(Guid overridingTenantId, Guid? originalTenantId, string userId, string requestPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid GUID format in X-Tenant-Id header: {HeaderValue}")]
    private partial void LogInvalidTenantIdHeader(string headerValue);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tenant region resolved: {Region}")]
    private partial void LogRegionResolved(string region);
}
