using Foundry.Shared.Kernel.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Shared.Infrastructure.BackgroundJobs;

public static class BackgroundJobsExtensions
{
    public static IServiceCollection AddFoundryBackgroundJobs(this IServiceCollection services)
    {
        services.AddSingleton<IJobScheduler, HangfireJobScheduler>();
        return services;
    }
}
