using Wallow.Showcases.Application.Contracts;
using Wallow.Showcases.Infrastructure.Extensions;
using Wallow.Showcases.Infrastructure.Persistence;
using Wallow.Showcases.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wallow.Showcases.Tests.Infrastructure.Extensions;

public class ShowcasesModuleExtensionsTests
{
    private static IConfiguration BuildTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();

    [Fact]
    public void AddShowcasesModule_RegistersShowcaseRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildTestConfiguration();

        services.AddShowcasesModule(configuration);

        ServiceDescriptor? repositoryDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IShowcaseRepository) &&
            d.ImplementationType == typeof(ShowcaseRepository));

        repositoryDescriptor.Should().NotBeNull();
        repositoryDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddShowcasesModule_RegistersShowcasesDbContext()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildTestConfiguration();

        services.AddShowcasesModule(configuration);

        ServiceDescriptor? dbContextDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ShowcasesDbContext));

        dbContextDescriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddShowcasesModule_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildTestConfiguration();

        IServiceCollection result = services.AddShowcasesModule(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public async Task InitializeShowcasesModuleAsync_InNonDevelopmentEnvironment_DoesNotMigrate()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
        });
        builder.Services.AddShowcasesModule(builder.Configuration);

        WebApplication app = builder.Build();

        // In Production, no migration is attempted (avoids network call)
        Func<Task> act = async () => await app.InitializeShowcasesModuleAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeShowcasesModuleAsync_InDevelopmentEnvironment_CatchesConnectionException()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;

        // Invalid connection string - will fail to connect but exception is swallowed
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid-host-xyz;Database=test;Username=test;Password=test;Connect Timeout=1"
        });
        builder.Services.AddShowcasesModule(builder.Configuration);
        builder.Logging.ClearProviders().AddConsole();

        WebApplication app = builder.Build();

        Func<Task> act = async () => await app.InitializeShowcasesModuleAsync();

        // Exception is caught internally and logged as warning
        await act.Should().NotThrowAsync();
    }
}
