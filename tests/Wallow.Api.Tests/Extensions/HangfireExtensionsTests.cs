using Wallow.Api.Extensions;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;

namespace Wallow.Api.Tests.Extensions;

public class HangfireExtensionsTests
{
    private static (IServiceCollection Services, IConfiguration Configuration) CreateTestDependencies()
    {
        ServiceCollection services = new();
        services.AddLogging();

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();

        return (services, configuration);
    }

    [Fact]
    public void AddHangfireServices_RegistersJobStorage()
    {
        (IServiceCollection services, IConfiguration configuration) = CreateTestDependencies();

        services.AddHangfireServices(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        JobStorage? storage = provider.GetService<JobStorage>();
        storage.Should().NotBeNull();
    }

    [Fact]
    public void AddHangfireServices_RegistersBackgroundJobServer()
    {
        (IServiceCollection services, IConfiguration configuration) = CreateTestDependencies();
        int hostedServicesBefore = services.Count(d => d.ServiceType == typeof(IHostedService));

        services.AddHangfireServices(configuration);

        int hostedServicesAfter = services.Count(d => d.ServiceType == typeof(IHostedService));
        hostedServicesAfter.Should().BeGreaterThan(hostedServicesBefore);
    }

    [Fact]
    public void AddHangfireServices_RegistersBackgroundJobClient()
    {
        (IServiceCollection services, IConfiguration configuration) = CreateTestDependencies();

        services.AddHangfireServices(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IBackgroundJobClient? client = provider.GetService<IBackgroundJobClient>();
        client.Should().NotBeNull();
    }

    [Fact]
    public void AddHangfireServices_RegistersRecurringJobManager()
    {
        (IServiceCollection services, IConfiguration configuration) = CreateTestDependencies();

        services.AddHangfireServices(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IRecurringJobManager? manager = provider.GetService<IRecurringJobManager>();
        manager.Should().NotBeNull();
    }

    [Fact]
    public void AddHangfireServices_ReturnsSameServiceCollection()
    {
        (IServiceCollection services, IConfiguration configuration) = CreateTestDependencies();

        IServiceCollection result = services.AddHangfireServices(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddHangfireServices_ConfiguresPostgreSqlStorage()
    {
        (IServiceCollection services, IConfiguration configuration) = CreateTestDependencies();

        services.AddHangfireServices(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        JobStorage? storage = provider.GetService<JobStorage>();
        storage.Should().NotBeNull();
        storage.Should().BeOfType<Hangfire.PostgreSql.PostgreSqlStorage>();
    }

    [Fact]
    public void UseHangfireDashboard_ReturnsSameWebApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        (_, IConfiguration configuration) = CreateTestDependencies();
        builder.Services.AddHangfireServices(configuration);
        WebApplication app = builder.Build();

        WebApplication result = app.UseHangfireDashboard();

        result.Should().BeSameAs(app);
    }
}
