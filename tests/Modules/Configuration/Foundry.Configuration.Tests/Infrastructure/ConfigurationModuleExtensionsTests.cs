using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Infrastructure.Extensions;
using Foundry.Configuration.Infrastructure.Persistence;
using Foundry.Configuration.Infrastructure.Services;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Configuration.Tests.Infrastructure;

public class ConfigurationModuleExtensionsTests
{
    private static (ServiceCollection Services, IConfiguration Configuration) CreateServices()
    {
        ServiceCollection services = new();

        // Required dependencies
        Dictionary<string, string?> configValues = new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        services.AddSingleton(configuration);
        services.AddSingleton(configuration);
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton(Substitute.For<IDistributedCache>());
        services.AddSingleton(new TenantSaveChangesInterceptor(new TenantContext()));
        services.AddLogging();

        return (services, configuration);
    }

    [Fact]
    public void AddConfigurationModule_RegistersDbContext()
    {
        (ServiceCollection services, IConfiguration configuration) = CreateServices();
        services.AddConfigurationModule(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        ConfigurationDbContext? dbContext = scope.ServiceProvider.GetService<ConfigurationDbContext>();
        dbContext.Should().NotBeNull();
    }

    [Fact]
    public void AddConfigurationModule_RegistersCustomFieldDefinitionRepository()
    {
        (ServiceCollection services, IConfiguration configuration) = CreateServices();
        services.AddConfigurationModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ICustomFieldDefinitionRepository));
        descriptor.Should().NotBeNull();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddConfigurationModule_RegistersFeatureFlagRepository()
    {
        (ServiceCollection services, IConfiguration configuration) = CreateServices();
        services.AddConfigurationModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IFeatureFlagRepository));
        descriptor.Should().NotBeNull();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddConfigurationModule_RegistersFeatureFlagOverrideRepository()
    {
        (ServiceCollection services, IConfiguration configuration) = CreateServices();
        services.AddConfigurationModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IFeatureFlagOverrideRepository));
        descriptor.Should().NotBeNull();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddConfigurationModule_RegistersFeatureFlagServiceAsCached()
    {
        (ServiceCollection services, IConfiguration configuration) = CreateServices();
        services.AddConfigurationModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IFeatureFlagService));
        descriptor.Should().NotBeNull();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddConfigurationModule_RegistersFeatureFlagServiceConcrete()
    {
        (ServiceCollection services, IConfiguration configuration) = CreateServices();
        services.AddConfigurationModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(FeatureFlagService));
        descriptor.Should().NotBeNull();
    }
}
