using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wallow.Shared.Contracts.RateLimiting;

namespace Wallow.Shared.Infrastructure.RateLimiting;

public static class RateLimitingServiceExtensions
{
    public static IServiceCollection AddFixedWindowCounter(this IServiceCollection services)
    {
        services.TryAddSingleton<IFixedWindowCounter, RedisFixedWindowCounter>();
        return services;
    }
}
