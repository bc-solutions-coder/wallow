using System.Diagnostics.CodeAnalysis;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Branding.Infrastructure.Repositories;
using Wallow.Branding.Infrastructure.Services;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Wallow.Branding.Infrastructure.Extensions;

[ExcludeFromCodeCoverage]
public static class BrandingInfrastructureExtensions
{
    public static IServiceCollection AddBrandingInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBrandingPersistence(configuration);
        services.AddReadDbContext<BrandingDbContext>(configuration);
        return services;
    }

    private static void AddBrandingPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<BrandingDbContext>((sp, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "branding");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<BrandingDbContext>(sp =>
        {
            IDbContextFactory<BrandingDbContext> factory = sp.GetRequiredService<IDbContextFactory<BrandingDbContext>>();
            BrandingDbContext ctx = factory.CreateDbContext();
            ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
            ctx.SetTenant(tenant.TenantId);
            return ctx;
        });

        // Dedicated bounded cache for branding — separate from the global IMemoryCache so that
        // SizeLimit works safely (third-party libs like OpenIddict don't set Size on entries)
        services.AddKeyedSingleton<IMemoryCache>("BrandingCache",
            (_, _) => new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }));

        // Branding repositories
        services.AddScoped<IClientBrandingRepository, ClientBrandingRepository>();

        // Branding services
        services.AddScoped<IClientBrandingService, ClientBrandingService>();
    }
}
