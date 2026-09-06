using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using NSubstitute;
using StackExchange.Redis;
using Wallow.Modules.Registry;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Architecture.Tests.Modules;

/// <summary>
/// Each module registers every catalog declared in its Domain assembly. Calling AddServices
/// directly keeps host feature flags from hiding an omitted registration.
/// </summary>
public class ModuleErrorCatalogTests
{
    public static TheoryData<string> ModuleNames
    {
        get
        {
            TheoryData<string> data = new();
            foreach (IWallowModule module in WallowModuleRegistry.All)
            {
                data.Add(module.Name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void AddServices_RegistersEveryDeclaredErrorCatalog(string moduleName)
    {
        IWallowModule module = WallowModuleRegistry.All.Single(candidate => candidate.Name == moduleName);
        Assembly domain = Assembly.Load($"Wallow.{moduleName}.Domain");
        Type[] declaredCatalogs = domain.GetTypes().Where(DeclaresCatalogEntries).ToArray();
        declaredCatalogs.Should().NotBeEmpty($"{moduleName} must declare its error catalog");
        ServiceCollection services = new();
        services.AddSingleton(Substitute.For<IConnectionMultiplexer>());
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            })
            .Build();
        IHostEnvironment environment = new HostingEnvironment { EnvironmentName = Environments.Development };

        module.AddServices(services, configuration, environment);

        using ServiceProvider provider = services.BuildServiceProvider();
        Type[] registeredCatalogs = provider.GetServices<ErrorCatalogRegistration>()
            .Select(registration => registration.CatalogType)
            .ToArray();
        registeredCatalogs.Should().BeEquivalentTo(
            declaredCatalogs,
            $"{moduleName}.AddServices must register each declared catalog with AddErrorCatalog so its codes reach OpenAPI");
    }

    private static bool DeclaresCatalogEntries(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        return type.GetFields(flags).Any(field => field.FieldType == typeof(ErrorCatalogEntry))
            || type.GetProperties(flags).Any(property =>
                property.PropertyType == typeof(ErrorCatalogEntry) && property.GetIndexParameters().Length == 0);
    }
}
