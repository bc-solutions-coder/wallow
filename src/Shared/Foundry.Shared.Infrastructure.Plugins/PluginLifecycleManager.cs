using Foundry.Shared.Kernel.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Shared.Infrastructure.Plugins;

public sealed partial class PluginLifecycleManager(
    PluginRegistry registry,
    PluginLoader loader,
    IPluginPermissionValidator permissionValidator,
    IOptions<PluginOptions> options,
    ILogger<PluginLifecycleManager> logger)
{
    private readonly PluginOptions _options = options.Value;

    public Task<IReadOnlyList<PluginManifest>> DiscoverPluginsAsync(string path)
    {
        LogDiscoveringPlugins(logger, path);

        IReadOnlyList<PluginManifest> manifests = PluginManifestLoader.LoadFromDirectory(path);

        foreach (PluginManifest manifest in manifests)
        {
            registry.Register(manifest);
            LogDiscoveredPlugin(logger, manifest.Id, manifest.Version);
        }

        LogDiscoveredPluginCount(logger, manifests.Count);
        return Task.FromResult(manifests);
    }

    public Task<PluginRegistryEntry> LoadPluginAsync(string id)
    {
        PluginRegistryEntry entry = GetEntryOrThrow(id);
        ValidateTransition(entry, PluginLifecycleState.Installed);

        LogLoadingPlugin(logger, id);
        PluginRegistryEntry result = loader.LoadPlugin(entry.Manifest, _options.PluginsDirectory);
        LogPluginLoaded(logger, id);

        return Task.FromResult(result);
    }

    public Task EnablePluginAsync(string id, IServiceCollection services, IConfiguration configuration)
    {
        PluginRegistryEntry entry = GetEntryOrThrow(id);
        ValidateTransition(entry, PluginLifecycleState.Enabled);

        List<string> missingPermissions = entry.Manifest.RequiredPermissions
            .Where(p => !permissionValidator.HasPermission(id, p))
            .ToList();

        if (missingPermissions.Count > 0)
        {
            throw new InvalidOperationException(
                $"Plugin '{id}' requires permissions not granted: {string.Join(", ", missingPermissions)}.");
        }

        LogEnablingPlugin(logger, id);
        entry.Instance!.AddServices(services, configuration);
        registry.UpdateState(id, PluginLifecycleState.Enabled);
        LogPluginEnabled(logger, id);

        return Task.CompletedTask;
    }

    public async Task InitializePluginAsync(string id, IServiceProvider serviceProvider)
    {
        PluginRegistryEntry entry = GetEntryOrThrow(id);

        if (entry.State != PluginLifecycleState.Enabled)
        {
            throw new InvalidOperationException($"Plugin '{id}' must be Enabled before initialization. Current state: {entry.State}.");
        }

        LogInitializingPlugin(logger, id);

        PluginContext context = new PluginContext(
            serviceProvider,
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<ILogger<PluginContext>>());

        await entry.Instance!.InitializeAsync(context);
        LogPluginInitialized(logger, id);
    }

    public async Task DisablePluginAsync(string id)
    {
        PluginRegistryEntry entry = GetEntryOrThrow(id);
        ValidateTransition(entry, PluginLifecycleState.Disabled);

        LogDisablingPlugin(logger, id);
        await entry.Instance!.ShutdownAsync();
        registry.UpdateState(id, PluginLifecycleState.Disabled);
        LogPluginDisabled(logger, id);
    }

    private PluginRegistryEntry GetEntryOrThrow(string id)
    {
        return registry.GetEntry(id)
            ?? throw new InvalidOperationException($"Plugin '{id}' is not registered.");
    }

    private static void ValidateTransition(PluginRegistryEntry entry, PluginLifecycleState target)
    {
        bool valid = (entry.State, target) switch
        {
            (PluginLifecycleState.Discovered, PluginLifecycleState.Installed) => true,
            (PluginLifecycleState.Installed, PluginLifecycleState.Enabled) => true,
            (PluginLifecycleState.Enabled, PluginLifecycleState.Disabled) => true,
            (PluginLifecycleState.Disabled, PluginLifecycleState.Enabled) => true,
            _ => false
        };

        if (!valid)
        {
            throw new InvalidOperationException(
                $"Invalid state transition for plugin '{entry.Manifest.Id}': {entry.State} -> {target}.");
        }
    }

    // Discovery
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Discovering plugins in {Path}")]
    private static partial void LogDiscoveringPlugins(ILogger logger, string path);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Discovered plugin {PluginId} v{Version}")]
    private static partial void LogDiscoveredPlugin(ILogger logger, string pluginId, string version);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Discovered {Count} plugin(s)")]
    private static partial void LogDiscoveredPluginCount(ILogger logger, int count);

    // Loading
    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Loading plugin {PluginId}")]
    private static partial void LogLoadingPlugin(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Plugin {PluginId} loaded successfully")]
    private static partial void LogPluginLoaded(ILogger logger, string pluginId);

    // Activation
    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Enabling plugin {PluginId}")]
    private static partial void LogEnablingPlugin(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Plugin {PluginId} enabled")]
    private static partial void LogPluginEnabled(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Initializing plugin {PluginId}")]
    private static partial void LogInitializingPlugin(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Plugin {PluginId} initialized successfully")]
    private static partial void LogPluginInitialized(ILogger logger, string pluginId);

    // Deactivation
    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Disabling plugin {PluginId}")]
    private static partial void LogDisablingPlugin(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Plugin {PluginId} disabled")]
    private static partial void LogPluginDisabled(ILogger logger, string pluginId);
}
