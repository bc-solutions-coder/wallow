using System.Security.Claims;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Foundry.Identity.Infrastructure.Authorization;

/// <summary>
/// Middleware that authenticates SCIM API requests using Bearer token.
/// Only applies to /scim/v2/* endpoints.
/// </summary>
public sealed partial class ScimAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ScimAuthenticationMiddleware> _logger;

    private const string AuthorizationHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";
    private const string ScimPathPrefix = "/scim/v2";

    public ScimAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ScimAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IScimService scimService,
        TenantContext tenantContext)
    {
        // Only apply to SCIM endpoints
        if (!context.Request.Path.StartsWithSegments(ScimPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Discovery endpoints don't require authentication per SCIM spec
        if (IsDiscoveryEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Extract Bearer token
        if (!context.Request.Headers.TryGetValue(AuthorizationHeader, out StringValues authHeader))
        {
            await ReturnUnauthorizedAsync(context, "Missing Authorization header");
            return;
        }

        string authValue = authHeader.ToString();
        if (!authValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await ReturnUnauthorizedAsync(context, "Invalid authorization scheme. Use Bearer token.");
            return;
        }

        string token = authValue[BearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            await ReturnUnauthorizedAsync(context, "Empty Bearer token");
            return;
        }

        // Validate the SCIM token
        bool isValid = await scimService.ValidateTokenAsync(token, context.RequestAborted);

        if (!isValid)
        {
            LogInvalidScimTokenAttempt(context.Connection.RemoteIpAddress);
            await ReturnUnauthorizedAsync(context, "Invalid or expired SCIM token");
            return;
        }

        LogScimTokenAuthenticated(tenantContext.TenantId.Value);

        // Create a minimal claims principal for SCIM requests
        List<Claim> claims =
        [
            new("scim_client", "true"),
            new("auth_method", "scim_bearer"),
            new("tenant_id", tenantContext.TenantId.Value.ToString())
        ];

        ClaimsIdentity identity = new(claims, "ScimBearer");
        context.User = new ClaimsPrincipal(identity);

        await _next(context);
    }

    private static bool IsDiscoveryEndpoint(PathString path)
    {
        string pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;

        return pathValue.EndsWith("/serviceproviderconfig", StringComparison.OrdinalIgnoreCase) ||
               pathValue.EndsWith("/schemas", StringComparison.OrdinalIgnoreCase) ||
               pathValue.EndsWith("/resourcetypes", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ReturnUnauthorizedAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/scim+json";

        ScimError error = new()
        {
            Status = 401,
            ScimType = "invalidCredentials",
            Detail = detail
        };

        await context.Response.WriteAsJsonAsync(error);
    }
}

public sealed partial class ScimAuthenticationMiddleware
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid SCIM token attempt from {RemoteIp}")]
    private partial void LogInvalidScimTokenAttempt(System.Net.IPAddress? remoteIp);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SCIM token authenticated for tenant {TenantId}")]
    private partial void LogScimTokenAuthenticated(Guid tenantId);
}
