using System.Reflection;
using Foundry.Shared.Kernel.Plugins;

namespace Foundry.Shared.Infrastructure.Plugins;

public sealed class PluginLoader(PluginRegistry registry)
{
    public PluginRegistryEntry LoadPlugin(PluginManifest manifest, string pluginsBasePath)
    {
        string pluginDir = Path.Combine(pluginsBasePath, manifest.Id);
        string assemblyPath = Path.Combine(pluginDir, manifest.EntryAssembly);

        PluginAssemblyLoadContext loadContext = new PluginAssemblyLoadContext(pluginDir);

        Assembly assembly;
        try
        {
            assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
        }
        catch (Exception ex)
        {
            loadContext.Unload();
            throw new PluginLoadException(manifest.Id, $"Failed to load assembly '{manifest.EntryAssembly}'.", ex);
        }

        List<Type> pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IFoundryPlugin).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        if (pluginTypes.Count == 0)
        {
            loadContext.Unload();
            throw new PluginLoadException(manifest.Id, $"No IFoundryPlugin implementation found in '{manifest.EntryAssembly}'.");
        }

        if (pluginTypes.Count > 1)
        {
            loadContext.Unload();
            throw new PluginLoadException(manifest.Id, $"Multiple IFoundryPlugin implementations found in '{manifest.EntryAssembly}'. Expected exactly one.");
        }

        IFoundryPlugin plugin = (IFoundryPlugin)(Activator.CreateInstance(pluginTypes[0])
            ?? throw new PluginLoadException(manifest.Id, $"Failed to create instance of '{pluginTypes[0].FullName}'."));

        if (plugin.Manifest.Id != manifest.Id)
        {
            loadContext.Unload();
            throw new PluginLoadException(manifest.Id, $"Plugin manifest ID mismatch: expected '{manifest.Id}', got '{plugin.Manifest.Id}'.");
        }

        registry.SetInstance(manifest.Id, plugin, loadContext);
        registry.UpdateState(manifest.Id, PluginLifecycleState.Installed);

        return registry.GetEntry(manifest.Id)
            ?? throw new PluginLoadException(manifest.Id, "Plugin entry not found after registration.");
    }
}
