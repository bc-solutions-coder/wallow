using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.Shared.Infrastructure.Core.Auditing;
namespace Wallow.Shared.Infrastructure.Tests.Auditing;

public class AuditingExtensionsAdditionalTests
{
    private static IConfiguration CreateConfiguration(string connectionString = "Host=localhost;Database=test") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

    [Fact]
    public void AddWallowAuditing_RegistersLoggingServices()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);

        services.Should().Contain(sd => sd.ServiceType == typeof(ILoggerFactory));
    }

    [Fact]
    public void AddWallowAuditing_CanBeCalledMultipleTimes_WithoutThrowingException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        Action act = () =>
        {
            services.AddWallowAuditing(configuration);
            services.AddWallowAuditing(configuration);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task InitializeAppAuditingAsync_InNonDevelopmentEnvironment_SkipsMigration()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid;Database=nonexistent"
        });
        builder.Services.AddWallowAuditing(builder.Configuration);
        WebApplication app = builder.Build();

        // Should not throw even with an invalid connection string because migration is skipped
        Func<Task> act = () => app.InitializeAppAuditingAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAppAuditingAsync_InStagingEnvironment_SkipsMigration()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Staging;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid;Database=nonexistent"
        });
        builder.Services.AddWallowAuditing(builder.Configuration);
        WebApplication app = builder.Build();

        Func<Task> act = () => app.InitializeAppAuditingAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAppAuditingAsync_WhenMigrationFails_DoesNotThrow()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Invalid connection string will cause migration to fail
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid_host_that_does_not_exist;Database=nonexistent;Timeout=1"
        });
        builder.Services.AddWallowAuditing(builder.Configuration);
        WebApplication app = builder.Build();

        Func<Task> act = () => app.InitializeAppAuditingAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void AddWallowAuditing_WithEmptyConnectionString_DoesNotThrowDuringRegistration()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(string.Empty);

        Action act = () => services.AddWallowAuditing(configuration);

        act.Should().NotThrow();
    }

}
