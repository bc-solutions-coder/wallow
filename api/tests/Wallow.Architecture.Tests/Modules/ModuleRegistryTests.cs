using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using NSubstitute;
using StackExchange.Redis;
using Wallow.Modules.Registry;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.Architecture.Tests.Modules;

/// <summary>
/// Guards the "one module list" property. Both hosts must source their modules from
/// <see cref="WallowModuleRegistry"/>: <c>Wallow.Api.WallowModules</c> filtered through
/// <c>IFeatureManager</c>, <c>Wallow.MigrationService.ModuleMigrations</c> unfiltered.
/// </summary>
/// <remarks>
/// The identity assertions are what make this suite a real regression guard rather than a
/// tautology. Comparing the two hosts' module *types* only proves the lists agree today, and two
/// hand-maintained arrays with identical contents agree right up until the moment someone edits
/// one of them. Comparing object *identity* fails the instant a host constructs its own
/// <c>new XModule()</c> array again, even one that is byte-for-byte identical to the registry —
/// which is precisely the failure mode the bead describes (adding an eighth module to one list
/// and not the other).
/// </remarks>
public class ModuleRegistryTests
{
    /// <summary>
    /// Every module flag turned on, so the API host's filtered list is expected to be the whole
    /// registry and can be compared against the migration host's unfiltered one.
    /// </summary>
    private static readonly Dictionary<string, string?> _allModuleFlagsEnabled = new()
    {
        ["FeatureManagement:Modules.Identity"] = "true",
        ["FeatureManagement:Modules.Branding"] = "true",
        ["FeatureManagement:Modules.Notifications"] = "true",
        ["FeatureManagement:Modules.Announcements"] = "true",
        ["FeatureManagement:Modules.Storage"] = "true",
        ["FeatureManagement:Modules.ApiKeys"] = "true",
        ["FeatureManagement:Modules.Inquiries"] = "true",
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
    };

    [Fact]
    public void Registry_ShouldContain_ExactlySevenModules_OnePerModuleType()
    {
        IReadOnlyList<string> registeredTypeNames =
            [.. WallowModuleRegistry.All.Select(module => module.GetType().Name)];

        registeredTypeNames.Should().BeEquivalentTo(
            [
                "IdentityModule",
                "BrandingModule",
                "NotificationsModule",
                "AnnouncementsModule",
                "StorageModule",
                "ApiKeysModule",
                "InquiriesModule",
            ],
            "the registry is the platform's one list of modules, so it must name all seven exactly once");
    }

    [Fact]
    public void MigrationServiceHost_AllModules_ShouldBe_TheSameInstancesAs_Registry()
    {
        IReadOnlyList<IWallowModule> migrationHostModules = ReadModuleMigrationsAll();

        IReadOnlyList<string> notFromRegistry =
        [
            .. migrationHostModules
                .Where(module => !IsRegistryInstance(module))
                .Select(module => module.GetType().Name)
        ];

        notFromRegistry.Should().BeEmpty(
            "ModuleMigrations.All must delegate to WallowModuleRegistry.All; any module it hands "
            + "back that is not a registry instance was constructed by a second, drifting list");
    }

    [Fact]
    public void ApiHost_EnabledModules_ShouldBe_TheSameInstancesAs_Registry()
    {
        IReadOnlyList<IWallowModule> enabledModules = InvokeAddWallowModules();

        IReadOnlyList<string> notFromRegistry =
        [
            .. enabledModules
                .Where(module => !IsRegistryInstance(module))
                .Select(module => module.GetType().Name)
        ];

        notFromRegistry.Should().BeEmpty(
            "AddWallowModules must filter WallowModuleRegistry.All rather than its own array; any "
            + "module it returns that is not a registry instance came from a second, drifting list");
    }

    [Fact]
    public void ApiHost_EnabledModules_ShouldMatch_MigrationServiceHost_AllModules_WhenEveryFlagEnabled()
    {
        IReadOnlyList<IWallowModule> enabledModules = InvokeAddWallowModules();
        IReadOnlyList<IWallowModule> migrationHostModules = ReadModuleMigrationsAll();

        enabledModules.Select(module => module.GetType())
            .Should().BeEquivalentTo(
                migrationHostModules.Select(module => module.GetType()),
                "with every flag on, the API host runs exactly the set of modules the migration "
                + "host migrates — a module in one and not the other is either an unmigrated schema "
                + "or an unregistered module");
    }

    private static bool IsRegistryInstance(IWallowModule module)
    {
        return WallowModuleRegistry.All.Any(
            registered => ReferenceEquals(registered, module));
    }

    /// <summary>
    /// Calls <c>Wallow.Api.WallowModules.AddWallowModules</c> and keeps its return value. The class
    /// is internal but the method is public, which reflection reaches without any
    /// <c>InternalsVisibleTo</c> plumbing — the same approach <c>ModuleToggleTests</c> already uses.
    /// </summary>
    private static IReadOnlyList<IWallowModule> InvokeAddWallowModules()
    {
        ServiceCollection services = new();

        // Modules resolve IConnectionMultiplexer at registration time for Redis-backed services
        IConnectionMultiplexer mockRedis = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(mockRedis);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(_allModuleFlagsEnabled)
            .Build();

        Assembly apiAssembly = Assembly.Load("Wallow.Api");
        Type wallowModulesType = apiAssembly.GetType("Wallow.Api.WallowModules")!;
        MethodInfo addMethod = wallowModulesType.GetMethod(
            "AddWallowModules", BindingFlags.Public | BindingFlags.Static)!;
        IHostEnvironment environment = new HostingEnvironment { EnvironmentName = Environments.Development };

        return (IReadOnlyList<IWallowModule>)addMethod.Invoke(
            null, [services, configuration, environment])!;
    }

    /// <summary>
    /// Reads <c>Wallow.MigrationService.ModuleMigrations.All</c>. Same accessibility shape as
    /// <c>WallowModules</c> — internal class, public member — so the same reflection works.
    /// </summary>
    private static IReadOnlyList<IWallowModule> ReadModuleMigrationsAll()
    {
        Assembly migrationAssembly = Assembly.Load("Wallow.MigrationService");
        Type moduleMigrationsType = migrationAssembly.GetType("Wallow.MigrationService.ModuleMigrations")!;
        PropertyInfo allProperty = moduleMigrationsType.GetProperty(
            "All", BindingFlags.Public | BindingFlags.Static)!;

        return (IReadOnlyList<IWallowModule>)allProperty.GetValue(null)!;
    }
}
