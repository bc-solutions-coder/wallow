using Wallow.Shared.Kernel.Plugins;

namespace Wallow.Shared.Infrastructure.Plugins;

public sealed class PluginRegistryEntry
{
    public PluginManifest Manifest { get; }
    public PluginLifecycleState State { get; internal set; }
    public IWallowPlugin? Instance { get; internal set; }
    public PluginAssemblyLoadContext? LoadContext { get; internal set; }

    public PluginRegistryEntry(PluginManifest manifest)
    {
        Manifest = manifest;
        State = PluginLifecycleState.Discovered;
    }
}
