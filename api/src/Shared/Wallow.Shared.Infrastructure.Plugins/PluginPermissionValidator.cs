using Microsoft.Extensions.Options;
using Wallow.Shared.Kernel.Plugins;

namespace Wallow.Shared.Infrastructure.Plugins;

public sealed class PluginPermissionValidator(
    PluginRegistry registry,
    IOptions<PluginOptions> options) : IPluginPermissionValidator
{
    private readonly PluginOptions _options = options.Value;

    public bool HasPermission(string pluginId, string permission)
    {
        if (!_options.Permissions.TryGetValue(pluginId, out List<string>? configured))
        {
            return false;
        }

        PluginRegistryEntry? entry = registry.GetEntry(pluginId);
        if (entry is null)
        {
            return false;
        }

        return entry.Manifest.RequiredPermissions.Contains(permission)
            && configured.Contains(permission);
    }

    public IReadOnlyList<string> GetGrantedPermissions(string pluginId)
    {
        PluginRegistryEntry? entry = registry.GetEntry(pluginId);
        if (entry is null)
        {
            return [];
        }

        if (!_options.Permissions.TryGetValue(pluginId, out List<string>? configured))
        {
            return [];
        }

        return entry.Manifest.RequiredPermissions
            .Intersect(configured)
            .ToList();
    }
}
