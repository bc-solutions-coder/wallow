using System.Net.Sockets;
using Amazon;
using Amazon.S3;
using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Storage.Application.Configuration;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Enums;
using Foundry.Storage.Infrastructure.Configuration;
using Foundry.Storage.Infrastructure.Persistence;
using Foundry.Storage.Infrastructure.Persistence.Repositories;
using Foundry.Storage.Infrastructure.Providers;
using Foundry.Storage.Infrastructure.Scanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Foundry.Storage.Infrastructure.Extensions;

public static class StorageInfrastructureExtensions
{
    public static IServiceCollection AddStorageInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PresignedUrlOptions>(configuration.GetSection(PresignedUrlOptions.SectionName));
        services.AddStoragePersistence(configuration);
        services.AddStorageProvider(configuration);
        services.AddScoped<IFileScanner, ClamAvFileScanner>();
        services.AddClamAvHealthCheck(configuration);

        return services;
    }

    private static void AddStoragePersistence(
        this IServiceCollection services,
        IConfiguration _)
    {
        services.AddDbContext<StorageDbContext>((sp, options) =>
        {
            string? connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsql =>
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
                    AmazonS3Config config = new AmazonS3Config
                    {
                        ServiceURL = s3Options.Endpoint,
                        ForcePathStyle = s3Options.UsePathStyle,
                        RegionEndpoint = RegionEndpoint.GetBySystemName(s3Options.Region)
                    };
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
