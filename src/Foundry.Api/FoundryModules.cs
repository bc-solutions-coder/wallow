// Infrastructure extensions - canonical source for module registration
using Foundry.Announcements.Infrastructure.Extensions;
using Foundry.Billing.Infrastructure.Extensions;
using Foundry.Identity.Infrastructure.Extensions;
using Foundry.Inquiries.Infrastructure.Extensions;
using Foundry.Messaging.Infrastructure.Extensions;
using Foundry.Notifications.Infrastructure.Extensions;
using Foundry.Shared.Infrastructure.Plugins;
using Foundry.Showcases.Infrastructure.Extensions;
using Foundry.Storage.Infrastructure.Extensions;
using Microsoft.FeatureManagement;

namespace Foundry.Api;

/// <summary>
/// Central registry for all Foundry modules.
/// Each module provides AddXxxModule() and InitializeXxxModuleAsync() extension methods.
/// </summary>
internal static class FoundryModules
{
    public static IServiceCollection AddFoundryModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Ensure IConfiguration and FeatureManagement are registered before building the temp provider.
        // This makes AddFoundryModules self-contained (works even without prior DI setup in tests).
        services.AddSingleton(configuration);
        services.AddFeatureManagement();
        ServiceProvider tempProvider = services.BuildServiceProvider();
        IFeatureManager featureManager = tempProvider.GetRequiredService<IFeatureManager>();

        // ============================================================================
        // PLATFORM MODULES
        // Core infrastructure services used across all domain modules
        // ============================================================================
        if (featureManager.IsEnabledAsync("Modules.Identity").GetAwaiter().GetResult())
        {
            services.AddIdentityModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.Billing").GetAwaiter().GetResult())
        {
            services.AddBillingModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.Notifications").GetAwaiter().GetResult())
        {
            services.AddNotificationsModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.Messaging").GetAwaiter().GetResult())
        {
            services.AddMessagingModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.Announcements").GetAwaiter().GetResult())
        {
            services.AddAnnouncementsModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.Storage").GetAwaiter().GetResult())
        {
            services.AddStorageModule(configuration);
        }

        // ============================================================================
        // FEATURE MODULES
        // Higher-level application features built on platform and domain modules
        // ============================================================================
        if (featureManager.IsEnabledAsync("Modules.Inquiries").GetAwaiter().GetResult())
        {
            services.AddInquiriesModule(configuration);
        }

        if (featureManager.IsEnabledAsync("Modules.Showcases").GetAwaiter().GetResult())
        {
            services.AddShowcasesModule(configuration);
        }

        // ============================================================================
        // PLUGIN SYSTEM
        // Extensibility via dynamically loaded plugin assemblies
        // ============================================================================
        services.AddFoundryPlugins(configuration);

        return services;
    }

    public static async Task InitializeFoundryModulesAsync(this WebApplication app)
    {
        IFeatureManager featureManager = app.Services.GetRequiredService<IFeatureManager>();

        // ============================================================================
        // PLATFORM MODULES
        // Core infrastructure services - runs DB migrations
        // ============================================================================
        if (await featureManager.IsEnabledAsync("Modules.Identity"))
        {
            await app.InitializeIdentityModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.Billing"))
        {
            await app.InitializeBillingModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.Notifications"))
        {
            await app.InitializeNotificationsModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.Messaging"))
        {
            await app.InitializeMessagingModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.Announcements"))
        {
            await app.InitializeAnnouncementsModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.Storage"))
        {
            await app.InitializeStorageModuleAsync();
        }

        // ============================================================================
        // FEATURE MODULES
        // EF Core modules run migrations
        // ============================================================================
        if (await featureManager.IsEnabledAsync("Modules.Inquiries"))
        {
            await app.InitializeInquiriesModuleAsync();
        }

        if (await featureManager.IsEnabledAsync("Modules.Showcases"))
        {
            await app.InitializeShowcasesModuleAsync();
        }

        // ============================================================================
        // PLUGIN SYSTEM
        // Discover and optionally load plugins from configured directory
        // ============================================================================
        await app.InitializeFoundryPluginsAsync();
    }
}
