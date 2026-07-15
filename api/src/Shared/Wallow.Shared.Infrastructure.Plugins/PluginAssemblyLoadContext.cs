using System.Reflection;
using System.Runtime.Loader;

namespace Wallow.Shared.Infrastructure.Plugins;

public sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _pluginDirectory;

    public PluginAssemblyLoadContext(string pluginDirectory)
        : base(isCollectible: true)
    {
        _pluginDirectory = pluginDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string assemblyPath = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");

        if (File.Exists(assemblyPath))
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fall back to Default context for shared Wallow/Microsoft assemblies
        return null;
    }
}
