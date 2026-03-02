using Amazon;
using Amazon.S3;
using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Enums;
using Foundry.Storage.Infrastructure.Configuration;
using Foundry.Storage.Infrastructure.Persistence;
using Foundry.Storage.Infrastructure.Persistence.Repositories;
using Foundry.Storage.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Foundry.Storage.Infrastructure.Extensions;

public static class StorageInfrastructureExtensions
{
    public static IServiceCollection AddStorageInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddStoragePersistence(configuration);
        services.AddStorageProvider(configuration);

        return services;
    }

    private static IServiceCollection AddStoragePersistence(
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

        return services;
    }

    private static IServiceCollection AddStorageProvider(
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
                services.AddSingleton<IStorageProvider, S3StorageProvider>();
                break;
            default:
                services.AddSingleton<IStorageProvider, LocalStorageProvider>();
                break;
        }

        return services;
    }
}
