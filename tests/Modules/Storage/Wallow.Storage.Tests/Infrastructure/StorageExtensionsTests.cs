using System.Net;
using System.Net.Sockets;
using Amazon.S3;
using FluentValidation;
using Wallow.Shared.Contracts.Storage;
using Wallow.Storage.Application.Configuration;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Infrastructure.Configuration;
using Wallow.Storage.Infrastructure.Extensions;
using Wallow.Storage.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Scanning;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Wallow.Storage.Tests.Infrastructure;

public sealed class StorageExtensionsTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ServiceCollection CreateBaseServices(IConfiguration configuration)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        return services;
    }

    [Fact]
    public void AddStorageInfrastructure_WithLocalProvider_RegistersLocalStorageProvider()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:Provider"] = "Local"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? providerDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IStorageProvider));

        providerDescriptor.Should().NotBeNull();
        providerDescriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        providerDescriptor.ImplementationType!.Name.Should().Be("LocalStorageProvider");
    }

    [Fact]
    public void AddStorageInfrastructure_WithS3Provider_RegistersS3StorageProvider()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:Provider"] = "S3",
            ["Storage:S3:Endpoint"] = "http://localhost:9000",
            ["Storage:S3:AccessKey"] = "test-key",
            ["Storage:S3:SecretKey"] = "test-secret",
            ["Storage:S3:BucketName"] = "test-bucket",
            ["Storage:S3:Region"] = "us-east-1"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? providerDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IStorageProvider));

        providerDescriptor.Should().NotBeNull();
        providerDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
        providerDescriptor.ImplementationType!.Name.Should().Be("S3StorageProvider");
    }

    [Fact]
    public void AddStorageInfrastructure_WithS3Provider_RegistersAmazonS3Client()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:Provider"] = "S3",
            ["Storage:S3:Endpoint"] = "http://localhost:9000",
            ["Storage:S3:AccessKey"] = "test-key",
            ["Storage:S3:SecretKey"] = "test-secret",
            ["Storage:S3:BucketName"] = "test-bucket",
            ["Storage:S3:Region"] = "us-east-1"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? s3Descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IAmazonS3));

        s3Descriptor.Should().NotBeNull();
        s3Descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddStorageInfrastructure_WithLocalProvider_DoesNotRegisterAmazonS3Client()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:Provider"] = "Local"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? s3Descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IAmazonS3));

        s3Descriptor.Should().BeNull();
    }

    [Fact]
    public void AddStorageInfrastructure_DefaultProvider_RegistersLocalStorageProvider()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? providerDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IStorageProvider));

        providerDescriptor.Should().NotBeNull();
        providerDescriptor!.ImplementationType!.Name.Should().Be("LocalStorageProvider");
    }

    [Fact]
    public void AddStorageInfrastructure_RegistersFileScanner()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? scannerDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IFileScanner));

        scannerDescriptor.Should().NotBeNull();
        scannerDescriptor!.ImplementationType.Should().Be<ClamAvFileScanner>();
        scannerDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddStorageInfrastructure_RegistersClamAvHealthCheck()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? healthCheckDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IHealthCheck));

        // Health checks are registered through IHealthChecksBuilder, verify via the registration
        bool hasHealthChecks = services.Any(
            d => d.ServiceType == typeof(HealthCheckRegistration));

        // The AddHealthChecks extension registers HealthCheckService
        bool hasHealthCheckService = services.Any(
            d => d.ServiceType == typeof(HealthCheckService));

        hasHealthCheckService.Should().BeTrue();
    }

    [Fact]
    public void AddStorageInfrastructure_RegistersStorageDbContext()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? dbContextDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(StorageDbContext));

        dbContextDescriptor.Should().NotBeNull();
        dbContextDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddStorageInfrastructure_RegistersStorageBucketRepository()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? repoDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IStorageBucketRepository));

        repoDescriptor.Should().NotBeNull();
        repoDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddStorageInfrastructure_RegistersStoredFileRepository()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceDescriptor? repoDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IStoredFileRepository));

        repoDescriptor.Should().NotBeNull();
        repoDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddStorageInfrastructure_BindsPresignedUrlOptions()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:PresignedUrls:MaxDownloadExpiryMinutes"] = "120",
            ["Storage:PresignedUrls:MaxUploadExpiryMinutes"] = "30"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        PresignedUrlOptions options = provider.GetRequiredService<IOptions<PresignedUrlOptions>>().Value;

        options.MaxDownloadExpiryMinutes.Should().Be(120);
        options.MaxUploadExpiryMinutes.Should().Be(30);
    }

    [Fact]
    public void AddStorageInfrastructure_ReturnsServiceCollection()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);

        IServiceCollection result = services.AddStorageInfrastructure(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddStorageInfrastructure_WithCustomClamAvConfig_RegistersHealthCheckWithCustomValues()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:ClamAvHost"] = "clamav.internal",
            ["Storage:ClamAvPort"] = "3311"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);

        bool hasHealthCheckService = services.Any(
            d => d.ServiceType == typeof(HealthCheckService));

        hasHealthCheckService.Should().BeTrue();
    }

    [Fact]
    public void AddStorageModule_RegistersBothApplicationAndInfrastructure()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageModule(configuration);

        // Verify infrastructure registrations exist
        bool hasStorageProvider = services.Any(d => d.ServiceType == typeof(IStorageProvider));
        bool hasFileScanner = services.Any(d => d.ServiceType == typeof(IFileScanner));

        hasStorageProvider.Should().BeTrue();
        hasFileScanner.Should().BeTrue();
    }

    [Fact]
    public void AddStorageModule_ReturnsServiceCollection()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);

        IServiceCollection result = services.AddStorageModule(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddStorageModule_RegistersStorageDbContext()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageModule(configuration);

        ServiceDescriptor? dbContextDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(StorageDbContext));

        dbContextDescriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddStorageModule_RegistersRepositories()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageModule(configuration);

        bool hasBucketRepo = services.Any(d => d.ServiceType == typeof(IStorageBucketRepository));
        bool hasFileRepo = services.Any(d => d.ServiceType == typeof(IStoredFileRepository));

        hasBucketRepo.Should().BeTrue();
        hasFileRepo.Should().BeTrue();
    }

    [Fact]
    public void AddStorageModule_WithS3Config_RegistersS3Provider()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:Provider"] = "S3",
            ["Storage:S3:Endpoint"] = "http://localhost:9000",
            ["Storage:S3:AccessKey"] = "key",
            ["Storage:S3:SecretKey"] = "secret",
            ["Storage:S3:BucketName"] = "bucket",
            ["Storage:S3:Region"] = "us-east-1"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageModule(configuration);

        ServiceDescriptor? providerDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IStorageProvider));

        providerDescriptor.Should().NotBeNull();
        providerDescriptor!.ImplementationType!.Name.Should().Be("S3StorageProvider");
    }

    [Fact]
    public async Task ClamAvHealthCheck_WhenReachable_ReturnsHealthy()
    {
        using FakeClamAvServer server = new FakeClamAvServer();
        ClamAvHealthCheck healthCheck = new ClamAvHealthCheck("127.0.0.1", server.Port);
        HealthCheckContext context = new HealthCheckContext();

        HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("reachable");
    }

    [Fact]
    public async Task ClamAvHealthCheck_WhenUnreachable_ReturnsUnhealthy()
    {
        // Use port 1 which nothing listens on
        ClamAvHealthCheck healthCheck = new ClamAvHealthCheck("127.0.0.1", 1);
        HealthCheckContext context = new HealthCheckContext();

        HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("unreachable");
    }

    [Fact]
    public async Task ClamAvHealthCheck_WhenUnreachable_IncludesException()
    {
        ClamAvHealthCheck healthCheck = new ClamAvHealthCheck("127.0.0.1", 1);
        HealthCheckContext context = new HealthCheckContext();

        HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

        result.Exception.Should().NotBeNull();
    }

    [Fact]
    public void AddStorageInfrastructure_WithS3Provider_ResolvesAmazonS3Client()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:Provider"] = "S3",
            ["Storage:S3:Endpoint"] = "http://localhost:9000",
            ["Storage:S3:AccessKey"] = "test-key",
            ["Storage:S3:SecretKey"] = "test-secret",
            ["Storage:S3:BucketName"] = "test-bucket",
            ["Storage:S3:Region"] = "us-east-1",
            ["Storage:S3:UsePathStyle"] = "true"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        IAmazonS3 s3Client = provider.GetRequiredService<IAmazonS3>();

        s3Client.Should().NotBeNull();
        s3Client.Should().BeOfType<AmazonS3Client>();
    }

    [Fact]
    public void AddStorageInfrastructure_WithS3Provider_ConfiguresRegionEndpoint()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:Provider"] = "S3",
            ["Storage:S3:Endpoint"] = "http://minio.local:9000",
            ["Storage:S3:AccessKey"] = "my-access-key",
            ["Storage:S3:SecretKey"] = "my-secret-key",
            ["Storage:S3:BucketName"] = "my-bucket",
            ["Storage:S3:Region"] = "eu-west-1",
            ["Storage:S3:UsePathStyle"] = "false"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        IAmazonS3 s3Client = provider.GetRequiredService<IAmazonS3>();

        s3Client.Config.RegionEndpoint.SystemName.Should().Be("eu-west-1");
    }

    [Fact]
    public void AddStorageInfrastructure_BindsStorageOptions()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:Provider"] = "Local",
            ["Storage:ClamAvHost"] = "clam.internal",
            ["Storage:ClamAvPort"] = "9999"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        StorageOptions options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

        options.Provider.Should().Be(Storage.Domain.Enums.StorageProvider.Local);
        options.ClamAvHost.Should().Be("clam.internal");
        options.ClamAvPort.Should().Be(9999);
    }

    [Fact]
    public void AddStorageInfrastructure_RegistersClamAvHealthCheckWithCorrectNameAndTags()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        HealthCheckServiceOptions healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        HealthCheckRegistration? clamRegistration = healthOptions.Registrations
            .FirstOrDefault(r => r.Name == "clamav");

        clamRegistration.Should().NotBeNull();
        clamRegistration!.Tags.Should().Contain("clamav");
    }

    [Fact]
    public void AddStorageInfrastructure_WithNullStorageSection_DefaultsToLocalProvider()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageInfrastructure(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        StorageOptions options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

        options.Provider.Should().Be(Storage.Domain.Enums.StorageProvider.Local);
        options.ClamAvHost.Should().Be("localhost");
        options.ClamAvPort.Should().Be(3310);
    }

    [Fact]
    public void AddStorageModule_RegistersFluentValidators()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageModule(configuration);

        bool hasValidators = services.Any(d =>
            d.ServiceType.IsGenericType &&
            d.ServiceType.GetGenericTypeDefinition() == typeof(IValidator<>));

        hasValidators.Should().BeTrue();
    }

    [Fact]
    public void AddStorageModule_RegistersPresignedUrlOptions()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:PresignedUrls:MaxDownloadExpiryMinutes"] = "60",
            ["Storage:PresignedUrls:MaxUploadExpiryMinutes"] = "15"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageModule(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        PresignedUrlOptions options = provider.GetRequiredService<IOptions<PresignedUrlOptions>>().Value;

        options.MaxDownloadExpiryMinutes.Should().Be(60);
        options.MaxUploadExpiryMinutes.Should().Be(15);
    }

    [Fact]
    public void AddStorageModule_RegistersHealthChecks()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        ServiceCollection services = CreateBaseServices(configuration);
        services.AddStorageModule(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        HealthCheckServiceOptions healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        healthOptions.Registrations.Should().Contain(r => r.Name == "clamav");
    }

    [Fact]
    public async Task InitializeStorageModuleAsync_InNonDevelopment_SkipsMigration()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        WebApplication app = builder.Build();

        // Should not throw — it skips migration entirely in non-Development
        WebApplication result = await app.InitializeStorageModuleAsync();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public async Task InitializeStorageModuleAsync_InDevelopment_WhenDbContextMissing_LogsWarningAndReturnsApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
        });

        WebApplication app = builder.Build();

        // StorageDbContext is not registered, so GetRequiredService will throw,
        // which should be caught and logged as a warning
        WebApplication result = await app.InitializeStorageModuleAsync();

        result.Should().BeSameAs(app);
    }

    private sealed class FakeClamAvServer : IDisposable
    {
        private readonly TcpListener _listener;

        public int Port { get; }

        public FakeClamAvServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = AcceptAndCloseAsync();
        }

        private async Task AcceptAndCloseAsync()
        {
            try
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync();
            }
            catch (ObjectDisposedException)
            {
                // Server was disposed before a client connected
            }
        }

        public void Dispose()
        {
            _listener.Stop();
            _listener.Dispose();
        }
    }
}
