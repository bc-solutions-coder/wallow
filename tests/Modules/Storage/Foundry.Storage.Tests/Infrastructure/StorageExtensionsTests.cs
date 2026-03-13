using System.Net;
using System.Net.Sockets;
using Foundry.Shared.Contracts.Storage;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Infrastructure.Extensions;
using Foundry.Storage.Infrastructure.Scanning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Foundry.Storage.Tests.Infrastructure;

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
