using System.Security.Claims;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Foundry.Identity.Infrastructure.Authorization;

/// <summary>
/// Middleware that authenticates requests using API keys (X-Api-Key header).
/// Falls through to JWT authentication if no API key is present.
/// </summary>
public sealed partial class ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
{

    private const string ApiKeyHeader = "X-Api-Key";

    public async Task InvokeAsync(
        HttpContext context,
        IApiKeyService apiKeyService,
        TenantContext tenantContext)
    {
        // Check for API key header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out StringValues apiKeyHeader))
        {
            // No API key, continue to next middleware (JWT auth)
            await next(context);
            return;
        }

        string apiKey = apiKeyHeader.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await next(context);
            return;
        }

        // Validate the API key
        ApiKeyValidationResult result = await apiKeyService.ValidateApiKeyAsync(apiKey, context.RequestAborted);

        if (!result.IsValid)
        {
            LogInvalidApiKeyAttempt(result.Error);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Unauthorized",
                status = 401,
                detail = result.Error ?? "Invalid API key"
            });
            return;
        }

        LogApiKeyAuthenticated(result.KeyId, result.UserId, result.TenantId);

        // Create claims principal from API key
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, result.UserId!.Value.ToString()),
            new("sub", result.UserId!.Value.ToString()),
            new("api_key_id", result.KeyId!),
            new("auth_method", "api_key"),
            new("organization", result.TenantId!.Value.ToString())
        ];

        // Add scope claims (or all permissions if no scopes specified)
        if (result.Scopes != null && result.Scopes.Count > 0)
        {
            foreach (string scope in result.Scopes)
            {
                claims.Add(new Claim("scope", scope));
            }
        }

        ClaimsIdentity identity = new(claims, "ApiKey");
        context.User = new ClaimsPrincipal(identity);

        // Set tenant context (same pattern as TenantResolutionMiddleware)
        tenantContext.SetTenant(TenantId.Create(result.TenantId!.Value), $"api-key-{result.KeyId}");

        await next(context);
    }
}

public sealed partial class ApiKeyAuthenticationMiddleware
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid API key attempt: {Error}")]
    private partial void LogInvalidApiKeyAttempt(string? error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "API key {KeyId} authenticated for user {UserId} in tenant {TenantId}")]
    private partial void LogApiKeyAuthenticated(string? keyId, Guid? userId, Guid? tenantId);
}
