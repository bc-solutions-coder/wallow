using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.ValueObjects;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Infrastructure.Authorization;

/// <summary>
/// Middleware that authenticates SCIM API requests using Bearer token.
/// Only applies to /scim/v2/* endpoints.
/// Queries ScimConfiguration with IgnoreQueryFilters to bypass tenant filtering,
/// then sets the tenant context from the matched configuration.
/// </summary>
public sealed partial class ScimAuthenticationMiddleware(RequestDelegate next, ILogger<ScimAuthenticationMiddleware> logger)
{

    private const string AuthorizationHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";
    private const string ScimPathPrefix = "/scim/v2";

    public async Task InvokeAsync(
        HttpContext context,
        IdentityDbContext dbContext,
        TenantContext tenantContext,
        TimeProvider timeProvider)
    {
        // Only apply to SCIM endpoints
        if (!context.Request.Path.StartsWithSegments(ScimPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Discovery endpoints don't require authentication per SCIM spec
        if (IsDiscoveryEndpoint(context.Request.Path))
        {
            await next(context);
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

        // Use the token prefix (first 8 chars) to narrow down candidates across all tenants
        string tokenPrefix = token.Length >= 8 ? token[..8] : token;

        ScimConfiguration? config = await dbContext.ScimConfigurations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsEnabled && c.TokenPrefix == tokenPrefix, context.RequestAborted);

        if (config == null || !config.IsTokenValid(timeProvider))
        {
            LogInvalidScimTokenAttempt(context.Connection.RemoteIpAddress);
            await ReturnUnauthorizedAsync(context, "Invalid or expired SCIM token");
            return;
        }

        // Validate full token hash with constant-time comparison
        string hashedToken = TokenHash.Compute(token);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(config.BearerToken);
        byte[] actualBytes = Encoding.UTF8.GetBytes(hashedToken);

        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            LogInvalidScimTokenAttempt(context.Connection.RemoteIpAddress);
            await ReturnUnauthorizedAsync(context, "Invalid or expired SCIM token");
            return;
        }

        // Set tenant context from the matched SCIM configuration
        tenantContext.SetTenant(config.TenantId);

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

        await next(context);
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
