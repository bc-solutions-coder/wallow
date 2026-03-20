namespace Wallow.Shared.Kernel.Plugins;

public interface IPluginPermissionValidator
{
    bool HasPermission(string pluginId, string permission);
    IReadOnlyList<string> GetGrantedPermissions(string pluginId);
}
