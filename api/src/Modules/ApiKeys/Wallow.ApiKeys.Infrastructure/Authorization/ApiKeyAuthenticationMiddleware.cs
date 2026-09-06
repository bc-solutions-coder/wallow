using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Wallow.ApiKeys.Domain.Errors;
using Wallow.Shared.Contracts.ApiKeys;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.ApiKeys.Infrastructure.Authorization;

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
            await WriteInvalidKeyProblemAsync(context);
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
            new("org_id", result.TenantId!.Value.ToString())
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
    // The reason stays in the log; the body carries the catalog code and its user-safe sentence.
    private static async Task WriteInvalidKeyProblemAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

        ProblemDetails problem = new()
        {
            Status = StatusCodes.Status401Unauthorized,
            Detail = ApiKeysErrors.ApiKeyInvalid.DefaultMessage,
        };
        problem.Extensions["code"] = ApiKeysErrors.ApiKeyInvalid.Code;

        IProblemDetailsService problemDetailsService =
            context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid API key attempt: {Error}")]
    private partial void LogInvalidApiKeyAttempt(string? error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "API key {KeyId} authenticated for user {UserId} in tenant {TenantId}")]
    private partial void LogApiKeyAuthenticated(string? keyId, Guid? userId, Guid? tenantId);
}
