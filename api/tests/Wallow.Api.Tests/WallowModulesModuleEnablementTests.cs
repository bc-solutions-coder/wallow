using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.ApiKeys.Infrastructure.Modules;
using Wallow.Identity.Infrastructure.Modules;
using Wallow.Modules.Registry;
using Wallow.Notifications.Infrastructure.Modules;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Storage.Infrastructure.Modules;

namespace Wallow.Api.Tests;

/// <summary>
/// Checks enabled-set membership for registered module types and rejection of unknown types.
/// </summary>
public sealed class WallowModulesModuleEnablementTests
{
    [Fact]
    public void IsModuleEnabled_IsTrue_WhenTheModuleIsInTheEnabledSet()
    {
        IReadOnlyList<IWallowModule> enabledModules = [new StorageModule(), new ApiKeysModule()];

        enabledModules.IsModuleEnabled<ApiKeysModule>().Should().BeTrue(
            "the gate must answer yes for a module AddWallowModules actually registered");
    }

    [Fact]
    public void IsModuleEnabled_IsFalse_WhenTheModuleIsNotInTheEnabledSet()
    {
        IReadOnlyList<IWallowModule> enabledModules = [new StorageModule(), new NotificationsModule()];

        enabledModules.IsModuleEnabled<ApiKeysModule>().Should().BeFalse(
            "a module the host did not register must read as disabled, whatever its flag says");
    }

    [Fact]
    public void IsModuleEnabled_IsFalse_ForAnEmptyEnabledSet()
    {
        IReadOnlyList<IWallowModule> enabledModules = [];

        enabledModules.IsModuleEnabled<NotificationsModule>().Should().BeFalse(
            "with nothing enabled, no module may read as enabled");
    }

    [Fact]
    public void IsModuleEnabled_ReadsMembershipOnly_NotTheFeatureFlagTheModuleIsNamedAfter()
    {
        // Core-module membership must be honored independently of feature-flag values.
        IWallowModule identity = WallowModuleRegistry.All.Single(module => module is IdentityModule);
        identity.IsCore.Should().BeTrue(
            "this fact is only about a core module; if Identity stopped being one, pick another");

        IReadOnlyList<IWallowModule> enabledModules = [identity];

        enabledModules.IsModuleEnabled<IdentityModule>().Should().BeTrue(
            "a core module is registered whatever its flag says, so the gate must see it as enabled");
    }

    [Fact]
    public void IsModuleEnabled_Throws_ForATypeTheRegistryDoesNotShip()
    {
        // An unregistered type is a caller error, not a disabled module.
        IReadOnlyList<IWallowModule> enabledModules = [new StorageModule()];

        Action asking = () => enabledModules.IsModuleEnabled<UnshippedModule>();

        asking.Should().Throw<InvalidOperationException>(
                "a module type outside WallowModuleRegistry.All can never be enabled, so answering " +
                "'disabled' would hide the mistake instead of reporting it")
            .WithMessage("*UnshippedModule*");
    }

    [Fact]
    public void IsModuleEnabled_Throws_ForANullEnabledSet()
    {
        IReadOnlyList<IWallowModule> enabledModules = null!;

        Action asking = () => enabledModules.IsModuleEnabled<StorageModule>();

        asking.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Module fixture absent from the production registry.
    /// </summary>
    private sealed class UnshippedModule : IWallowModule
    {
        public string Name => "Unshipped";

        public bool IsCore => false;

        public IReadOnlyList<Assembly> HandlerAssemblies => [];

        public IReadOnlyList<Type> DbContextTypes => [];

        public string SchemaName => "unshipped";

        public IServiceCollection AddServices(
            IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment) => services;
    }
}
