using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Shared.Kernel.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services)
    {
        // Time abstraction for testable time-dependent code
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddScoped<ITenantContextFactory, TenantContextFactory>();

        return services;
    }
}
