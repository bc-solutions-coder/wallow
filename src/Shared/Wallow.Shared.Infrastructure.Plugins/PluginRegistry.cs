using System.Collections.Concurrent;
using Wallow.Shared.Kernel.Plugins;

namespace Wallow.Shared.Infrastructure.Plugins;

public sealed class PluginRegistry
{
    private readonly ConcurrentDictionary<string, PluginRegistryEntry> _entries = new();

    public void Register(PluginManifest manifest)
    {
        PluginRegistryEntry entry = new PluginRegistryEntry(manifest);
        _entries.TryAdd(manifest.Id, entry);
    }

    public PluginRegistryEntry? GetEntry(string id) =>
        _entries.GetValueOrDefault(id);

    public IReadOnlyCollection<PluginRegistryEntry> GetAll() =>
        _entries.Values.ToList().AsReadOnly();

    public void UpdateState(string id, PluginLifecycleState state)
    {
        if (_entries.TryGetValue(id, out PluginRegistryEntry? entry))
        {
            entry.State = state;
        }
    }

    public void SetInstance(string id, IWallowPlugin instance, PluginAssemblyLoadContext loadContext)
    {
        if (_entries.TryGetValue(id, out PluginRegistryEntry? entry))
        {
            entry.Instance = instance;
            entry.LoadContext = loadContext;
        }
    }

    public void Remove(string id) =>
        _entries.TryRemove(id, out _);
}
