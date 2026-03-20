using Wallow.Shared.Infrastructure.Workflows.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Wallow.Shared.Infrastructure.Tests.Workflows;

public class ElsaExtensionsTests
{
    private static IConfiguration CreateConfigurationWithConnectionString()
    {
        Dictionary<string, string?> settings = new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    [Fact]
    public void AddWallowWorkflows_RegistersElsaServices_InServiceCollection()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfigurationWithConnectionString();
        IHostEnvironment environment = CreateEnvironment(Environments.Development);

        services.AddWallowWorkflows(configuration, environment);

        // Elsa registers many services; verify some well-known Elsa types are present
        List<Type> registeredServiceTypes = services.Select(sd => sd.ServiceType).ToList();

        registeredServiceTypes.Should().Contain(t => t.FullName != null && t.FullName.Contains("Elsa"),
            "AddElsa should register Elsa-namespaced services");
    }

    [Fact]
    public void AddWallowWorkflows_WithoutConnectionString_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        IHostEnvironment environment = CreateEnvironment(Environments.Development);

        Action act = () => services.AddWallowWorkflows(configuration, environment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DefaultConnection*");
    }

    [Fact]
    public void AddWallowWorkflows_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfigurationWithConnectionString();
        IHostEnvironment environment = CreateEnvironment(Environments.Development);

        IServiceCollection result = services.AddWallowWorkflows(configuration, environment);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddWallowWorkflows_RegistersMultipleElsaServices()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfigurationWithConnectionString();
        IHostEnvironment environment = CreateEnvironment(Environments.Development);

        services.AddWallowWorkflows(configuration, environment);

        int elsaServiceCount = services.Count(sd =>
            sd.ServiceType.FullName != null && sd.ServiceType.FullName.Contains("Elsa"));

        elsaServiceCount.Should().BeGreaterThan(10,
            "AddElsa with management, runtime, identity, scheduling, http, and email should register many services");
    }

    [Fact]
    public void AddWallowWorkflows_InDevelopment_WithoutSigningKey_UsesDefaultKey()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfigurationWithConnectionString();
        IHostEnvironment environment = CreateEnvironment(Environments.Development);

        Action act = () => services.AddWallowWorkflows(configuration, environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddWallowWorkflows_InProduction_WithoutSigningKey_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfigurationWithConnectionString();
        IHostEnvironment environment = CreateEnvironment(Environments.Production);

        Action act = () => services.AddWallowWorkflows(configuration, environment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SigningKey*");
    }

    [Fact]
    public void AddWallowWorkflows_InStaging_WithoutSigningKey_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfigurationWithConnectionString();
        IHostEnvironment environment = CreateEnvironment(Environments.Staging);

        Action act = () => services.AddWallowWorkflows(configuration, environment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SigningKey*");
    }

    [Fact]
    public void AddWallowWorkflows_InProduction_WithSigningKey_DoesNotThrow()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> settings = new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Elsa:Identity:SigningKey"] = "my-production-signing-key-that-is-configured"
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        IHostEnvironment environment = CreateEnvironment(Environments.Production);

        Action act = () => services.AddWallowWorkflows(configuration, environment);

        act.Should().NotThrow();
    }
}
