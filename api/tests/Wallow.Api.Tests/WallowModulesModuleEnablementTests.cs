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
/// The membership semantics <c>Program.cs</c>'s two module gates now evaluate: whether the enabled
/// set <c>AddWallowModules</c> returned contains a given module.
/// </summary>
/// <remarks>
/// <para>
/// The gate is named by TYPE rather than by a flag string on purpose, and that is the guarantee this
/// bead actually buys: <c>IsModuleEnabled&lt;ApiKeysModule&gt;()</c> cannot be misspelled, and a
/// rename of the module class is a compile error at the call site instead of a branch that silently
/// takes the "disabled" path. A test cannot demonstrate that — the compiler is the enforcement. What
/// these facts pin is the part that IS expressible at runtime: the true/false answer for a shipped
/// module, and the refusal to answer at all for a type the registry does not ship.
/// </para>
/// <para>
/// Real production module instances, not a name-carrying stub: the two call sites reference these
/// exact types, so a stub would prove something narrower than what ships.
/// </para>
/// </remarks>
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
        // The failure mode the bead exists to close, stated directly. A module the registry forces on
        // regardless of its flag (IWallowModule.IsCore) is IN the enabled set even though
        // FeatureManagement would answer false for it. The gate reads the set, so it agrees with the
        // registry rather than with the flag — there is no second opinion left to disagree with.
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
        // Unknown module must be loud, not silently false. Returning false here would reintroduce the
        // exact bug in a new costume: a gate that reads "disabled" for a module nobody can switch on.
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

    /// <summary>A module that exists only inside this test assembly, so the registry cannot ship it.</summary>
    private sealed class UnshippedModule : IWallowModule
    {
        public string Name => "Unshipped";

        public bool IsCore => false;

        public IEnumerable<Assembly> HandlerAssemblies => [];

        public IReadOnlyList<Type> DbContextTypes => [];

        public string SchemaName => "unshipped";

        public IServiceCollection AddServices(
            IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment) => services;
    }
}
