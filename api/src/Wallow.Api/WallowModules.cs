// Infrastructure extensions - canonical source for module registration
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Wallow.Announcements.Api.Controllers;
using Wallow.ApiKeys.Api.Controllers;
using Wallow.Branding.Api.Controllers;
using Wallow.Identity.Api.Controllers;
using Wallow.Inquiries.Api.Controllers;
using Wallow.Modules.Registry;
using Wallow.Notifications.Api.Controllers;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Shared.Infrastructure.Plugins;
using Wallow.Storage.Api.Controllers;

namespace Wallow.Api;

/// <summary>
/// Registers the Wallow modules this host runs. The modules themselves come from
/// <see cref="WallowModuleRegistry"/> — the one list both hosts read — and are filtered here through
/// <see cref="IFeatureManager"/>.
/// </summary>
internal static partial class WallowModules
{
    /// <summary>
    /// Anchors each module's HTTP-carrying <c>.Api</c> assembly on one of that module's own controller
    /// types, keyed by <see cref="IWallowModule.Name"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This lives in the host rather than on <see cref="IWallowModule"/> on purpose.
    /// <see cref="WallowModuleRegistry"/> deliberately references only Infrastructure projects so both
    /// hosts can read it; putting an <c>.Api</c> assembly on the module interface would drag
    /// Microsoft.AspNetCore.Mvc into <c>Wallow.MigrationService</c>, which has no HTTP surface at all.
    /// <c>Wallow.Api</c> is the one host with an HTTP surface, and it already references every module's
    /// <c>.Api</c> project, so the table costs nothing here.
    /// </para>
    /// <para>
    /// Keyed by name rather than zipped positionally against <see cref="WallowModuleRegistry.All"/>, so
    /// reordering the registry cannot silently re-point a module at another module's assembly. Anchored
    /// on a real controller type rather than an assembly-name string, so renaming or deleting one is a
    /// compile error here instead of a route that quietly disappears.
    /// </para>
    /// </remarks>
    private static readonly (string ModuleName, Assembly ApiAssembly)[] _moduleApiAssemblies =
    [
        ("Identity", typeof(UsersController).Assembly),
        ("Branding", typeof(ClientBrandingController).Assembly),
        ("Notifications", typeof(NotificationsController).Assembly),
        ("Announcements", typeof(AnnouncementsController).Assembly),
        ("Storage", typeof(StorageController).Assembly),
        ("ApiKeys", typeof(ApiKeysController).Assembly),
        ("Inquiries", typeof(InquiriesController).Assembly),
    ];

    /// <summary>
    /// Registers every enabled module and returns the enabled set.
    /// </summary>
    /// <remarks>
    /// The return value is the point of this method as much as the registration is.
    /// <see cref="IFeatureManager"/> used to be resolved twice from two different providers — once
    /// here off a temporary provider built from the half-finished <see cref="IServiceCollection"/>,
    /// and again in <c>InitializeWallowModulesAsync</c> off the final <c>app.Services</c> — so
    /// "which modules are on" was answered independently in two places and inferred a third time by
    /// Wolverine's assembly scan. Callers now compute it once, here, and pass the result on.
    /// </remarks>
    public static IReadOnlyList<IWallowModule> AddWallowModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Ensure IConfiguration and FeatureManagement are registered before building the temp provider.
        // This makes AddWallowModules self-contained (works even without prior DI setup in tests).
        services.AddSingleton(configuration);
        services.AddFeatureManagement();

        IReadOnlyList<IWallowModule> enabledModules = ResolveEnabledModules(services);

        foreach (IWallowModule module in enabledModules)
        {
            module.AddServices(services, configuration, environment);
        }

        // ============================================================================
        // PLUGIN SYSTEM
        // Extensibility via dynamically loaded plugin assemblies
        // ============================================================================
        services.AddWallowPlugins(configuration);

        return enabledModules;
    }

    public static async Task InitializeWallowModulesAsync(
        this WebApplication app,
        IReadOnlyList<IWallowModule> enabledModules)
    {
        // In Testing environment, run EF Core migrations inline since the separate
        // MigrationService (used in production/Aspire) is not available. The test factory
        // spins up a fresh Postgres container with no schema.
        if (app.Environment.IsEnvironment("Testing"))
        {
            await RunTestMigrationsAsync(app.Services, enabledModules);
        }

        // The seven per-module InitializeXModuleAsync calls that used to live here are gone: all
        // seven were no-ops returning Task.FromResult(app). What is worth recording instead is which
        // modules actually booted, which nothing logged before.
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(WallowModules));
        string moduleNames = string.Join(", ", enabledModules.Select(module => module.Name));
        LogEnabledModules(logger, moduleNames);

        // ============================================================================
        // PLUGIN SYSTEM
        // Discover and optionally load plugins from configured directory
        // ============================================================================
        await app.InitializeWallowPluginsAsync();
    }

    /// <summary>
    /// Removes the <see cref="ApplicationPart"/> of every module's <c>.Api</c> assembly that is not
    /// in <paramref name="enabledModules"/>, so a disabled module's controllers never enter the
    /// <c>ActionDescriptorCollection</c> that routing, Asp.Versioning's ApiExplorer and
    /// Microsoft.AspNetCore.OpenApi's document generator all read from.
    /// </summary>
    /// <param name="manager">The host's part manager, already populated by <c>AddControllersWithViews</c>.</param>
    /// <param name="enabledModules">The exact set <c>AddWallowModules</c> registered.</param>
    /// <remarks>
    /// Only an assembly this class explicitly claims as a module's <c>.Api</c> assembly is ever a
    /// removal candidate, so a host-level or shared part is never at risk. Call this from
    /// <c>ConfigureApplicationPartManager</c>, which runs its setup action against the fully populated
    /// part list before anything reads a feature off it.
    /// </remarks>
    internal static void RemoveDisabledModuleApiParts(
        ApplicationPartManager manager,
        IReadOnlyList<IWallowModule> enabledModules)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(enabledModules);

        EnsureEveryShippedModuleIsMapped();

        HashSet<string> enabledNames = enabledModules
            .Select(module => module.Name)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<Assembly> disabledApiAssemblies =
        [
            .. _moduleApiAssemblies
                .Where(entry => !enabledNames.Contains(entry.ModuleName))
                .Select(entry => entry.ApiAssembly),
        ];

        List<ApplicationPart> partsToRemove =
        [
            .. manager.ApplicationParts.Where(part =>
                part is AssemblyPart assemblyPart && disabledApiAssemblies.Contains(assemblyPart.Assembly)),
        ];

        foreach (ApplicationPart part in partsToRemove)
        {
            manager.ApplicationParts.Remove(part);
        }
    }

    /// <summary>
    /// True when <paramref name="enabledModules"/> — the exact set <c>AddWallowModules</c> registered —
    /// contains <typeparamref name="TModule"/>.
    /// </summary>
    /// <typeparam name="TModule">
    /// The module being asked about, named by its own type. A module the platform does not ship is
    /// rejected rather than answered; see the exception below.
    /// </typeparam>
    /// <param name="enabledModules">The exact set <c>AddWallowModules</c> returned.</param>
    /// <returns><see langword="true"/> when that module is one the host registered.</returns>
    /// <remarks>
    /// <para>
    /// The one way left in <c>Wallow.Api</c> to ask "is module X on" outside <c>AddWallowModules</c>
    /// itself. It reads membership in the already-resolved set rather than re-resolving
    /// <see cref="IFeatureManager"/> and re-deriving the <c>"Modules.{Name}"</c> flag key, so a module
    /// the registry forces on regardless of its flag (<see cref="IWallowModule.IsCore"/>) reads as
    /// enabled here too — <c>enabledModules</c> already applied that short-circuit in
    /// <c>ResolveEnabledModules</c>, and this method never re-derives it. There is no second opinion
    /// left that could disagree with the registry.
    /// </para>
    /// <para>
    /// Named by type rather than by module name, and that is the point. A flag string or a name string
    /// can be misspelled or left behind by a rename, and both mistakes compile and read as DISABLED —
    /// which, since a disabled module loses its endpoints, removes working surface silently. Naming the
    /// module class makes both mistakes a compile error at the call site instead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="enabledModules"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TModule"/> is not a module <see cref="WallowModuleRegistry.All"/> ships, so
    /// no configuration could ever enable it. Answering "disabled" would hide that; refusing to answer
    /// reports it.
    /// </exception>
    internal static bool IsModuleEnabled<TModule>(this IReadOnlyList<IWallowModule> enabledModules)
        where TModule : class, IWallowModule
    {
        ArgumentNullException.ThrowIfNull(enabledModules);

        if (!WallowModuleRegistry.All.Any(IsTModule))
        {
            throw new InvalidOperationException(
                $"{typeof(TModule).FullName} is not a module {nameof(WallowModuleRegistry)}." +
                $"{nameof(WallowModuleRegistry.All)} ships, so no configuration can enable it and " +
                "asking whether it is enabled can only ever answer no. Add it to the registry, or ask " +
                "about a module that is in it.");
        }

        return enabledModules.Any(IsTModule);

        // Exact runtime type, not a type test: every module is a sealed class registered as one
        // instance, so this is the identity check the call sites mean. It also keeps the guard above
        // total — passing an interface, or any other type no registry entry actually is, throws rather
        // than matching everything.
        static bool IsTModule(IWallowModule module) => module.GetType() == typeof(TModule);
    }

    /// <summary>
    /// Fails the host when <see cref="WallowModuleRegistry.All"/> and <see cref="_moduleApiAssemblies"/>
    /// disagree about which modules the platform ships.
    /// </summary>
    /// <remarks>
    /// A module missing from the table would be gated everywhere else and still routed here — the exact
    /// bug this gate exists to close, reintroduced for one module and invisible until someone switched
    /// it off in production. Refusing to start is the only outcome that cannot be missed. The reverse
    /// direction (a table entry naming a module the registry no longer has) is caught too: it is dead
    /// weight that would silently stop meaning anything.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The two lists disagree.</exception>
    private static void EnsureEveryShippedModuleIsMapped()
    {
        HashSet<string> mappedNames = _moduleApiAssemblies
            .Select(entry => entry.ModuleName)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> shippedNames = WallowModuleRegistry.All
            .Select(module => module.Name)
            .ToHashSet(StringComparer.Ordinal);

        string unmapped = string.Join(", ", shippedNames.Except(mappedNames, StringComparer.Ordinal).Order(StringComparer.Ordinal));
        string unknown = string.Join(", ", mappedNames.Except(shippedNames, StringComparer.Ordinal).Order(StringComparer.Ordinal));

        if (unmapped.Length > 0 || unknown.Length > 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WallowModules)}.{nameof(_moduleApiAssemblies)} must name exactly the modules in " +
                $"{nameof(WallowModuleRegistry)}.{nameof(WallowModuleRegistry.All)}. " +
                "Shipped but unmapped (their controllers would stay routed while the module is disabled): " +
                $"[{unmapped}]. Mapped but not shipped: [{unknown}].");
        }
    }

    /// <summary>
    /// Resolves the feature flags once, off a temporary provider built from the services registered
    /// so far. The provider is deliberately not disposed: <c>services.AddSingleton(configuration)</c>
    /// registers an existing instance, and disposing the container would dispose the host's own
    /// <see cref="IConfiguration"/> with it.
    /// </summary>
    private static IReadOnlyList<IWallowModule> ResolveEnabledModules(IServiceCollection services)
    {
        ServiceProvider tempProvider = services.BuildServiceProvider();
        IFeatureManager featureManager = tempProvider.GetRequiredService<IFeatureManager>();

        return
        [
            .. WallowModuleRegistry.All.Where(module =>
                module.IsCore
                || featureManager.IsEnabledAsync($"Modules.{module.Name}").GetAwaiter().GetResult())
        ];
    }

    /// <summary>
    /// Migrates every context the Testing host needs, given the modules the host actually enabled.
    /// </summary>
    /// <param name="services">The host's root service provider.</param>
    /// <param name="enabledModules">
    /// The exact set <c>AddWallowModules</c> registered. Every module-owned context comes from here;
    /// the two auditing contexts belong to no module and stay named explicitly.
    /// </param>
    /// <remarks>
    /// Internal rather than private so a test can drive it with a module list of its own — the only
    /// way to prove that a module the method does not name is still migrated.
    /// </remarks>
    internal static async Task RunTestMigrationsAsync(
        IServiceProvider services,
        IReadOnlyList<IWallowModule> enabledModules)
    {
        ArgumentNullException.ThrowIfNull(enabledModules);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;

        // Core modules migrate first and sequentially, because seeding elsewhere depends on
        // Identity's schema already existing. Identity is the only core module today, so this loop
        // runs exactly the line it replaced.
        IEnumerable<Type> coreContextTypes = enabledModules
            .Where(module => module.IsCore)
            .SelectMany(module => module.DbContextTypes);

        foreach (Type contextType in coreContextTypes)
        {
            await MigrateContextAsync(sp, contextType);
        }

        // The two auditing contexts belong to no module (see IWallowModule.DbContextTypes) and so
        // cannot come from the registry. They stay explicit, in the same position after the core
        // contexts that this method has always run them in.
        await sp.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<AuthAuditDbContext>().Database.MigrateAsync();

        // Feature module contexts, migrated in parallel. The GetService probe this replaced only
        // ever guarded against a module being disabled, which enabledModules already encodes
        // exactly: it is the set AddWallowModules called AddServices on, and no module registers
        // its DbContext conditionally, so every context named here is registered in this provider.
        List<Task> featureMigrations =
        [
            .. enabledModules
                .Where(module => !module.IsCore)
                .SelectMany(module => module.DbContextTypes)
                .Select(contextType => MigrateContextAsync(sp, contextType)),
        ];

        await Task.WhenAll(featureMigrations);
    }

    /// <summary>
    /// Resolves and migrates one context named by <see cref="IWallowModule.DbContextTypes"/>, whose
    /// XML doc contracts that every type in it derives from <see cref="DbContext"/>.
    /// </summary>
    private static Task MigrateContextAsync(IServiceProvider sp, Type contextType) =>
        ((DbContext)sp.GetRequiredService(contextType)).Database.MigrateAsync();

    [LoggerMessage(Level = LogLevel.Information, Message = "Wallow modules enabled: {EnabledModules}")]
    private static partial void LogEnabledModules(ILogger logger, string enabledModules);
}
