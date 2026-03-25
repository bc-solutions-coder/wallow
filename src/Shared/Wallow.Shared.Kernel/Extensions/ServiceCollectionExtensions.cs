using Wallow.Shared.Kernel.Configuration;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wallow.Shared.Kernel.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services)
    {
        // Time abstraction for testable time-dependent code
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton(new TenantSaveChangesInterceptor());
        services.AddScoped<ITenantContextFactory, TenantContextFactory>();
        services.TryAddScoped<ITenantConnectionResolver, DefaultTenantConnectionResolver>();

        services.AddOptions<ServiceUrlsOptions>()
            .BindConfiguration(ServiceUrlsOptions.SectionName);

        return services;
    }
}
