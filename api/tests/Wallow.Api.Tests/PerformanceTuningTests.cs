using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wallow.Tests.Common.Factories;

namespace Wallow.Api.Tests;

[Collection(nameof(PerformanceTuningTestCollection))]
[Trait("Category", "Integration")]
public sealed class PerformanceTuningTests
{
    private readonly WallowApiFactory _baseFactory;

    public PerformanceTuningTests(WallowApiFactory factory)
    {
        _baseFactory = factory;
    }

    [Fact]
    public void ConfigureKestrel_ZeroAndNullPerformanceValues_DoesNotOverrideDefaults()
    {

        using WebApplicationFactory<Program> customFactory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Performance:ThreadPoolMinWorkerThreads"] = "0",
                    ["Performance:KestrelMaxConcurrentConnections"] = null,
                    ["Performance:KestrelMaxConcurrentUpgradedConnections"] = null,
                });
            });
        });

        // Resolving Services builds the test host.
        IServiceProvider services = customFactory.Services;

        KestrelServerOptions kestrelOptions = services
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;


        kestrelOptions.Limits.MaxConcurrentConnections.Should().BeNull();
        kestrelOptions.Limits.MaxConcurrentUpgradedConnections.Should().BeNull();


        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        IConfigurationSection performanceSection = configuration.GetSection(PerformanceOptions.SectionName);
        performanceSection.Exists().Should().BeTrue();
        performanceSection["ThreadPoolMinWorkerThreads"].Should().Be("0");
    }

    [Fact]
    public void ConnectionStrings_ReadReplicaConnection_ExistsInConfiguration()
    {
        IConfiguration configuration = _baseFactory.Services.GetRequiredService<IConfiguration>();

        string? readReplicaConnection = configuration.GetConnectionString("ReadReplicaConnection");

        readReplicaConnection.Should().NotBeNullOrEmpty(
            "appsettings.json must define ConnectionStrings:ReadReplicaConnection for read replica support");
    }

    [Fact]
    public void ConfigureKestrel_NonZeroConnectionLimits_AppliesLimitsToKestrelOptions()
    {
        using WebApplicationFactory<Program> customFactory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Performance:KestrelMaxConcurrentConnections"] = "100",
                    ["Performance:KestrelMaxConcurrentUpgradedConnections"] = "50",
                });
            });
        });

        IServiceProvider services = customFactory.Services;

        KestrelServerOptions kestrelOptions = services
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;


        kestrelOptions.Limits.MaxConcurrentConnections.Should().Be(100);
        kestrelOptions.Limits.MaxConcurrentUpgradedConnections.Should().Be(50);
    }
}

[CollectionDefinition(nameof(PerformanceTuningTestCollection))]
public class PerformanceTuningTestCollection : ICollectionFixture<WallowApiFactory>
{
}
