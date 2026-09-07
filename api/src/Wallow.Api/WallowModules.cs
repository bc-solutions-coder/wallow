
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
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
/// Registers core and configured optional modules from <see cref="WallowModuleRegistry"/>.
/// </summary>
internal static partial class WallowModules
{
    /// <summary>
    /// Maps module names to controller assemblies. Keeping this HTTP map in the API host
    /// avoids adding MVC dependencies to the shared registry.
    /// </summary>
    private static readonly (string ModuleName, Assembly ApiAssembly)[] _moduleApiAssemblies =
    [
        ("Identity", typeof(UsersController).Assembly),
        ("Branding", typeof(OrganizationClientBrandingController).Assembly),
        ("Notifications", typeof(NotificationsController).Assembly),
        ("Storage", typeof(StorageController).Assembly),
        ("ApiKeys", typeof(ApiKeysController).Assembly),
        ("Inquiries", typeof(InquiriesController).Assembly),
    ];

    /// <summary>
    /// Registers the enabled module set and plugins. Pass the returned set to later host setup.
    /// </summary>
    public static IReadOnlyList<IWallowModule> AddWallowModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Make configuration and feature management available to module services.
        services.AddSingleton(configuration);
        services.AddFeatureManagement();

        IReadOnlyList<IWallowModule> enabledModules = ResolveEnabledModules(configuration);

        foreach (IWallowModule module in enabledModules)
        {
            module.AddServices(services, configuration, environment);
        }


        services.AddWallowPlugins(configuration);

        return enabledModules;
    }

    public static async Task InitializeWallowModulesAsync(
        this WebApplication app,
        IReadOnlyList<IWallowModule> enabledModules)
    {
        // Testing hosts migrate their fresh databases without a separate migration worker.
        if (app.Environment.IsEnvironment("Testing"))
        {
            await RunTestMigrationsAsync(app.Services, enabledModules);
        }


        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(WallowModules));
        string moduleNames = string.Join(", ", enabledModules.Select(module => module.Name));
        LogEnabledModules(logger, moduleNames);


        await app.InitializeWallowPluginsAsync();
    }

    /// <summary>
    /// Removes mapped controller assemblies for disabled modules from the populated part manager.
    /// Host and shared assemblies outside the module map are retained.
    /// </summary>
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
    /// Checks exact module-type membership in the enabled set.
    /// Throws for types not shipped by <see cref="WallowModuleRegistry"/>.
    /// </summary>
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

        // Exact types prevent a shared interface from matching multiple modules.
        static bool IsTModule(IWallowModule module) => module.GetType() == typeof(TModule);
    }

    /// <summary>
    /// Rejects module-name mismatches between the shipped registry and controller-assembly map.
    /// </summary>
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
    /// Selects core modules and optional modules whose configuration flag is true.
    /// </summary>
    private static IReadOnlyList<IWallowModule> ResolveEnabledModules(IConfiguration configuration) =>
    [
        .. WallowModuleRegistry.All.Where(module =>
            module.IsCore || IsModuleFlagEnabled(configuration, module.Name))
    ];

    /// <summary>
    /// Reads FeatureManagement:Modules.{name} as a scalar boolean. Missing flags are false;
    /// object values and invalid boolean strings throw.
    /// </summary>
    private static bool IsModuleFlagEnabled(IConfiguration configuration, string moduleName)
    {
        string key = $"FeatureManagement:Modules.{moduleName}";
        IConfigurationSection section = configuration.GetSection(key);

        if (!section.Exists())
        {
            return false;
        }

        if (section.Value is string value && bool.TryParse(value, out bool enabled))
        {
            return enabled;
        }

        string actual = section.Value is null
            ? $"an object whose child keys are [{string.Join(", ", section.GetChildren().Select(child => child.Key))}]"
            : $"'{section.Value}'";

        throw new InvalidOperationException(
            $"Configuration key '{key}' must be a scalar boolean, and is {actual}. A module flag is " +
            "read once at startup, directly off IConfiguration, and decides whether that module's " +
            "services, message handlers and HTTP endpoints exist at all — there is no request in " +
            "flight for a Microsoft.FeatureManagement filter (EnabledFor, percentage, targeting, " +
            "time window) to evaluate against, so a filter here would never run. Set true or false, " +
            "and gate request-scoped behaviour inside the module instead of switching the module " +
            "off. The host refuses to start rather than read this as 'disabled', because a disabled " +
            "module loses its endpoints silently.");
    }

    /// <summary>
    /// Migrates enabled core contexts sequentially, then auth audit, then feature contexts in parallel.
    /// </summary>
    internal static async Task RunTestMigrationsAsync(
        IServiceProvider services,
        IReadOnlyList<IWallowModule> enabledModules)
    {
        ArgumentNullException.ThrowIfNull(enabledModules);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;

        // Core schemas must exist before feature migrations.
        IEnumerable<Type> coreContextTypes = enabledModules
            .Where(module => module.IsCore)
            .SelectMany(module => module.DbContextTypes);

        foreach (Type contextType in coreContextTypes)
        {
            await MigrateContextAsync(sp, contextType);
        }

        // Auth auditing is shared infrastructure outside the module registry.
        await sp.GetRequiredService<AuthAuditDbContext>().Database.MigrateAsync();

        // Enabled feature modules declare the contexts registered above.
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
    /// Resolves and migrates the registered DbContext type declared by a module.
    /// </summary>
    private static Task MigrateContextAsync(IServiceProvider sp, Type contextType) =>
        ((DbContext)sp.GetRequiredService(contextType)).Database.MigrateAsync();

    [LoggerMessage(Level = LogLevel.Information, Message = "Wallow modules enabled: {EnabledModules}")]
    private static partial void LogEnabledModules(ILogger logger, string enabledModules);
}
