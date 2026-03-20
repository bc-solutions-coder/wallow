using Wallow.Shared.Kernel.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Shared.Infrastructure.BackgroundJobs;

public static class BackgroundJobsExtensions
{
    public static IServiceCollection AddWallowBackgroundJobs(this IServiceCollection services)
    {
        services.AddSingleton<IJobScheduler, HangfireJobScheduler>();
        return services;
    }
}
