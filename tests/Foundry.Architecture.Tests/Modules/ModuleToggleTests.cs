using System.Reflection;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Configuration.Infrastructure.Persistence;
using Foundry.Identity.Infrastructure.Persistence;
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
                ["Foundry:Modules:Identity"] = "false",
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
    public void NoModulesSection_ShouldRegister_AllModulesByDefault()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            })
            .Build();

        InvokeAddFoundryModules(services, configuration);

        services.Should().Contain(sd => sd.ServiceType == typeof(IdentityDbContext),
            "Identity module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(BillingDbContext),
            "Billing module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(CommunicationsDbContext),
            "Communications module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(StorageDbContext),
            "Storage module should be registered by default");
        services.Should().Contain(sd => sd.ServiceType == typeof(ConfigurationDbContext),
            "Configuration module should be registered by default");
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
