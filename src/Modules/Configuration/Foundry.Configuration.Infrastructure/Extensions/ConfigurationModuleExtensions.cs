using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Extensions;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Infrastructure.Persistence;
using Foundry.Configuration.Infrastructure.Persistence.Repositories;
using Foundry.Configuration.Infrastructure.Services;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Foundry.Configuration.Infrastructure.Extensions;

public static partial class ConfigurationModuleExtensions
{
    public static IServiceCollection AddConfigurationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = services.AddConfigurationApplication();
        return services.AddConfigurationPersistence(configuration);
    }

    private static IServiceCollection AddConfigurationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ConfigurationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "configuration");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<ICustomFieldDefinitionRepository, CustomFieldDefinitionRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<IFeatureFlagOverrideRepository, FeatureFlagOverrideRepository>();

        services.AddScoped<FeatureFlagService>();
        services.AddScoped<IFeatureFlagService>(sp =>
            new CachedFeatureFlagService(
                sp.GetRequiredService<FeatureFlagService>(),
                sp.GetRequiredService<IDistributedCache>()));

        return services;
    }

    public static async Task InitializeConfigurationModuleAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            try
            {
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                ConfigurationDbContext db = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
                await db.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ConfigurationModule");
                LogStartupFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Configuration module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
