using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.ApiKeys.Infrastructure.Repositories;
using Wallow.ApiKeys.Infrastructure.Services;
using Wallow.Shared.Contracts.ApiKeys;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.ApiKeys.Infrastructure.Extensions;

[ExcludeFromCodeCoverage]
public static class ApiKeysInfrastructureExtensions
{
    public static IServiceCollection AddApiKeysInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApiKeysPersistence(configuration);
        services.AddReadDbContext<ApiKeysDbContext>(configuration);
        return services;
    }

    private static void AddApiKeysPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<ApiKeysDbContext>((sp, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "apikeys");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<ApiKeysDbContext>(sp =>
        {
            IDbContextFactory<ApiKeysDbContext> factory = sp.GetRequiredService<IDbContextFactory<ApiKeysDbContext>>();
            ApiKeysDbContext ctx = factory.CreateDbContext();
            ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
            ctx.SetTenant(tenant.TenantId);
            return ctx;
        });

        // ApiKeys repositories
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

        // ApiKeys services
        services.AddSingleton<IRedisDatabase>(sp =>
        {
            IConnectionMultiplexer mux = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisDatabaseWrapper(mux.GetDatabase());
        });
        services.AddScoped<IApiKeyService, RedisApiKeyService>();
    }
}
