using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using NSubstitute;
using StackExchange.Redis;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Persistence;

namespace Wallow.Architecture.Tests.Modules;

public class ModuleToggleTests
{
    [Fact]
    public void CoreModule_ShouldStillRegister_WhenFeatureFlagDisabled()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:Modules.Identity"] = "false",
                ["FeatureManagement:Modules.Notifications"] = "true",
                ["FeatureManagement:Modules.Announcements"] = "true",
                ["FeatureManagement:Modules.Storage"] = "true",
                ["FeatureManagement:Modules.Inquiries"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            })
            .Build();

        InvokeAddWallowModules(services, configuration);

        // Identity is a required platform dependency — always registered even when the feature flag is false
        bool hasIdentityDbContext = services.Any(
            sd => sd.ServiceType == typeof(IdentityDbContext));

        hasIdentityDbContext.Should().BeTrue(
            "Identity module is a required platform dependency and should always be registered");
    }

    [Fact]
    public void OptionalModules_ShouldNotRegister_WhenFeatureFlagsDisabled()
    {
        ServiceCollection services = new();

        // Only the flags under test are set: every optional module is off unless its flag says otherwise,
        // so this covers both an explicit "false" (Storage, Announcements) and an absent flag (the rest).
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:Modules.Storage"] = "false",
                ["FeatureManagement:Modules.Announcements"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            })
            .Build();

        InvokeAddWallowModules(services, configuration);

        services.Should().NotContain(sd => sd.ServiceType == typeof(StorageDbContext),
            "Storage is an optional module and should not be registered when its feature flag is false");
        services.Should().NotContain(sd => sd.ServiceType == typeof(AnnouncementsDbContext),
            "Announcements is an optional module and should not be registered when its feature flag is false");
    }

    [Fact]
    public void AllModulesEnabled_ShouldRegister_AllModules()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:Modules.Identity"] = "true",
                ["FeatureManagement:Modules.Notifications"] = "true",
                ["FeatureManagement:Modules.Announcements"] = "true",
                ["FeatureManagement:Modules.Storage"] = "true",
                ["FeatureManagement:Modules.Inquiries"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            })
            .Build();

        InvokeAddWallowModules(services, configuration);

        services.Should().Contain(sd => sd.ServiceType == typeof(IdentityDbContext),
            "Identity module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(NotificationsDbContext),
            "Notifications module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(AnnouncementsDbContext),
            "Announcements module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(StorageDbContext),
            "Storage module should be registered by default");
    }

    private static void InvokeAddWallowModules(IServiceCollection services, IConfiguration configuration)
    {
        // Modules resolve IConnectionMultiplexer at registration time for Redis-backed services
        IConnectionMultiplexer mockRedis = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(mockRedis);

        Assembly apiAssembly = Assembly.Load("Wallow.Api");
        Type wallowModulesType = apiAssembly.GetType("Wallow.Api.WallowModules")!;
        MethodInfo addMethod = wallowModulesType.GetMethod(
            "AddWallowModules", BindingFlags.Public | BindingFlags.Static)!;
        IHostEnvironment environment = new HostingEnvironment { EnvironmentName = Environments.Development };
        addMethod.Invoke(null, [services, configuration, environment]);
    }
}
