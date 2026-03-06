using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Infrastructure.Middleware;

/// <summary>
/// Tracks service account usage by updating LastUsedAt timestamp when API requests are made.
/// </summary>
public sealed partial class ServiceAccountTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ServiceAccountTrackingMiddleware> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ServiceAccountTrackingMiddleware(
        RequestDelegate next,
        ILogger<ServiceAccountTrackingMiddleware> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _next = next;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Only track successful requests from service accounts
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            // azp (Authorized Party) contains the Keycloak client ID
            string? clientId = context.User.FindFirst("azp")?.Value;
            if (clientId?.StartsWith("sa-", StringComparison.Ordinal) == true)
            {
                // Fire and forget - don't block the response
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Need to create a new scope since the request scope may be disposed
                        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
                        IServiceAccountUnfilteredRepository repository = scope.ServiceProvider.GetRequiredService<IServiceAccountUnfilteredRepository>();
                        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                        ServiceAccountMetadata? metadata = await repository.GetByKeycloakClientIdAsync(clientId);
                        if (metadata != null)
                        {
                            metadata.MarkUsed(timeProvider);
                            await repository.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail - tracking is non-critical
                        LogUpdateLastUsedFailed(ex, clientId);
                    }
                });
            }
        }
    }
}

public sealed partial class ServiceAccountTrackingMiddleware
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update LastUsedAt for service account {ClientId}")]
    private partial void LogUpdateLastUsedFailed(Exception ex, string clientId);
}

/// <summary>
/// Extension methods for registering ServiceAccountTrackingMiddleware.
/// </summary>
public static class ServiceAccountTrackingMiddlewareExtensions
{
    /// <summary>
    /// Adds service account usage tracking middleware to the pipeline.
    /// Should be called after UseAuthentication and UseAuthorization.
    /// </summary>
    public static IApplicationBuilder UseServiceAccountTracking(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ServiceAccountTrackingMiddleware>();
    }
}
