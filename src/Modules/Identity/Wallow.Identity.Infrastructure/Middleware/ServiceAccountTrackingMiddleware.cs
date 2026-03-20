using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wallow.Identity.Infrastructure.Middleware;

/// <summary>
/// Tracks service account usage by updating LastUsedAt timestamp when API requests are made.
/// Lazily creates metadata records for service accounts that don't yet have one.
/// </summary>
public sealed partial class ServiceAccountTrackingMiddleware(RequestDelegate next, ILogger<ServiceAccountTrackingMiddleware> logger, IServiceScopeFactory serviceScopeFactory)
{

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        // Only track successful requests from service accounts
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            // azp (Authorized Party) contains the Keycloak client ID
            string? clientId = context.User.FindFirst("azp")?.Value;
            if (clientId?.StartsWith("sa-", StringComparison.Ordinal) == true
                    || clientId?.StartsWith("app-", StringComparison.Ordinal) == true)
            {
                // Fire and forget - don't block the response
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Need to create a new scope since the request scope may be disposed
                        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
                        IServiceAccountUnfilteredRepository unfilteredRepository = scope.ServiceProvider.GetRequiredService<IServiceAccountUnfilteredRepository>();
                        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                        ServiceAccountMetadata? metadata = await unfilteredRepository.GetByKeycloakClientIdAsync(clientId);
                        if (metadata != null)
                        {
                            metadata.MarkUsed(timeProvider);
                            await unfilteredRepository.SaveChangesAsync();
                        }
                        else
                        {
                            // Lazily create metadata for service accounts that authenticated via Keycloak
                            // but don't yet have a local metadata record
                            IServiceAccountRepository repository = scope.ServiceProvider.GetRequiredService<IServiceAccountRepository>();
                            ServiceAccountMetadata newMetadata = ServiceAccountMetadata.Create(
                                TenantId.Platform,
                                clientId,
                                clientId,
                                null,
                                [],
                                Guid.Empty,
                                timeProvider);
                            newMetadata.MarkUsed(timeProvider);
                            repository.Add(newMetadata);
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
