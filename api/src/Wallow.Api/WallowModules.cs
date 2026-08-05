// Infrastructure extensions - canonical source for module registration
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Wallow.Modules.Registry;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Shared.Infrastructure.Plugins;

namespace Wallow.Api;

/// <summary>
/// Registers the Wallow modules this host runs. The modules themselves come from
/// <see cref="WallowModuleRegistry"/> — the one list both hosts read — and are filtered here through
/// <see cref="IFeatureManager"/>.
/// </summary>
internal static partial class WallowModules
{
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
