using System.Reflection;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Messaging.Infrastructure.Persistence;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Architecture.Tests.Modules;

public class ModuleToggleTests
{
    [Fact]
    public void DisabledModule_ShouldNotRegister_Services()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:Modules.Identity"] = "false",
                ["FeatureManagement:Modules.Billing"] = "true",
                ["FeatureManagement:Modules.Notifications"] = "true",
                ["FeatureManagement:Modules.Messaging"] = "true",
                ["FeatureManagement:Modules.Announcements"] = "true",
                ["FeatureManagement:Modules.Storage"] = "true",
                ["FeatureManagement:Modules.Inquiries"] = "true",
                ["FeatureManagement:Modules.Showcases"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            })
            .Build();

        InvokeAddWallowModules(services, configuration);

        bool hasIdentityDbContext = services.Any(
            sd => sd.ServiceType == typeof(IdentityDbContext));

        hasIdentityDbContext.Should().BeFalse(
            "Identity module is disabled and should not register any services");
    }

    [Fact]
    public void AllModulesEnabled_ShouldRegister_AllModules()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:Modules.Identity"] = "true",
                ["FeatureManagement:Modules.Billing"] = "true",
                ["FeatureManagement:Modules.Notifications"] = "true",
                ["FeatureManagement:Modules.Messaging"] = "true",
                ["FeatureManagement:Modules.Announcements"] = "true",
                ["FeatureManagement:Modules.Storage"] = "true",
                ["FeatureManagement:Modules.Inquiries"] = "true",
                ["FeatureManagement:Modules.Showcases"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            })
            .Build();

        InvokeAddWallowModules(services, configuration);

        services.Should().Contain(sd => sd.ServiceType == typeof(IdentityDbContext),
            "Identity module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(BillingDbContext),
            "Billing module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(NotificationsDbContext),
            "Notifications module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(MessagingDbContext),
            "Messaging module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(AnnouncementsDbContext),
            "Announcements module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(StorageDbContext),
            "Storage module should be registered by default");
    }

    private static void InvokeAddWallowModules(IServiceCollection services, IConfiguration configuration)
    {
        Assembly apiAssembly = Assembly.Load("Wallow.Api");
        Type wallowModulesType = apiAssembly.GetType("Wallow.Api.WallowModules")!;
        MethodInfo addMethod = wallowModulesType.GetMethod(
            "AddWallowModules", BindingFlags.Public | BindingFlags.Static)!;
        addMethod.Invoke(null, [services, configuration]);
    }
}
