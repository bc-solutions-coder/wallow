// Infrastructure extensions - canonical source for module registration
using Foundry.Billing.Infrastructure.Extensions;
using Foundry.Communications.Infrastructure.Extensions;
using Foundry.Configuration.Infrastructure.Extensions;
using Foundry.Identity.Infrastructure.Extensions;
using Foundry.Shared.Infrastructure.Plugins;
using Foundry.Storage.Infrastructure.Extensions;

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
        IConfigurationSection modules = configuration.GetSection("Foundry:Modules");

        // ============================================================================
        // PLATFORM MODULES
        // Core infrastructure services used across all domain modules
        // ============================================================================
        if (modules.GetValue("Identity", defaultValue: true))
            services.AddIdentityModule(configuration);

        if (modules.GetValue("Billing", defaultValue: true))
            services.AddBillingModule(configuration);

        if (modules.GetValue("Communications", defaultValue: true))
            services.AddCommunicationsModule(configuration);

        if (modules.GetValue("Storage", defaultValue: true))
            services.AddStorageModule(configuration);

        // ============================================================================
        // FEATURE MODULES
        // Higher-level application features built on platform and domain modules
        // ============================================================================
        if (modules.GetValue("Configuration", defaultValue: true))
            services.AddConfigurationModule(configuration);

        // ============================================================================
        // PLUGIN SYSTEM
        // Extensibility via dynamically loaded plugin assemblies
        // ============================================================================
        services.AddFoundryPlugins(configuration);

        return services;
    }

    public static async Task InitializeFoundryModulesAsync(this WebApplication app)
    {
        IConfigurationSection modules = app.Configuration.GetSection("Foundry:Modules");

        // ============================================================================
        // PLATFORM MODULES
        // Core infrastructure services - runs DB migrations
        // ============================================================================
        if (modules.GetValue("Identity", defaultValue: true))
            await app.InitializeIdentityModuleAsync();

        if (modules.GetValue("Billing", defaultValue: true))
            await app.InitializeBillingModuleAsync();

        if (modules.GetValue("Communications", defaultValue: true))
            await app.InitializeCommunicationsModuleAsync();

        if (modules.GetValue("Storage", defaultValue: true))
            await app.InitializeStorageModuleAsync();

        // ============================================================================
        // FEATURE MODULES
        // EF Core modules run migrations
        // ============================================================================
        if (modules.GetValue("Configuration", defaultValue: true))
            await app.InitializeConfigurationModuleAsync();

        // ============================================================================
        // PLUGIN SYSTEM
        // Discover and optionally load plugins from configured directory
        // ============================================================================
        await app.InitializeFoundryPluginsAsync();
    }
}
