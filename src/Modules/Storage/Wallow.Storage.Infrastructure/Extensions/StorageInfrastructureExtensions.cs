using System.Net.Sockets;
using Amazon;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Application.Configuration;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Settings;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Infrastructure.Configuration;
using Wallow.Storage.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Persistence.Repositories;
using Wallow.Storage.Infrastructure.Providers;
using Wallow.Storage.Infrastructure.Scanning;

namespace Wallow.Storage.Infrastructure.Extensions;

public static class StorageInfrastructureExtensions
{
    public static IServiceCollection AddStorageInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PresignedUrlOptions>(configuration.GetSection(PresignedUrlOptions.SectionName));
        services.AddStoragePersistence(configuration);
        services.AddReadDbContext<StorageDbContext>(configuration);
        services.AddSettings<StorageDbContext, StorageSettingKeys>("storage");
        services.AddStorageProvider(configuration);
        services.AddScoped<IFileScanner, ClamAvFileScanner>();
        services.AddClamAvHealthCheck(configuration);

        return services;
    }

    private static void AddStoragePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<StorageDbContext>((sp, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "storage");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<StorageDbContext>(sp =>
        {
            IDbContextFactory<StorageDbContext> factory = sp.GetRequiredService<IDbContextFactory<StorageDbContext>>();
            StorageDbContext ctx = factory.CreateDbContext();
            ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
            ctx.SetTenant(tenant.TenantId);
            return ctx;
        });

        services.AddScoped<IStorageBucketRepository, StorageBucketRepository>();
        services.AddScoped<IStoredFileRepository, StoredFileRepository>();
    }

    private static void AddStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        StorageOptions storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
                             ?? new StorageOptions();

        switch (storageOptions.Provider)
        {
            case StorageProvider.S3:
                services.AddSingleton<IAmazonS3>(sp =>
                {
                    S3StorageOptions s3Options = sp.GetRequiredService<IOptions<StorageOptions>>().Value.S3;
                    AmazonS3Config config = new()
                    {
                        ForcePathStyle = s3Options.UsePathStyle,
                        AuthenticationRegion = s3Options.Region
                    };

                    if (!string.IsNullOrEmpty(s3Options.Endpoint))
                    {
                        config.ServiceURL = s3Options.Endpoint;
                    }
                    else
                    {
                        config.RegionEndpoint = RegionEndpoint.GetBySystemName(s3Options.Region);
                    }
                    return new AmazonS3Client(s3Options.AccessKey, s3Options.SecretKey, config);
                });
                services.AddScoped<IStorageProvider, S3StorageProvider>();
                break;
            default:
                services.AddSingleton<IStorageProvider, LocalStorageProvider>();
                break;
        }
    }

    private static void AddClamAvHealthCheck(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        StorageOptions storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
                                        ?? new StorageOptions();

        services.AddHealthChecks()
            .AddCheck(
                "clamav",
                new ClamAvHealthCheck(storageOptions.ClamAvHost, storageOptions.ClamAvPort),
                tags: ["clamav"]);
    }
}

internal sealed class ClamAvHealthCheck(string host, int port) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using TcpClient client = new();
            await client.ConnectAsync(host, port, cancellationToken);
            return HealthCheckResult.Healthy("ClamAV is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("ClamAV is unreachable.", ex);
        }
    }
}
