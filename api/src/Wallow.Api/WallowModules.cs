// Infrastructure extensions - canonical source for module registration
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Wallow.Announcements.Infrastructure.Modules;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.ApiKeys.Infrastructure.Modules;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.Branding.Infrastructure.Modules;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Modules;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Inquiries.Infrastructure.Modules;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.Notifications.Infrastructure.Modules;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Shared.Infrastructure.Plugins;
using Wallow.Storage.Infrastructure.Modules;
using Wallow.Storage.Infrastructure.Persistence;

namespace Wallow.Api;

/// <summary>
/// Central registry for all Wallow modules. Each module is an <see cref="IWallowModule"/> living in
/// its own Infrastructure assembly; this list is the only place that knows all seven exist.
/// </summary>
internal static partial class WallowModules
{
    /// <summary>
    /// Every module the host can run, in registration order. Identity is <c>IsCore</c> and comes
    /// first because the rest of the platform assumes its schema and services are present.
    /// </summary>
    private static readonly IWallowModule[] _allModules =
    [
        new IdentityModule(),
        new BrandingModule(),
        new NotificationsModule(),
        new AnnouncementsModule(),
        new StorageModule(),
        new ApiKeysModule(),
        new InquiriesModule(),
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
            await RunTestMigrationsAsync(app.Services);
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
            .. _allModules.Where(module =>
                module.IsCore
                || featureManager.IsEnabledAsync($"Modules.{module.Name}").GetAwaiter().GetResult())
        ];
    }

    private static async Task RunTestMigrationsAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;

        // Core contexts must be migrated first (Identity depends on its schema for seeding)
        await sp.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<AuthAuditDbContext>().Database.MigrateAsync();

        // Feature module contexts — only migrate if the module is enabled (registered in DI)
        List<Task> featureMigrations = [];
        MigrateIfRegistered<BrandingDbContext>(sp, featureMigrations);
        MigrateIfRegistered<NotificationsDbContext>(sp, featureMigrations);
        MigrateIfRegistered<AnnouncementsDbContext>(sp, featureMigrations);
        MigrateIfRegistered<StorageDbContext>(sp, featureMigrations);
        MigrateIfRegistered<ApiKeysDbContext>(sp, featureMigrations);
        MigrateIfRegistered<InquiriesDbContext>(sp, featureMigrations);

        await Task.WhenAll(featureMigrations);
    }

    private static void MigrateIfRegistered<TContext>(IServiceProvider sp, List<Task> tasks)
        where TContext : DbContext
    {
        TContext? context = sp.GetService<TContext>();
        if (context is not null)
        {
            tasks.Add(context.Database.MigrateAsync());
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Wallow modules enabled: {EnabledModules}")]
    private static partial void LogEnabledModules(ILogger logger, string enabledModules);
}
