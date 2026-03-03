using Foundry.Shared.Kernel.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Shared.Infrastructure.Plugins;

public static partial class PluginServiceExtensions
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load plugin {PluginId}")]
    private static partial void LogFailedToLoadPlugin(ILogger logger, string pluginId, Exception ex);

    public static IServiceCollection AddFoundryPlugins(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PluginOptions>(configuration.GetSection(PluginOptions.SectionName));
        services.AddSingleton<PluginRegistry>();
        services.AddSingleton<IPluginPermissionValidator, PluginPermissionValidator>();
        services.AddSingleton<PluginLoader>();
        services.AddSingleton<PluginLifecycleManager>();

        return services;
    }

    public static async Task InitializeFoundryPluginsAsync(this WebApplication app)
    {
        PluginLifecycleManager lifecycleManager = app.Services.GetRequiredService<PluginLifecycleManager>();
        PluginOptions options = app.Services.GetRequiredService<IOptions<PluginOptions>>().Value;
        ILogger<PluginLifecycleManager> logger = app.Services.GetRequiredService<ILogger<PluginLifecycleManager>>();

        IReadOnlyList<PluginManifest> manifests = await lifecycleManager.DiscoverPluginsAsync(options.PluginsDirectory);

        if (!options.AutoEnable || manifests.Count == 0)
        {
            return;
        }

        foreach (PluginManifest manifest in manifests)
        {
            try
            {
                await lifecycleManager.LoadPluginAsync(manifest.Id);
            }
            catch (Exception ex)
            {
                LogFailedToLoadPlugin(logger, manifest.Id, ex);
            }
        }
    }
}
