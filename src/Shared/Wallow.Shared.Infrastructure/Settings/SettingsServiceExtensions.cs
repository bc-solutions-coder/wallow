using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Shared.Infrastructure.Settings;

public static class SettingsServiceExtensions
{
    public static IServiceCollection AddSettings<TDbContext, TRegistry>(this IServiceCollection services, string moduleKey)
        where TDbContext : TenantAwareDbContext<TDbContext>
        where TRegistry : class, ISettingRegistry, new()
    {
        TRegistry registry = new();
        services.AddKeyedSingleton<ISettingRegistry>(moduleKey, registry);
        services.AddScoped<ITenantSettingRepository<TDbContext>, TenantSettingRepository<TDbContext>>();
        services.AddScoped<IUserSettingRepository<TDbContext>, UserSettingRepository<TDbContext>>();
        services.AddKeyedScoped<ISettingsService>(moduleKey, (sp, _) =>
        {
            ITenantSettingRepository<TDbContext> tenantRepo = sp.GetRequiredService<ITenantSettingRepository<TDbContext>>();
            IUserSettingRepository<TDbContext> userRepo = sp.GetRequiredService<IUserSettingRepository<TDbContext>>();
            IDistributedCache cache = sp.GetRequiredService<IDistributedCache>();
            return new CachedSettingsService<TDbContext>(tenantRepo, userRepo, registry, cache);
        });

        return services;
    }
}
