// Infrastructure extensions - canonical source for module registration
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Wallow.Announcements.Infrastructure.Extensions;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.ApiKeys.Infrastructure.Extensions;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.Branding.Infrastructure.Extensions;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Inquiries.Infrastructure.Extensions;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.Notifications.Infrastructure.Extensions;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Infrastructure.Plugins;
using Wallow.Storage.Infrastructure.Extensions;
using Wallow.Storage.Infrastructure.Persistence;

namespace Wallow.Api;

/// <summary>
/// Central registry for all Wallow modules.
/// Each module provides AddXxxModule() and InitializeXxxModuleAsync() extension methods.
/// </summary>
internal static class WallowModules
{
    public static IServiceCollection AddWallowModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Ensure IConfiguration and FeatureManagement are registered before building the temp provider.
        // This makes AddWallowModules self-contained (works even without prior DI setup in tests).
        services.AddSingleton(configuration);
        services.AddFeatureManagement();
        ServiceProvider tempProvider = services.BuildServiceProvider();
        IFeatureManager featureManager = tempProvider.GetRequiredService<IFeatureManager>();

        // ============================================================================
        // PLATFORM MODULES
        // Core infrastructure services used across all domain modules
        // ============================================================================
        // Identity is a required platform dependency — always registered, not behind a feature flag
        services.AddIdentityModule(configuration, environment);

        if (featureManager.IsEnabledAsync("Modules.Branding").GetAwaiter().GetResult())
        {
            services.AddBrandingModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.Notifications").GetAwaiter().GetResult())
        {
            services.AddNotificationsModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.Announcements").GetAwaiter().GetResult())
        {
            services.AddAnnouncementsModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.Storage").GetAwaiter().GetResult())
        {
            services.AddStorageModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.ApiKeys").GetAwaiter().GetResult())
        {
            services.AddApiKeysModule(configuration);
        }

        // ============================================================================
        // FEATURE MODULES
        // Higher-level application features built on platform and domain modules
        // ============================================================================
        if (featureManager.IsEnabledAsync("Modules.Inquiries").GetAwaiter().GetResult())
        {
            services.AddInquiriesModule(configuration);
        }

        // ============================================================================
        // PLUGIN SYSTEM
        // Extensibility via dynamically loaded plugin assemblies
        // ============================================================================
        services.AddWallowPlugins(configuration);

        return services;
    }

    public static async Task InitializeWallowModulesAsync(this WebApplication app)
    {
        IFeatureManager featureManager = app.Services.GetRequiredService<IFeatureManager>();

        // In Testing environment, run EF Core migrations inline since the separate
        // MigrationService (used in production/Aspire) is not available. The test factory
        // spins up a fresh Postgres container with no schema.
        if (app.Environment.IsEnvironment("Testing"))
        {
            await RunTestMigrationsAsync(app.Services);
        }

        // ============================================================================
        // PLATFORM MODULES
        // Core infrastructure services
        // ============================================================================
        // Identity is a required platform dependency — always initialized
        await app.InitializeIdentityModuleAsync();

        if (await featureManager.IsEnabledAsync("Modules.Branding"))
        {
            await app.InitializeBrandingModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.Notifications"))
        {
            await app.InitializeNotificationsModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.Announcements"))
        {
            await app.InitializeAnnouncementsModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.Storage"))
        {
            await app.InitializeStorageModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.ApiKeys"))
        {
            await app.InitializeApiKeysModuleAsync();
        }

        // ============================================================================
        // FEATURE MODULES
        // EF Core modules run migrations
        // ============================================================================
        if (await featureManager.IsEnabledAsync("Modules.Inquiries"))
        {
            await app.InitializeInquiriesModuleAsync();
        }

        // ============================================================================
        // PLUGIN SYSTEM
        // Discover and optionally load plugins from configured directory
        // ============================================================================
        await app.InitializeWallowPluginsAsync();
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
}
