using System.Reflection;
using Foundry.Announcements.Infrastructure.Persistence;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Messaging.Infrastructure.Persistence;
using Foundry.Notifications.Infrastructure.Persistence;
using Foundry.Storage.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Architecture.Tests.Modules;

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

        InvokeAddFoundryModules(services, configuration);

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

        InvokeAddFoundryModules(services, configuration);

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

    private static void InvokeAddFoundryModules(IServiceCollection services, IConfiguration configuration)
    {
        Assembly apiAssembly = Assembly.Load("Foundry.Api");
        Type foundryModulesType = apiAssembly.GetType("Foundry.Api.FoundryModules")!;
        MethodInfo addMethod = foundryModulesType.GetMethod(
            "AddFoundryModules", BindingFlags.Public | BindingFlags.Static)!;
        addMethod.Invoke(null, [services, configuration]);
    }
}
