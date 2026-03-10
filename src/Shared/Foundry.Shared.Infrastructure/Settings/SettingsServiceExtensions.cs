using Foundry.Shared.Infrastructure.Core.Persistence;
using Foundry.Shared.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Shared.Infrastructure.Settings;

public static class SettingsServiceExtensions
{
    public static IServiceCollection AddSettings<TDbContext, TRegistry>(this IServiceCollection services)
        where TDbContext : TenantAwareDbContext<TDbContext>
        where TRegistry : class, ISettingRegistry
    {
        services.AddSingleton<ISettingRegistry, TRegistry>();
        services.AddScoped<ITenantSettingRepository<TDbContext>, TenantSettingRepository<TDbContext>>();
        services.AddScoped<IUserSettingRepository<TDbContext>, UserSettingRepository<TDbContext>>();
        services.AddScoped<ISettingsService, CachedSettingsService<TDbContext>>();

        return services;
    }
}
